using Application.DTO;

namespace Application.Interfaces;

public interface IComercialService
{
    Task<IReadOnlyList<ProductoFicha>> ProductosAsync();
    Task<IReadOnlyList<ClienteFicha>> ClientesAsync();
    Task<IReadOnlyList<BodegaFicha>> BodegasAsync();
    Task GuardarProductoAsync(ProductoFicha model, int usuario);
    Task GuardarClienteAsync(ClienteFicha model, int usuario);
    Task GuardarBodegaAsync(BodegaFicha model, int usuario);
    Task<int> CrearCotizacionAsync(DocumentoSolicitud model, int usuario);
    Task ActualizarCotizacionAsync(int id, DocumentoSolicitud model, byte[] version, int usuario);
    Task EmitirCotizacionAsync(int id, int horas, byte[] version, int usuario);
    Task CancelarCotizacionAsync(int id, byte[] version, int usuario);
    Task<int> CrearVentaAsync(DocumentoSolicitud model, int usuario);
    Task<int> VenderCotizacionAsync(int id, Guid clave, int usuario);
    Task<IReadOnlyList<DocumentoResumen>> DocumentosAsync(bool ventas, string? buscar = null);
    Task<DocumentoDetalle> DocumentoAsync(bool venta, int id);
    Task<IReadOnlyList<StockFila>> StockAsync(int? bodega = null, string? buscar = null);
    Task<IReadOnlyList<MovimientoFila>> MovimientosAsync(int? bodega, DateTime desde, DateTime hasta);
    Task<long> PrepararImportacionAsync(int bodega, string archivo, byte[] contenido, string? referencia, Guid clave, int usuario);
    Task AplicarImportacionAsync(long id, byte[] version, int usuario, bool confirmarDuplicado = false);
    Task CancelarImportacionAsync(long id, int usuario);
    Task<IReadOnlyList<ImportacionFila>> ImportacionesAsync();
    Task<ImportacionDetalle> ImportacionAsync(long id);
    Task<IReadOnlyList<DocumentoResumen>> ReporteVentasAsync(DateTime desde, DateTime hasta, int? bodega);
    Task ExpirarReservasAsync();
}
public interface IArchivoInventario
{
    IReadOnlyList<FilaExcel> Leer(byte[] contenido);
    byte[] Plantilla();
    byte[] Exportar(string hoja, string[] columnas, IEnumerable<object?[]> filas);
}
