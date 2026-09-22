using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DTO;
using Application.Interfaces;
using Dapper;
using Domain;
using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Servicios;

public sealed class ComercialService(ApplicationDBContext db, IArchivoInventario excel, TimeProvider clock) : IComercialService
{
    private SqlConnection Connection => (SqlConnection)db.Database.GetDbConnection();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task Abrir() { if (Connection.State != ConnectionState.Open) await Connection.OpenAsync(); }

    // Primera version: serializa operaciones comerciales de escritura en SQL Server.
    // El bloqueo se comparte entre procesos, y se libera al confirmar/revertir.
    private async Task<T> Mutar<T>(int? usuario, Func<SqlTransaction, Task<T>> operation)
    {
        await Abrir();
        await using var tx = (SqlTransaction)await Connection.BeginTransactionAsync();
        await Connection.ExecuteAsync("""
            DECLARE @r int;
            EXEC @r=sys.sp_getapplock @Resource=N'ComercialStock.Operaciones',
                @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @r<0 THROW 51001,N'No se pudo adquirir el bloqueo comercial. Reintente.',1;
            """, transaction: tx);
        if (usuario.HasValue && !await Connection.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Usuario WHERE UsuarioId=@usuario AND Activo=1) THEN 1 ELSE 0 END AS bit)", new { usuario }, tx))
            throw new ReglaNegocioException("El usuario no está activo.");
        await Expirar(tx);
        var result = await operation(tx);
        await tx.CommitAsync();
        return result;
    }

    private Task<int> Expirar(SqlTransaction tx) => Connection.ExecuteAsync("""
        UPDATE dbo.ReservaStock SET Estado='Vencida',CerradaEn=@now WHERE Estado='Activa' AND VenceEn<=@now;
        UPDATE dbo.Cotizacion SET Estado='Vencida' WHERE Estado='Emitida' AND VenceEn<=@now;
        """, new { now = Now }, tx);
    public async Task ExpirarReservasAsync() => await Mutar<object?>(null, _ => Task.FromResult<object?>(null));

    public async Task<IReadOnlyList<ProductoFicha>> ProductosAsync()
    { await Abrir(); return (await Connection.QueryAsync<ProductoFicha>("SELECT * FROM dbo.Producto ORDER BY Nombre,ProductoId")).AsList(); }
    public async Task<IReadOnlyList<ClienteFicha>> ClientesAsync()
    { await Abrir(); return (await Connection.QueryAsync<ClienteFicha>("SELECT * FROM dbo.Cliente ORDER BY NombreRazonSocial,ClienteId")).AsList(); }
    public async Task<IReadOnlyList<BodegaFicha>> BodegasAsync()
    { await Abrir(); return (await Connection.QueryAsync<BodegaFicha>("SELECT * FROM dbo.Bodega ORDER BY Nombre,BodegaId")).AsList(); }

    private static void ValidarObjeto(object model)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new(model), results, true))
            throw new ReglaNegocioException(string.Join(" ", results.Select(x => x.ErrorMessage)));
    }
    private static void Cambio(int affected)
    { if (affected != 1) throw new ReglaNegocioException("El registro cambió o ya no existe. Recarga la página antes de guardar."); }

    public async Task GuardarProductoAsync(ProductoFicha m, int usuario)
    {
        ValidarObjeto(m);
        m.SKU = m.SKU.Trim(); m.Nombre = m.Nombre.Trim(); m.UnidadMedida = m.UnidadMedida.Trim().ToUpperInvariant();
        if (m.SKU.Length == 0 || m.Nombre.Length == 0 || m.UnidadMedida.Length == 0 || decimal.Round(m.PrecioReferencia, 4) != m.PrecioReferencia)
            throw new ReglaNegocioException("Revisa SKU, nombre, unidad y precio (máximo 4 decimales).");
        await Mutar(usuario, async tx =>
        {
            if (await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Producto WHERE SKU=@SKU AND ProductoId<>@ProductoId", m, tx) > 0)
                throw new ReglaNegocioException("El SKU ya está registrado.");
            if (m.ProductoId == 0) return await Connection.ExecuteAsync("INSERT dbo.Producto(SKU,Nombre,Descripcion,UnidadMedida,PrecioReferencia,Activo) VALUES(@SKU,@Nombre,@Descripcion,@UnidadMedida,@PrecioReferencia,@Activo)", m, tx);
            if (await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ReservaStock WHERE ProductoId=@ProductoId AND Estado='Activa'", m, tx) > 0 && !m.Activo)
                throw new ReglaNegocioException("No puedes desactivar un producto con reservas activas.");
            var unidad = await Connection.QuerySingleOrDefaultAsync<string>("SELECT UnidadMedida FROM dbo.Producto WHERE ProductoId=@ProductoId", m, tx);
            if (unidad != m.UnidadMedida && await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MovimientoStock WHERE ProductoId=@ProductoId", m, tx) > 0)
                throw new ReglaNegocioException("La unidad de un producto con movimientos no puede cambiar.");
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Producto SET SKU=@SKU,Nombre=@Nombre,Descripcion=@Descripcion,UnidadMedida=@UnidadMedida,PrecioReferencia=@PrecioReferencia,Activo=@Activo WHERE ProductoId=@ProductoId AND Version=@Version", m, tx));
            return 1;
        });
    }
    public async Task GuardarClienteAsync(ClienteFicha m, int usuario)
    {
        ValidarObjeto(m); m.NombreRazonSocial = m.NombreRazonSocial.Trim();
        if (m.NombreRazonSocial.Length == 0) throw new ReglaNegocioException("Ingresa el nombre del cliente.");
        await Mutar(usuario, async tx =>
        {
            if (m.ClienteId == 0) return await Connection.ExecuteAsync("INSERT dbo.Cliente(IdentificadorFiscal,NombreRazonSocial,Email,Telefono,Direccion,Activo) VALUES(@IdentificadorFiscal,@NombreRazonSocial,@Email,@Telefono,@Direccion,@Activo)", m, tx);
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Cliente SET IdentificadorFiscal=@IdentificadorFiscal,NombreRazonSocial=@NombreRazonSocial,Email=@Email,Telefono=@Telefono,Direccion=@Direccion,Activo=@Activo WHERE ClienteId=@ClienteId AND Version=@Version", m, tx));
            return 1;
        });
    }
    public async Task GuardarBodegaAsync(BodegaFicha m, int usuario)
    {
        ValidarObjeto(m); m.Codigo = m.Codigo.Trim(); m.Nombre = m.Nombre.Trim();
        if (m.Codigo.Length == 0 || m.Nombre.Length == 0) throw new ReglaNegocioException("Ingresa código y nombre.");
        await Mutar(usuario, async tx =>
        {
            if (await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Bodega WHERE Codigo=@Codigo AND BodegaId<>@BodegaId", m, tx) > 0)
                throw new ReglaNegocioException("El código de bodega ya existe.");
            if (m.BodegaId == 0) return await Connection.ExecuteAsync("INSERT dbo.Bodega(Codigo,Nombre,Direccion,Activa) VALUES(@Codigo,@Nombre,@Direccion,@Activa)", m, tx);
            if (!m.Activa && await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Existencia WHERE BodegaId=@BodegaId AND CantidadFisica>0", m, tx) > 0)
                throw new ReglaNegocioException("No puedes desactivar una bodega con existencias.");
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Bodega SET Codigo=@Codigo,Nombre=@Nombre,Direccion=@Direccion,Activa=@Activa WHERE BodegaId=@BodegaId AND Version=@Version", m, tx));
            return 1;
        });
    }

    private static void ValidarDocumento(DocumentoSolicitud m)
    {
        ValidarObjeto(m);
        if (m.ClaveOperacion == Guid.Empty || m.Lineas is null || m.Lineas.Count is < 1 or > 100)
            throw new ReglaNegocioException("El documento debe tener entre 1 y 100 líneas y una clave válida.");
        if (m.Lineas.Select(x => x.ProductoId).Distinct().Count() != m.Lineas.Count)
            throw new ReglaNegocioException("Un producto no puede aparecer dos veces en el documento.");
        foreach (var line in m.Lineas)
        {
            ValidarObjeto(line);
            if (line.DescuentoUnitario > line.PrecioUnitario || decimal.Round(line.Cantidad, 3) != line.Cantidad
                || decimal.Round(line.PrecioUnitario, 4) != line.PrecioUnitario || decimal.Round(line.DescuentoUnitario, 4) != line.DescuentoUnitario
                || decimal.Round(line.ImpuestoPorcentaje, 4) != line.ImpuestoPorcentaje)
                throw new ReglaNegocioException("Revisa descuentos y decimales: cantidad hasta 3, precios e impuesto hasta 4.");
        }
    }
    private static string Huella(object model) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model))));
    private static string HuellaDocumento(DocumentoSolicitud m, int usuario) => Huella(new
    {
        m.ClienteId, m.BodegaId, m.Observaciones, usuario,
        Lineas = m.Lineas.OrderBy(x => x.ProductoId).Select(x => new { x.ProductoId, x.Cantidad, x.PrecioUnitario, x.DescuentoUnitario, x.ImpuestoPorcentaje })
    });
    private async Task ValidarCabecera(int cliente, int bodega, SqlTransaction tx)
    {
        if (await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Cliente WHERE ClienteId=@cliente AND Activo=1", new { cliente }, tx) != 1)
            throw new ReglaNegocioException("El cliente no existe o está inactivo.");
        await ValidarBodega(bodega, tx);
    }
    private async Task ValidarBodega(int bodega, SqlTransaction tx)
    {
        if (await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Bodega WHERE BodegaId=@bodega AND Activa=1", new { bodega }, tx) != 1)
            throw new ReglaNegocioException("La bodega no existe o está inactiva.");
    }
    private async Task<List<LineaDocumento>> PrepararLineas(IEnumerable<LineaSolicitud> lines, SqlTransaction tx)
    {
        var result = new List<LineaDocumento>();
        foreach (var line in lines.OrderBy(x => x.ProductoId))
        {
            var product = await Connection.QuerySingleOrDefaultAsync<ProductoFicha>("SELECT * FROM dbo.Producto WHERE ProductoId=@ProductoId AND Activo=1", line, tx)
                ?? throw new ReglaNegocioException("Uno de los productos no existe o está inactivo.");
            result.Add(new LineaDocumento { ProductoId = line.ProductoId, SKU = product.SKU, DescripcionProducto = product.Nombre,
                Cantidad = line.Cantidad, PrecioUnitario = line.PrecioUnitario, DescuentoUnitario = line.DescuentoUnitario, TasaImpuesto = line.ImpuestoPorcentaje / 100m });
        }
        return result;
    }
    private async Task<int?> Repetida(string tabla, Guid clave, string hash, SqlTransaction tx)
    {
        // tabla solo puede ser una constante interna, nunca entrada HTTP.
        var row = await Connection.QuerySingleOrDefaultAsync<Operacion>($"SELECT {tabla}Id AS Id,HuellaSolicitud FROM dbo.{tabla} WHERE ClaveOperacion=@clave", new { clave }, tx);
        if (row is null) return null;
        if (row.HuellaSolicitud != hash) throw new ReglaNegocioException("La clave de la solicitud ya se utilizó con otros datos. Abre un formulario nuevo.");
        return row.Id;
    }
    private sealed class Operacion { public int Id { get; set; } public string? HuellaSolicitud { get; set; } }

    public Task<int> CrearCotizacionAsync(DocumentoSolicitud m, int usuario)
    {
        ValidarDocumento(m);
        return Mutar(usuario, async tx =>
        {
            var hash = HuellaDocumento(m, usuario);
            var previous = await Repetida("Cotizacion", m.ClaveOperacion, hash, tx);
            if (previous.HasValue) return previous.Value;
            await ValidarCabecera(m.ClienteId, m.BodegaId, tx);
            var lines = await PrepararLineas(m.Lineas, tx);
            var id = await Connection.ExecuteScalarAsync<int>("""
                INSERT dbo.Cotizacion(Numero,ClienteId,BodegaId,Estado,Moneda,CreadaEn,CreadaPorUsuarioId,Observaciones,ClaveOperacion,HuellaSolicitud)
                OUTPUT INSERTED.CotizacionId VALUES(@numero,@ClienteId,@BodegaId,'Borrador','CLP',@now,@usuario,@Observaciones,@ClaveOperacion,@hash)
                """, new { numero = "C-" + Guid.NewGuid().ToString("N")[..24], m.ClienteId, m.BodegaId, now = Now, usuario, m.Observaciones, m.ClaveOperacion, hash }, tx);
            await Connection.ExecuteAsync("UPDATE dbo.Cotizacion SET Numero=CONCAT('COT-',FORMAT(CotizacionId,'00000000')) WHERE CotizacionId=@id", new { id }, tx);
            await InsertarDetalles("Cotizacion", id, lines, tx);
            return id;
        });
    }
    private async Task InsertarDetalles(string tipo, int id, IEnumerable<LineaDocumento> lines, SqlTransaction tx)
    {
        foreach (var l in lines)
            await Connection.ExecuteAsync($"INSERT dbo.{tipo}Detalle({tipo}Id,ProductoId,DescripcionProducto,Cantidad,PrecioUnitario,DescuentoUnitario,TasaImpuesto) VALUES(@id,@ProductoId,@DescripcionProducto,@Cantidad,@PrecioUnitario,@DescuentoUnitario,@TasaImpuesto)",
                new { id, l.ProductoId, l.DescripcionProducto, l.Cantidad, l.PrecioUnitario, l.DescuentoUnitario, l.TasaImpuesto }, tx);
    }
    public async Task ActualizarCotizacionAsync(int id, DocumentoSolicitud m, byte[] version, int usuario)
    {
        ValidarDocumento(m);
        await Mutar(usuario, async tx =>
        {
            var doc = await Cotizacion(id, tx);
            if (doc.Estado != "Borrador") throw new ReglaNegocioException("Solo se pueden editar cotizaciones en borrador.");
            await ValidarCabecera(m.ClienteId, m.BodegaId, tx);
            var lines = await PrepararLineas(m.Lineas, tx);
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Cotizacion SET ClienteId=@ClienteId,BodegaId=@BodegaId,Observaciones=@Observaciones WHERE CotizacionId=@id AND Version=@version", new { id, version, m.ClienteId, m.BodegaId, m.Observaciones }, tx));
            await Connection.ExecuteAsync("DELETE FROM dbo.CotizacionDetalle WHERE CotizacionId=@id", new { id }, tx);
            await InsertarDetalles("Cotizacion", id, lines, tx);
            return id;
        });
    }
    private async Task<DocumentoResumen> Cotizacion(int id, SqlTransaction tx) =>
        await Connection.QuerySingleOrDefaultAsync<DocumentoResumen>("SELECT CotizacionId AS Id,ClienteId,BodegaId,Estado,Numero,VenceEn,Version,Observaciones FROM dbo.Cotizacion WHERE CotizacionId=@id", new { id }, tx)
        ?? throw new ReglaNegocioException("La cotización no existe.");
    private Task<IEnumerable<LineaDocumento>> Lineas(string tipo, int id, SqlTransaction? tx = null) =>
        Connection.QueryAsync<LineaDocumento>($"SELECT d.{tipo}DetalleId AS Id,d.*,p.SKU FROM dbo.{tipo}Detalle d JOIN dbo.Producto p ON p.ProductoId=d.ProductoId WHERE d.{tipo}Id=@id ORDER BY d.ProductoId", new { id }, tx);
    private async Task ComprobarStock(int bodega, IEnumerable<LineaDocumento> lines, int? propia, SqlTransaction tx)
    {
        foreach (var l in lines.OrderBy(x => x.ProductoId))
        {
            var available = await Connection.ExecuteScalarAsync<decimal?>("""
                SELECT e.CantidadFisica-ISNULL((SELECT SUM(r.Cantidad) FROM dbo.ReservaStock r
                    JOIN dbo.CotizacionDetalle d ON d.CotizacionDetalleId=r.CotizacionDetalleId
                    WHERE r.BodegaId=e.BodegaId AND r.ProductoId=e.ProductoId AND r.Estado='Activa'
                    AND r.VenceEn>@now AND (@propia IS NULL OR d.CotizacionId<>@propia)),0)
                FROM dbo.Existencia e WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Producto p ON p.ProductoId=e.ProductoId AND p.Activo=1
                WHERE e.BodegaId=@bodega AND e.ProductoId=@ProductoId
                """, new { bodega, l.ProductoId, now = Now, propia }, tx) ?? 0;
            if (available < l.Cantidad) throw new ReglaNegocioException($"Stock insuficiente para {l.SKU}. Disponible: {available:0.###}; solicitado: {l.Cantidad:0.###}.");
        }
    }
    public async Task EmitirCotizacionAsync(int id, int horas, byte[] version, int usuario)
    {
        if (horas is < 1 or > 720) throw new ReglaNegocioException("La reserva debe durar entre 1 y 720 horas.");
        await Mutar(usuario, async tx =>
        {
            var doc = await Cotizacion(id, tx);
            if (doc.Estado == "Emitida") return id; // Reintento sin duplicar reservas.
            if (doc.Estado != "Borrador") throw new ReglaNegocioException("Solo se puede emitir un borrador. Duplica una cotización vencida si necesitas una reserva nueva.");
            await ValidarCabecera(doc.ClienteId, doc.BodegaId, tx);
            var lines = (await Lineas("Cotizacion", id, tx)).ToList();
            if (lines.Count == 0) throw new ReglaNegocioException("Agrega al menos un producto.");
            await ComprobarStock(doc.BodegaId, lines, null, tx);
            var now = Now; var vence = now.AddHours(horas);
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Cotizacion SET Estado='Emitida',EmitidaEn=@now,VenceEn=@vence WHERE CotizacionId=@id AND Version=@version", new { id, now, vence, version }, tx));
            foreach (var l in lines)
                await Connection.ExecuteAsync("INSERT dbo.ReservaStock(CotizacionDetalleId,BodegaId,ProductoId,Cantidad,Estado,CreadaEn,VenceEn) VALUES(@Id,@BodegaId,@ProductoId,@Cantidad,'Activa',@now,@vence)", new { l.Id, doc.BodegaId, l.ProductoId, l.Cantidad, now, vence }, tx);
            return id;
        });
    }
    public async Task CancelarCotizacionAsync(int id, byte[] version, int usuario)
    {
        await Mutar(usuario, async tx =>
        {
            var doc = await Cotizacion(id, tx);
            if (doc.Estado == "Cancelada") return id;
            if (doc.Estado == "Convertida") throw new ReglaNegocioException("Una cotización vendida no puede cancelarse.");
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Cotizacion SET Estado='Cancelada' WHERE CotizacionId=@id AND Version=@version", new { id, version }, tx));
            await Connection.ExecuteAsync("UPDATE r SET Estado='Liberada',CerradaEn=@now FROM dbo.ReservaStock r JOIN dbo.CotizacionDetalle d ON d.CotizacionDetalleId=r.CotizacionDetalleId WHERE d.CotizacionId=@id AND r.Estado='Activa'", new { id, now = Now }, tx);
            return id;
        });
    }

    public Task<int> CrearVentaAsync(DocumentoSolicitud m, int usuario)
    {
        ValidarDocumento(m);
        return Mutar(usuario, async tx =>
        {
            var hash = HuellaDocumento(m, usuario);
            var previous = await Repetida("Venta", m.ClaveOperacion, hash, tx);
            if (previous.HasValue) return previous.Value;
            await ValidarCabecera(m.ClienteId, m.BodegaId, tx);
            return await RegistrarVenta(m.ClienteId, m.BodegaId, null, m.ClaveOperacion, hash, m.Observaciones, await PrepararLineas(m.Lineas, tx), usuario, tx);
        });
    }
    public Task<int> VenderCotizacionAsync(int id, Guid clave, int usuario)
    {
        if (clave == Guid.Empty) throw new ReglaNegocioException("Clave de operación inválida.");
        return Mutar(usuario, async tx =>
        {
            var hash = Huella(new { CotizacionId = id, usuario });
            var previous = await Repetida("Venta", clave, hash, tx);
            if (previous.HasValue) return previous.Value;
            var doc = await Cotizacion(id, tx);
            var sale = await Connection.QuerySingleOrDefaultAsync<int?>("SELECT VentaId FROM dbo.Venta WHERE CotizacionId=@id", new { id }, tx);
            if (sale.HasValue) return sale.Value;
            if (doc.Estado is not ("Emitida" or "Vencida")) throw new ReglaNegocioException("Emite la cotización antes de venderla. Una cancelada no puede venderse.");
            await ValidarCabecera(doc.ClienteId, doc.BodegaId, tx);
            return await RegistrarVenta(doc.ClienteId, doc.BodegaId, id, clave, hash, doc.Observaciones, (await Lineas("Cotizacion", id, tx)).ToList(), usuario, tx);
        });
    }
    private async Task<int> RegistrarVenta(int cliente, int bodega, int? cotizacion, Guid clave, string hash, string? observaciones, List<LineaDocumento> lines, int usuario, SqlTransaction tx)
    {
        if (lines.Count == 0) throw new ReglaNegocioException("La venta no tiene productos.");
        await ComprobarStock(bodega, lines, cotizacion, tx);
        var now = Now;
        var id = await Connection.ExecuteScalarAsync<int>("""
            INSERT dbo.Venta(Numero,ClienteId,BodegaId,CotizacionId,ClaveOperacion,HuellaSolicitud,Estado,Moneda,ConfirmadaEn,CreadaPorUsuarioId,Observaciones)
            OUTPUT INSERTED.VentaId VALUES(@numero,@cliente,@bodega,@cotizacion,@clave,@hash,'Confirmada','CLP',@now,@usuario,@observaciones)
            """, new { numero = "V-" + Guid.NewGuid().ToString("N")[..24], cliente, bodega, cotizacion, clave, hash, now, usuario, observaciones }, tx);
        await Connection.ExecuteAsync("UPDATE dbo.Venta SET Numero=CONCAT('VEN-',FORMAT(VentaId,'00000000')) WHERE VentaId=@id", new { id }, tx);
        await InsertarDetalles("Venta", id, lines, tx);
        foreach (var line in await Lineas("Venta", id, tx))
        {
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.Existencia SET CantidadFisica=CantidadFisica-@Cantidad WHERE BodegaId=@bodega AND ProductoId=@ProductoId AND CantidadFisica>=@Cantidad", new { bodega, line.ProductoId, line.Cantidad }, tx));
            await Connection.ExecuteAsync("INSERT dbo.MovimientoStock(BodegaId,ProductoId,Tipo,Cantidad,VentaDetalleId,CreadoEn,CreadoPorUsuarioId) VALUES(@bodega,@ProductoId,'SalidaVenta',-@Cantidad,@Id,@now,@usuario)", new { bodega, line.ProductoId, line.Cantidad, line.Id, now, usuario }, tx);
        }
        if (cotizacion.HasValue)
        {
            await Connection.ExecuteAsync("UPDATE r SET Estado='Consumida',CerradaEn=@now FROM dbo.ReservaStock r JOIN dbo.CotizacionDetalle d ON d.CotizacionDetalleId=r.CotizacionDetalleId WHERE d.CotizacionId=@cotizacion AND r.Estado='Activa'; UPDATE dbo.Cotizacion SET Estado='Convertida' WHERE CotizacionId=@cotizacion", new { cotizacion, now }, tx);
        }
        return id;
    }

    private static string DocumentoQuery(bool ventas)
    {
        var tipo = ventas ? "Venta" : "Cotizacion";
        var fecha = ventas ? "ConfirmadaEn" : "CreadaEn";
        var extra = ventas ? "NULL AS VenceEn,NULL AS Version,h.CotizacionId" : "h.VenceEn,h.Version,NULL AS CotizacionId";
        var estado = ventas ? "h.Estado" : "CASE WHEN h.Estado='Emitida' AND h.VenceEn<=@now THEN 'Vencida' ELSE h.Estado END";
        return $"""
            SELECT h.{tipo}Id AS Id,h.Numero,h.ClienteId,c.NombreRazonSocial AS Cliente,h.BodegaId,b.Nombre AS Bodega,
                {estado} AS Estado,h.{fecha} AS Fecha,{extra},h.Observaciones,
                ISNULL(t.Neto,0) AS Neto,ISNULL(t.Impuesto,0) AS Impuesto
            FROM dbo.{tipo} h JOIN dbo.Cliente c ON c.ClienteId=h.ClienteId JOIN dbo.Bodega b ON b.BodegaId=h.BodegaId
            OUTER APPLY(SELECT SUM(ROUND(d.Cantidad*(d.PrecioUnitario-d.DescuentoUnitario),0)) AS Neto,
                SUM(ROUND(ROUND(d.Cantidad*(d.PrecioUnitario-d.DescuentoUnitario),0)*d.TasaImpuesto,0)) AS Impuesto
                FROM dbo.{tipo}Detalle d WHERE d.{tipo}Id=h.{tipo}Id) t
            """;
    }
    public async Task<IReadOnlyList<DocumentoResumen>> DocumentosAsync(bool ventas, string? buscar = null)
    {
        await Abrir();
        return (await Connection.QueryAsync<DocumentoResumen>(DocumentoQuery(ventas) + $" WHERE (@q IS NULL OR h.Numero LIKE @q OR c.NombreRazonSocial LIKE @q) ORDER BY h.{(ventas ? "VentaId" : "CotizacionId")} DESC OFFSET 0 ROWS FETCH NEXT 200 ROWS ONLY", new { q = string.IsNullOrWhiteSpace(buscar) ? null : "%" + buscar.Trim() + "%", now = Now })).AsList();
    }
    public async Task<DocumentoDetalle> DocumentoAsync(bool venta, int id)
    {
        await Abrir();
        var head = await Connection.QuerySingleOrDefaultAsync<DocumentoResumen>(DocumentoQuery(venta) + $" WHERE h.{(venta ? "VentaId" : "CotizacionId")}=@id", new { id, now = Now })
            ?? throw new ReglaNegocioException("El documento no existe.");
        return new(head, (await Lineas(venta ? "Venta" : "Cotizacion", id)).ToList());
    }
    public async Task<IReadOnlyList<StockFila>> StockAsync(int? bodega = null, string? buscar = null)
    {
        await Abrir();
        return (await Connection.QueryAsync<StockFila>("""
            SELECT b.BodegaId,b.Nombre AS Bodega,p.ProductoId,p.SKU,p.Nombre AS Producto,p.UnidadMedida,
                ISNULL(e.CantidadFisica,0) AS CantidadFisica,ISNULL(r.Cantidad,0) AS Reservada
            FROM dbo.Bodega b CROSS JOIN dbo.Producto p
            LEFT JOIN dbo.Existencia e ON e.BodegaId=b.BodegaId AND e.ProductoId=p.ProductoId
            OUTER APPLY(SELECT SUM(Cantidad) AS Cantidad FROM dbo.ReservaStock
                WHERE BodegaId=b.BodegaId AND ProductoId=p.ProductoId AND Estado='Activa' AND VenceEn>@now) r
            WHERE (@bodega IS NULL OR b.BodegaId=@bodega) AND (p.Activo=1 OR e.CantidadFisica>0)
                AND (b.Activa=1 OR e.CantidadFisica>0) AND (@q IS NULL OR p.SKU LIKE @q OR p.Nombre LIKE @q)
            ORDER BY b.Nombre,p.Nombre
            """, new { bodega, q = string.IsNullOrWhiteSpace(buscar) ? null : "%" + buscar.Trim() + "%", now = Now })).AsList();
    }
    private static void ValidarFechas(DateTime desde, DateTime hasta)
    { if (hasta < desde || (hasta - desde).TotalDays > 366) throw new ReglaNegocioException("Selecciona un período de hasta 366 días, con inicio anterior al fin."); }
    public async Task<IReadOnlyList<DocumentoResumen>> ReporteVentasAsync(DateTime desde, DateTime hasta, int? bodega)
    {
        ValidarFechas(desde, hasta); await Abrir();
        return (await Connection.QueryAsync<DocumentoResumen>(DocumentoQuery(true) + " WHERE h.ConfirmadaEn>=@desde AND h.ConfirmadaEn<@fin AND (@bodega IS NULL OR h.BodegaId=@bodega) ORDER BY h.ConfirmadaEn DESC", new { desde = desde.Date, fin = hasta.Date.AddDays(1), bodega })).AsList();
    }
    public async Task<IReadOnlyList<MovimientoFila>> MovimientosAsync(int? bodega, DateTime desde, DateTime hasta)
    {
        ValidarFechas(desde, hasta); await Abrir();
        return (await Connection.QueryAsync<MovimientoFila>("""
            SELECT m.MovimientoStockId,m.CreadoEn,b.Nombre AS Bodega,p.SKU,p.Nombre AS Producto,m.Tipo,m.Cantidad,u.Nombre AS Usuario,
                COALESCE(v.Numero,CONCAT('IMP-',i.ImportacionInventarioId)) AS Origen
            FROM dbo.MovimientoStock m JOIN dbo.Bodega b ON b.BodegaId=m.BodegaId JOIN dbo.Producto p ON p.ProductoId=m.ProductoId
            JOIN dbo.Usuario u ON u.UsuarioId=m.CreadoPorUsuarioId
            LEFT JOIN dbo.VentaDetalle vd ON vd.VentaDetalleId=m.VentaDetalleId LEFT JOIN dbo.Venta v ON v.VentaId=vd.VentaId
            LEFT JOIN dbo.ImportacionInventarioDetalle i ON i.ImportacionInventarioDetalleId=m.ImportacionInventarioDetalleId
            WHERE m.CreadoEn>=@desde AND m.CreadoEn<@fin AND (@bodega IS NULL OR m.BodegaId=@bodega)
            ORDER BY m.MovimientoStockId DESC
            """, new { desde = desde.Date, fin = hasta.Date.AddDays(1), bodega })).AsList();
    }

    public Task<long> PrepararImportacionAsync(int bodega, string archivo, byte[] contenido, string? referencia, Guid clave, int usuario)
    {
        if (clave == Guid.Empty || referencia?.Length > 100 || archivo.Length > 255 || Path.GetExtension(archivo).ToLowerInvariant() != ".xlsx")
            throw new ReglaNegocioException("Revisa el archivo XLSX, la referencia y la clave de la solicitud.");
        var rows = excel.Leer(contenido);
        var hash = Convert.ToHexString(SHA256.HashData(contenido));
        return Mutar(usuario, async tx =>
        {
            var old = await Connection.QuerySingleOrDefaultAsync<ImportacionFila>("SELECT * FROM dbo.ImportacionInventario WHERE ClaveOperacion=@clave", new { clave }, tx);
            if (old is not null)
            {
                if (old.HashArchivo != hash || old.BodegaId != bodega || old.ReferenciaRecepcion != referencia)
                    throw new ReglaNegocioException("La clave de carga ya se usó con otros datos.");
                return old.ImportacionInventarioId;
            }
            await ValidarBodega(bodega, tx);
            foreach (var row in rows)
            {
                row.ProductoId = await Connection.QuerySingleOrDefaultAsync<int?>("SELECT ProductoId FROM dbo.Producto WHERE SKU=@SKUOriginal AND Activo=1", row, tx);
                if (!row.ProductoId.HasValue) row.ErrorValidacion = "SKU inexistente o producto inactivo.";
            }
            // Detecta tambien duplicados que la intercalacion SQL considere iguales.
            foreach (var group in rows.Where(x => x.ProductoId.HasValue).GroupBy(x => x.ProductoId).Where(x => x.Count() > 1))
                foreach (var row in group) row.ErrorValidacion = "Producto repetido en el archivo.";
            var estado = rows.Any(x => x.ErrorValidacion is not null) ? "ConErrores" : "Validada";
            var id = await Connection.ExecuteScalarAsync<long>("""
                INSERT dbo.ImportacionInventario(BodegaId,NombreArchivo,HashArchivo,ClaveOperacion,Estado,ReferenciaRecepcion,CreadaEn,CreadaPorUsuarioId)
                OUTPUT INSERTED.ImportacionInventarioId VALUES(@bodega,@archivo,@hash,@clave,@estado,@referencia,@now,@usuario)
                """, new { bodega, archivo, hash, clave, estado, referencia, now = Now, usuario }, tx);
            foreach (var row in rows)
                await Connection.ExecuteAsync("INSERT dbo.ImportacionInventarioDetalle(ImportacionInventarioId,NumeroFila,SKUOriginal,CantidadOriginal,ProductoId,Cantidad,ErrorValidacion) VALUES(@id,@NumeroFila,@SKUOriginal,@CantidadOriginal,@ProductoId,@Cantidad,@ErrorValidacion)",
                    new { id, row.NumeroFila, row.SKUOriginal, row.CantidadOriginal, row.ProductoId, row.Cantidad, row.ErrorValidacion }, tx);
            return id;
        });
    }
    public async Task AplicarImportacionAsync(long id, byte[] version, int usuario, bool confirmarDuplicado = false)
    {
        await Mutar(usuario, async tx =>
        {
            var head = await Connection.QuerySingleOrDefaultAsync<ImportacionFila>("SELECT * FROM dbo.ImportacionInventario WHERE ImportacionInventarioId=@id", new { id }, tx)
                ?? throw new ReglaNegocioException("La importación no existe.");
            if (head.Estado == "Aplicada") return id;
            if (head.Estado != "Validada") throw new ReglaNegocioException("Solo puedes aplicar una importación validada y sin errores.");
            if (!confirmarDuplicado && await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ImportacionInventario WHERE BodegaId=@BodegaId AND HashArchivo=@HashArchivo AND Estado='Aplicada' AND ImportacionInventarioId<>@ImportacionInventarioId", head, tx) > 0)
                throw new ReglaNegocioException("El archivo ya fue aplicado en esta bodega. Revisa la carga y confirma que corresponde a otra recepción.");
            await ValidarBodega(head.BodegaId, tx);
            var lines = (await Connection.QueryAsync<ImportacionLineaDb>("SELECT * FROM dbo.ImportacionInventarioDetalle WHERE ImportacionInventarioId=@id ORDER BY ProductoId", new { id }, tx)).ToList();
            if (lines.Count == 0 || lines.Any(x => x.ProductoId is null || x.Cantidad is null or <= 0 || x.ErrorValidacion is not null))
                throw new ReglaNegocioException("La importación tiene filas inválidas.");
            foreach (var line in lines)
            {
                if (await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Producto WHERE ProductoId=@ProductoId AND Activo=1", line, tx) != 1)
                    throw new ReglaNegocioException("Un producto fue desactivado. Cancela esta carga y prepara otra.");
                await Connection.ExecuteAsync("""
                    IF NOT EXISTS(SELECT 1 FROM dbo.Existencia WITH(UPDLOCK,HOLDLOCK) WHERE BodegaId=@BodegaId AND ProductoId=@ProductoId)
                        INSERT dbo.Existencia(BodegaId,ProductoId,CantidadFisica) VALUES(@BodegaId,@ProductoId,0);
                    UPDATE dbo.Existencia SET CantidadFisica=CantidadFisica+@Cantidad WHERE BodegaId=@BodegaId AND ProductoId=@ProductoId;
                    INSERT dbo.MovimientoStock(BodegaId,ProductoId,Tipo,Cantidad,ImportacionInventarioDetalleId,CreadoEn,CreadoPorUsuarioId)
                        VALUES(@BodegaId,@ProductoId,'EntradaExcel',@Cantidad,@ImportacionInventarioDetalleId,@now,@usuario);
                    """, new { head.BodegaId, line.ProductoId, line.Cantidad, line.ImportacionInventarioDetalleId, now = Now, usuario }, tx);
            }
            Cambio(await Connection.ExecuteAsync("UPDATE dbo.ImportacionInventario SET Estado='Aplicada',AplicadaEn=@now WHERE ImportacionInventarioId=@id AND Version=@version", new { id, version, now = Now }, tx));
            return id;
        });
    }
    private sealed class ImportacionLineaDb
    {
        public long ImportacionInventarioDetalleId { get; set; }
        public int? ProductoId { get; set; }
        public decimal? Cantidad { get; set; }
        public string? ErrorValidacion { get; set; }
    }
    public async Task CancelarImportacionAsync(long id, int usuario)
    {
        await Mutar(usuario, async tx =>
        {
            var state = await Connection.QuerySingleOrDefaultAsync<string>("SELECT Estado FROM dbo.ImportacionInventario WHERE ImportacionInventarioId=@id", new { id }, tx);
            if (state is null or "Aplicada") throw new ReglaNegocioException("No se puede cancelar una carga aplicada o inexistente.");
            return await Connection.ExecuteAsync("UPDATE dbo.ImportacionInventario SET Estado='Cancelada' WHERE ImportacionInventarioId=@id", new { id }, tx);
        });
    }
    public async Task<IReadOnlyList<ImportacionFila>> ImportacionesAsync()
    {
        await Abrir();
        return (await Connection.QueryAsync<ImportacionFila>("SELECT TOP(200) i.*,b.Nombre AS Bodega FROM dbo.ImportacionInventario i JOIN dbo.Bodega b ON b.BodegaId=i.BodegaId ORDER BY i.ImportacionInventarioId DESC")).AsList();
    }
    public async Task<ImportacionDetalle> ImportacionAsync(long id)
    {
        await Abrir();
        var head = await Connection.QuerySingleOrDefaultAsync<ImportacionFila>("""
            SELECT i.*,b.Nombre AS Bodega,CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ImportacionInventario o
                WHERE o.ImportacionInventarioId<>i.ImportacionInventarioId AND o.BodegaId=i.BodegaId AND o.HashArchivo=i.HashArchivo AND o.Estado='Aplicada') THEN 1 ELSE 0 END AS bit) AS ArchivoRepetido
            FROM dbo.ImportacionInventario i JOIN dbo.Bodega b ON b.BodegaId=i.BodegaId WHERE i.ImportacionInventarioId=@id
            """, new { id }) ?? throw new ReglaNegocioException("La importación no existe.");
        return new(head, (await Connection.QueryAsync<FilaExcel>("SELECT * FROM dbo.ImportacionInventarioDetalle WHERE ImportacionInventarioId=@id ORDER BY NumeroFila", new { id })).AsList());
    }
}
