using System.ComponentModel.DataAnnotations;
using Application.DTO;

namespace Web.Models;

public sealed class CatalogoViewModel
{
    public string Tipo { get; set; } = "Productos";
    public IReadOnlyList<ProductoFicha> Productos { get; set; } = [];
    public IReadOnlyList<ClienteFicha> Clientes { get; set; } = [];
    public IReadOnlyList<BodegaFicha> Bodegas { get; set; } = [];
    public ProductoFicha Producto { get; set; } = new();
    public ClienteFicha Cliente { get; set; } = new();
    public BodegaFicha Bodega { get; set; } = new();
}
public sealed class DocumentoForm
{
    public DocumentoSolicitud Documento { get; set; } = new();
    public int Id { get; set; }
    public byte[] Version { get; set; } = [];
    public bool Venta { get; set; }
    public IReadOnlyList<ProductoFicha> Productos { get; set; } = [];
    public IReadOnlyList<ClienteFicha> Clientes { get; set; } = [];
    public IReadOnlyList<BodegaFicha> Bodegas { get; set; } = [];
}
public sealed record DocumentosViewModel(bool Ventas, IReadOnlyList<DocumentoResumen> Documentos, string? Buscar);
public sealed record DocumentoViewModel(bool Venta, DocumentoDetalle Documento, int HorasReserva);
public sealed record InventarioViewModel(IReadOnlyList<StockFila> Filas, IReadOnlyList<BodegaFicha> Bodegas, int? Bodega, string? Buscar);
public sealed class CargaInventarioForm
{
    [Range(1, int.MaxValue)] public int BodegaId { get; set; }
    [Required] public IFormFile? Archivo { get; set; }
    [StringLength(100)] public string? ReferenciaRecepcion { get; set; }
    public Guid ClaveOperacion { get; set; } = Guid.NewGuid();
    public IReadOnlyList<BodegaFicha> Bodegas { get; set; } = [];
}
public sealed record ReportesViewModel(DateTime Desde, DateTime Hasta, int? Bodega,
    IReadOnlyList<BodegaFicha> Bodegas, IReadOnlyList<DocumentoResumen> Ventas, IReadOnlyList<MovimientoFila> Movimientos);
