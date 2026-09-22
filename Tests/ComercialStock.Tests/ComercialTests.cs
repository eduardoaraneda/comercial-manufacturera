using System.Net;
using Application.DTO;
using Application.Interfaces;
using Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ComercialStock.Tests;

public sealed partial class AccesoTests
{
    private sealed record Negocio(int Usuario, int Cliente, int Bodega, int Producto);
    private async Task<Negocio> PrepararNegocio(decimal cantidad = 5)
    {
        await Seed();
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IComercialService>();
        var actor = (await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().Usuarios.SingleAsync()).UsuarioId;
        await service.GuardarClienteAsync(new ClienteFicha { NombreRazonSocial = "Cliente de integración" }, actor);
        await service.GuardarBodegaAsync(new BodegaFicha { Codigo = "B-01", Nombre = "Bodega de pruebas" }, actor);
        await service.GuardarProductoAsync(new ProductoFicha { SKU = "TEST-01", Nombre = "Producto de prueba", PrecioReferencia = 1000 }, actor);
        var customer = (await service.ClientesAsync()).Single().ClienteId;
        var warehouse = (await service.BodegasAsync()).Single().BodegaId;
        var product = (await service.ProductosAsync()).Single().ProductoId;
        if (cantidad > 0)
        {
            var excel = scope.ServiceProvider.GetRequiredService<IArchivoInventario>();
            var bytes = excel.Exportar("Ingreso", ["SKU", "Cantidad"], [new object?[] { "TEST-01", cantidad }]);
            var id = await service.PrepararImportacionAsync(warehouse, "ingreso.xlsx", bytes, "Recepción inicial", Guid.NewGuid(), actor);
            var preview = await service.ImportacionAsync(id);
            Assert.Equal(0, (await service.StockAsync(warehouse)).Single().CantidadFisica);
            await service.AplicarImportacionAsync(id, preview.Cabecera.Version, actor);
        }
        return new(actor, customer, warehouse, product);
    }
    private static DocumentoSolicitud Pedido(Negocio n, decimal cantidad = 2) => new()
    {
        ClienteId = n.Cliente, BodegaId = n.Bodega,
        Lineas = [new() { ProductoId = n.Producto, Cantidad = cantidad, PrecioUnitario = 1000, ImpuestoPorcentaje = 19 }]
    };

    [Fact]
    public async Task StockInsuficienteSeMuestraEnLaCotizacionSinReservar()
    {
        var n = await PrepararNegocio(0);
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IComercialService>();
        var id = await service.CrearCotizacionAsync(Pedido(n), n.Usuario);
        var quote = await service.DocumentoAsync(false, id);
        using var client = Client(); await Login(client);
        var csrf = await Token(client, "/Documentos/Detalle/" + id);
        var response = await client.PostAsync("/Documentos/Emitir", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf, ["id"] = id.ToString(),
            ["horas"] = "24", ["version"] = Convert.ToBase64String(quote.Cabecera.Version)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var page = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Stock insuficiente para TEST-01", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("Ingresar stock desde Excel", html);
        Assert.Equal("Borrador", (await service.DocumentoAsync(false, id)).Cabecera.Estado);
        Assert.Equal(0, (await service.StockAsync()).Single().Reservada);
    }

    [Fact]
    public async Task VentaDescuentaUnaVezYReporteConservaPreciosHistoricos()
    {
        var n = await PrepararNegocio();
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IComercialService>();
        var request = Pedido(n);
        var id = await service.CrearVentaAsync(request, n.Usuario);
        Assert.Equal(id, await service.CrearVentaAsync(request, n.Usuario));
        Assert.Equal(3, (await service.StockAsync(n.Bodega)).Single().CantidadFisica);
        var doc = await service.DocumentoAsync(true, id);
        Assert.Equal(2000, doc.Cabecera.Neto);
        Assert.Equal(380, doc.Cabecera.Impuesto);
        Assert.Equal(2380, doc.Cabecera.Total);
        var product = (await service.ProductosAsync()).Single(); product.PrecioReferencia = 3000;
        await service.GuardarProductoAsync(product, n.Usuario);
        Assert.Equal(1000, (await service.DocumentoAsync(true, id)).Lineas.Single().PrecioUnitario);
        var report = await service.ReporteVentasAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date, n.Bodega);
        Assert.Single(report); Assert.Equal(2380, report.Single().Total);
        request.Lineas[0].Cantidad = 3;
        await Assert.ThrowsAsync<ReglaNegocioException>(() => service.CrearVentaAsync(request, n.Usuario));
    }

