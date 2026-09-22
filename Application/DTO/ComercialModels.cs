using System.ComponentModel.DataAnnotations;

namespace Application.DTO;

public sealed class ProductoFicha
{
    public int ProductoId { get; set; }
    [Required, StringLength(50)] public string SKU { get; set; } = "";
    [Required, StringLength(200)] public string Nombre { get; set; } = "";
    [StringLength(1000)] public string? Descripcion { get; set; }
    [Required, StringLength(10)] public string UnidadMedida { get; set; } = "UN";
    [Range(typeof(decimal), "0", "1000000000", ParseLimitsInInvariantCulture = true)] public decimal PrecioReferencia { get; set; }
    public bool Activo { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class ClienteFicha
{
    public int ClienteId { get; set; }
    [StringLength(30)] public string? IdentificadorFiscal { get; set; }
    [Required, StringLength(200)] public string NombreRazonSocial { get; set; } = "";
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(300)] public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class BodegaFicha
{
    public int BodegaId { get; set; }
    [Required, StringLength(20)] public string Codigo { get; set; } = "";
    [Required, StringLength(150)] public string Nombre { get; set; } = "";
    [StringLength(300)] public string? Direccion { get; set; }
    public bool Activa { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class LineaSolicitud
{
    [Range(1, int.MaxValue)] public int ProductoId { get; set; }
    [Range(typeof(decimal), "0.001", "1000000", ParseLimitsInInvariantCulture = true)] public decimal Cantidad { get; set; } = 1;
    [Range(typeof(decimal), "0", "1000000000", ParseLimitsInInvariantCulture = true)] public decimal PrecioUnitario { get; set; }
    [Range(typeof(decimal), "0", "1000000000", ParseLimitsInInvariantCulture = true)] public decimal DescuentoUnitario { get; set; }
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)] public decimal ImpuestoPorcentaje { get; set; } = 19;
}
public sealed class DocumentoSolicitud
{
    public Guid ClaveOperacion { get; set; } = Guid.NewGuid();
    [Range(1, int.MaxValue)] public int ClienteId { get; set; }
    [Range(1, int.MaxValue)] public int BodegaId { get; set; }
    [Range(1, 720)] public int HorasReserva { get; set; } = 24;
    [StringLength(1000)] public string? Observaciones { get; set; }
    public List<LineaSolicitud> Lineas { get; set; } = [new()];
}
public sealed class DocumentoResumen
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public int ClienteId { get; set; }
    public string Cliente { get; set; } = "";
    public int BodegaId { get; set; }
    public string Bodega { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTime Fecha { get; set; }
    public DateTime? VenceEn { get; set; }
    public decimal Neto { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Total => Neto + Impuesto;
    public string? Observaciones { get; set; }
    public byte[] Version { get; set; } = [];
    public int? CotizacionId { get; set; }
}
public sealed class LineaDocumento
{
    public long Id { get; set; }
    public int ProductoId { get; set; }
    public string SKU { get; set; } = "";
    public string DescripcionProducto { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal DescuentoUnitario { get; set; }
    public decimal TasaImpuesto { get; set; }
    public decimal Neto => decimal.Round(Cantidad * (PrecioUnitario - DescuentoUnitario), 0, MidpointRounding.AwayFromZero);
    public decimal Impuesto => decimal.Round(Neto * TasaImpuesto, 0, MidpointRounding.AwayFromZero);
    public decimal Total => Neto + Impuesto;
}
public sealed record DocumentoDetalle(DocumentoResumen Cabecera, IReadOnlyList<LineaDocumento> Lineas);
public sealed class StockFila
{
    public int BodegaId { get; set; }
    public string Bodega { get; set; } = "";
    public int ProductoId { get; set; }
    public string SKU { get; set; } = "";
    public string Producto { get; set; } = "";
    public string UnidadMedida { get; set; } = "";
    public decimal CantidadFisica { get; set; }
    public decimal Reservada { get; set; }
    public decimal Disponible => CantidadFisica - Reservada;
}
public sealed class MovimientoFila
{
    public long MovimientoStockId { get; set; }
    public DateTime CreadoEn { get; set; }
    public string Bodega { get; set; } = "";
    public string SKU { get; set; } = "";
    public string Producto { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal Cantidad { get; set; }
    public string Usuario { get; set; } = "";
    public string Origen { get; set; } = "";
}
public sealed class ImportacionFila
{
    public long ImportacionInventarioId { get; set; }
    public int BodegaId { get; set; }
    public string Bodega { get; set; } = "";
    public string NombreArchivo { get; set; } = "";
    public string HashArchivo { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? ReferenciaRecepcion { get; set; }
    public DateTime CreadaEn { get; set; }
    public DateTime? AplicadaEn { get; set; }
    public byte[] Version { get; set; } = [];
    public bool ArchivoRepetido { get; set; }
}
public sealed class FilaExcel
{
    public int NumeroFila { get; set; }
    public string SKUOriginal { get; set; } = "";
    public string CantidadOriginal { get; set; } = "";
    public int? ProductoId { get; set; }
    public decimal? Cantidad { get; set; }
    public string? ErrorValidacion { get; set; }
}
public sealed record ImportacionDetalle(ImportacionFila Cabecera, IReadOnlyList<FilaExcel> Lineas);
