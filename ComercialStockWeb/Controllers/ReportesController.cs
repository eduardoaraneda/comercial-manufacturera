using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[Authorize(Roles = "Administrador,Vendedor,Bodeguero")]
public sealed class ReportesController(IComercialService service, IArchivoInventario excel) : Controller
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    [HttpGet] public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, int? bodega)
    {
        var start = desde ?? DateTime.UtcNow.Date.AddDays(-30); var end = hasta ?? DateTime.UtcNow.Date;
        var canSell = User.IsInRole("Administrador") || User.IsInRole("Vendedor");
        return View(new ReportesViewModel(start, end, bodega, await service.BodegasAsync(),
            canSell ? await service.ReporteVentasAsync(start, end, bodega) : [], await service.MovimientosAsync(bodega, start, end)));
    }
    [HttpGet, Authorize(Roles = "Administrador,Vendedor")]
    public async Task<IActionResult> Ventas(DateTime desde, DateTime hasta, int? bodega)
    {
        var rows = await service.ReporteVentasAsync(desde, hasta, bodega);
        return File(excel.Exportar("Ventas", ["Número", "Fecha UTC", "Cliente", "Bodega", "Neto CLP", "Impuesto CLP", "Total CLP"], rows.Select(x => new object?[] { x.Numero, x.Fecha, x.Cliente, x.Bodega, x.Neto, x.Impuesto, x.Total })), Xlsx, "reporte-ventas.xlsx");
    }
    [HttpGet] public async Task<IActionResult> Movimientos(DateTime desde, DateTime hasta, int? bodega)
    {
        var rows = await service.MovimientosAsync(bodega, desde, hasta);
        return File(excel.Exportar("Movimientos", ["Id", "Fecha UTC", "Bodega", "SKU", "Producto", "Tipo", "Cantidad", "Origen", "Usuario"], rows.Select(x => new object?[] { x.MovimientoStockId, x.CreadoEn, x.Bodega, x.SKU, x.Producto, x.Tipo, x.Cantidad, x.Origen, x.Usuario })), Xlsx, "reporte-movimientos.xlsx");
    }
}