    [Fact]
    public async Task CotizacionReservaSinDescontarYVentaConsumeSuPropiaReserva()
    {
        var n = await PrepararNegocio();
        using var scope = app.Services.CreateScope(); var s = scope.ServiceProvider.GetRequiredService<IComercialService>();
        var quote = await s.CrearCotizacionAsync(Pedido(n, 4), n.Usuario);
        Assert.Equal(5, (await s.StockAsync()).Single().Disponible);
        await s.EmitirCotizacionAsync(quote, 24, (await s.DocumentoAsync(false, quote)).Cabecera.Version, n.Usuario);
        Assert.Equal(5, (await s.StockAsync()).Single().CantidadFisica);
        Assert.Equal(1, (await s.StockAsync()).Single().Disponible);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => s.CrearVentaAsync(Pedido(n, 2), n.Usuario));
        Assert.Empty(await s.DocumentosAsync(true));
        var sale = await s.VenderCotizacionAsync(quote, Guid.NewGuid(), n.Usuario);
        Assert.Equal(sale, await s.VenderCotizacionAsync(quote, Guid.NewGuid(), n.Usuario));
        var stock = (await s.StockAsync()).Single();
        Assert.Equal(1, stock.CantidadFisica); Assert.Equal(0, stock.Reservada);
        Assert.Equal("Convertida", (await s.DocumentoAsync(false, quote)).Cabecera.Estado);
    }

    [Fact]
    public async Task ReservaVencidaNoRobaStockDeOtraCotizacionYCancelarLibera()
    {
        var n = await PrepararNegocio();
        using var scope = app.Services.CreateScope(); var s = scope.ServiceProvider.GetRequiredService<IComercialService>();
        var a = await s.CrearCotizacionAsync(Pedido(n, 4), n.Usuario);
        await s.EmitirCotizacionAsync(a, 1, (await s.DocumentoAsync(false, a)).Cabecera.Version, n.Usuario);
        app.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(5, (await s.StockAsync()).Single().Disponible);
        var b = await s.CrearCotizacionAsync(Pedido(n, 3), n.Usuario);
        await s.EmitirCotizacionAsync(b, 24, (await s.DocumentoAsync(false, b)).Cabecera.Version, n.Usuario);
        Assert.Equal("Vencida", (await s.DocumentoAsync(false, a)).Cabecera.Estado);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => s.VenderCotizacionAsync(a, Guid.NewGuid(), n.Usuario));
        await s.CancelarCotizacionAsync(b, (await s.DocumentoAsync(false, b)).Cabecera.Version, n.Usuario);
        Assert.Equal(5, (await s.StockAsync()).Single().Disponible);
        await s.VenderCotizacionAsync(a, Guid.NewGuid(), n.Usuario);
        Assert.Equal(1, (await s.StockAsync()).Single().CantidadFisica);
    }

    [Fact]
    public async Task VentasSimultaneasNoSobrevenden()
    {
        var n = await PrepararNegocio();
        using var first = app.Services.CreateScope(); using var second = app.Services.CreateScope();
        async Task<bool> Sell(IServiceProvider services)
        {
            try { await services.GetRequiredService<IComercialService>().CrearVentaAsync(Pedido(n, 4), n.Usuario); return true; }
            catch (ReglaNegocioException) { return false; }
        }
        var results = await Task.WhenAll(Sell(first.ServiceProvider), Sell(second.ServiceProvider));
        Assert.Single(results, result => result);
        using var check = app.Services.CreateScope(); var s = check.ServiceProvider.GetRequiredService<IComercialService>();
        Assert.Single(await s.DocumentosAsync(true)); Assert.Equal(1, (await s.StockAsync()).Single().CantidadFisica);
        Assert.Single(await s.MovimientosAsync(n.Bodega, DateTime.UtcNow.Date, DateTime.UtcNow.Date), x => x.Tipo == "SalidaVenta");
    }

    [Fact]
    public async Task VentaDeVariosProductosFallaCompletaCuandoFaltaUno()
    {
        var n = await PrepararNegocio();
        using var scope = app.Services.CreateScope(); var s = scope.ServiceProvider.GetRequiredService<IComercialService>();
        await s.GuardarProductoAsync(new ProductoFicha { SKU = "SIN-STOCK", Nombre = "Sin existencias" }, n.Usuario);
        var request = Pedido(n);
        request.Lineas.Add(new LineaSolicitud { ProductoId = (await s.ProductosAsync()).Single(x => x.SKU == "SIN-STOCK").ProductoId, Cantidad = 1 });
        await Assert.ThrowsAsync<ReglaNegocioException>(() => s.CrearVentaAsync(request, n.Usuario));
        Assert.Empty(await s.DocumentosAsync(true));
        Assert.Equal(5, (await s.StockAsync()).Single(x => x.ProductoId == n.Producto).CantidadFisica);
        Assert.DoesNotContain(await s.MovimientosAsync(n.Bodega, DateTime.UtcNow.Date, DateTime.UtcNow.Date), x => x.Tipo == "SalidaVenta");
    }

    [Fact]
    public async Task ExcelSeValidaAntesDeAplicarYReintentoNoDuplicaIngresos()
    {
        var n = await PrepararNegocio(0);
        using var scope = app.Services.CreateScope(); var s = scope.ServiceProvider.GetRequiredService<IComercialService>();
        var excel = scope.ServiceProvider.GetRequiredService<IArchivoInventario>();
        var invalid = excel.Exportar("Ingreso", ["SKU", "Cantidad"], [new object?[] { "TEST-01", 3m }, new object?[] { "NO-EXISTE", -1m }]);
        var invalidId = await s.PrepararImportacionAsync(n.Bodega, "incorrecto.xlsx", invalid, null, Guid.NewGuid(), n.Usuario);
        var invalidPreview = await s.ImportacionAsync(invalidId);
        Assert.Equal("ConErrores", invalidPreview.Cabecera.Estado);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => s.AplicarImportacionAsync(invalidId, invalidPreview.Cabecera.Version, n.Usuario));
        Assert.Equal(0, (await s.StockAsync()).Single().CantidadFisica);
        var valid = excel.Exportar("Ingreso", ["SKU", "Cantidad"], [new object?[] { "TEST-01", 3m }]);
        var key = Guid.NewGuid();
        var id = await s.PrepararImportacionAsync(n.Bodega, "correcto.xlsx", valid, null, key, n.Usuario);
        Assert.Equal(id, await s.PrepararImportacionAsync(n.Bodega, "correcto.xlsx", valid, null, key, n.Usuario));
        var preview = await s.ImportacionAsync(id);
        await s.AplicarImportacionAsync(id, preview.Cabecera.Version, n.Usuario);
        await s.AplicarImportacionAsync(id, preview.Cabecera.Version, n.Usuario);
        Assert.Equal(3, (await s.StockAsync()).Single().CantidadFisica);
        var duplicate = await s.PrepararImportacionAsync(n.Bodega, "correcto.xlsx", valid, "Otra recepción", Guid.NewGuid(), n.Usuario);
        var duplicatePreview = await s.ImportacionAsync(duplicate);
        Assert.True(duplicatePreview.Cabecera.ArchivoRepetido);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => s.AplicarImportacionAsync(duplicate, duplicatePreview.Cabecera.Version, n.Usuario));
        await s.AplicarImportacionAsync(duplicate, duplicatePreview.Cabecera.Version, n.Usuario, true);
        Assert.Equal(6, (await s.StockAsync()).Single().CantidadFisica);
    }

    [Fact]
    public async Task ModulosRenderizanYPerfilesImpidenAccesoNoAutorizado()
    {
        await PrepararNegocio();
        using var client = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(client)).StatusCode);
        foreach (var path in new[] { "/Catalogos/Clientes", "/Catalogos/Productos", "/Catalogos/Bodegas", "/Documentos/Cotizaciones", "/Documentos/Ventas", "/Documentos/Crear", "/Inventario", "/Inventario/Cargar", "/Inventario/Importaciones", "/Reportes" })
        {
            var response = await client.GetAsync(path);
            Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode}");
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventario/Plantilla")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = (await users.FindByEmailAsync(Email))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "Administrador")).Succeeded);
            await users.UpdateSecurityStampAsync(user);
        }
        using var restricted = Client(); await Login(restricted);
        var blocked = await restricted.GetAsync("/Documentos/Crear");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("AccesoDenegado", blocked.Headers.Location!.ToString());
    }

    [Fact]
    public async Task FormulariosHttpCreanCotizacionReservanVendenYAplicanExcel()
    {
        var n = await PrepararNegocio();
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IComercialService>();
        using var client = Client(); await Login(client);
        var csrf = await Token(client, "/Documentos/Crear");
        var posted = await client.PostAsync("/Documentos/Crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf, ["Venta"] = "false",
            ["Documento.ClaveOperacion"] = Guid.NewGuid().ToString(),
            ["Documento.ClienteId"] = n.Cliente.ToString(), ["Documento.BodegaId"] = n.Bodega.ToString(),
            ["Documento.HorasReserva"] = "24", ["Documento.Lineas[0].ProductoId"] = n.Producto.ToString(),
            ["Documento.Lineas[0].Cantidad"] = "1.5", ["Documento.Lineas[0].PrecioUnitario"] = "1000",
            ["Documento.Lineas[0].DescuentoUnitario"] = "0", ["Documento.Lineas[0].ImpuestoPorcentaje"] = "19"
        }));
        Assert.True(posted.StatusCode == HttpStatusCode.Redirect, await posted.Content.ReadAsStringAsync());
        var quote = (await service.DocumentosAsync(false)).Single();
        Assert.Equal(1785, quote.Total);
        var detailUrl = "/Documentos/Detalle/" + quote.Id;
        csrf = await Token(client, detailUrl);
        var emitted = await client.PostAsync("/Documentos/Emitir", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf, ["id"] = quote.Id.ToString(), ["horas"] = "24", ["version"] = Convert.ToBase64String(quote.Version)
        }));
        Assert.Equal(HttpStatusCode.Redirect, emitted.StatusCode);
        Assert.Equal(3.5m, (await service.StockAsync()).Single().Disponible);
        csrf = await Token(client, detailUrl);
        var sold = await client.PostAsync("/Documentos/Convertir", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf, ["id"] = quote.Id.ToString(), ["clave"] = Guid.NewGuid().ToString()
        }));
        Assert.Equal(HttpStatusCode.Redirect, sold.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(sold.Headers.Location)).StatusCode);
        Assert.Equal(3.5m, (await service.StockAsync()).Single().CantidadFisica);
        csrf = await Token(client, "/Inventario/Cargar");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(csrf), "__RequestVerificationToken");
        upload.Add(new StringContent(n.Bodega.ToString()), "BodegaId");
        upload.Add(new StringContent(Guid.NewGuid().ToString()), "ClaveOperacion");
        var excel = scope.ServiceProvider.GetRequiredService<IArchivoInventario>();
        upload.Add(new ByteArrayContent(excel.Exportar("Ingreso", ["SKU", "Cantidad"], [new object?[] { "TEST-01", 2m }])), "Archivo", "nuevo.xlsx");
        var loaded = await client.PostAsync("/Inventario/Cargar", upload);
        Assert.Equal(HttpStatusCode.Redirect, loaded.StatusCode);
        var import = (await service.ImportacionesAsync()).First();
        csrf = await Token(client, "/Inventario/Revisar/" + import.ImportacionInventarioId);
        var applied = await client.PostAsync("/Inventario/Aplicar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf, ["id"] = import.ImportacionInventarioId.ToString(), ["version"] = Convert.ToBase64String(import.Version)
        }));
        Assert.Equal(HttpStatusCode.Redirect, applied.StatusCode);
        Assert.Equal(5.5m, (await service.StockAsync()).Single().CantidadFisica);
    }
}
