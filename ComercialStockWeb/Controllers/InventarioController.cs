using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[Authorize(Roles = "Administrador,Vendedor,Bodeguero")]
public sealed class InventarioController(IComercialService service, IArchivoInventario excel) : Controller
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private int UsuarioId => int.Parse(User.FindFirst("UsuarioId")!.Value, System.Globalization.CultureInfo.InvariantCulture);
    [HttpGet] public async Task<IActionResult> Index(int? bodega, string? buscar) => View(new InventarioViewModel(await service.StockAsync(bodega, buscar), await service.BodegasAsync(), bodega, buscar));
    [HttpGet] public async Task<IActionResult> Exportar(int? bodega, string? buscar)
    {
        var stock = await service.StockAsync(bodega, buscar);
        return File(excel.Exportar("Existencias", ["Bodega", "SKU", "Producto", "Unidad", "Físico", "Reservado", "Disponible"], stock.Select(x => new object?[] { x.Bodega, x.SKU, x.Producto, x.UnidadMedida, x.CantidadFisica, x.Reservada, x.Disponible })), Xlsx, "inventario.xlsx");
    }
    [HttpGet, Authorize(Roles = "Administrador,Bodeguero")]
    public async Task<IActionResult> Importaciones() => View(await service.ImportacionesAsync());
    [HttpGet, Authorize(Roles = "Administrador,Bodeguero")]
    public async Task<IActionResult> Cargar() => View(new CargaInventarioForm { Bodegas = (await service.BodegasAsync()).Where(x => x.Activa).ToList() });
    [HttpPost, Authorize(Roles = "Administrador,Bodeguero"), RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Cargar(CargaInventarioForm form)
    {
        if (form.Archivo is { Length: > 5_000_000 }) ModelState.AddModelError(nameof(form.Archivo), "Máximo 5 MB.");
        if (!ModelState.IsValid)
        {
            form.Bodegas = (await service.BodegasAsync()).Where(x => x.Activa).ToList(); return View(form);
        }
        using var stream = new MemoryStream();
        await form.Archivo!.CopyToAsync(stream);
        var id = await service.PrepararImportacionAsync(form.BodegaId, Path.GetFileName(form.Archivo.FileName), stream.ToArray(), form.ReferenciaRecepcion, form.ClaveOperacion, UsuarioId);
        return RedirectToAction(nameof(Revisar), new { id });
    }
    [HttpGet, Authorize(Roles = "Administrador,Bodeguero")]
    public IActionResult Plantilla() => File(excel.Plantilla(), Xlsx, "plantilla-ingreso.xlsx");
    [HttpGet, Authorize(Roles = "Administrador,Bodeguero")]
    public async Task<IActionResult> Revisar(long id) => View(await service.ImportacionAsync(id));
    [HttpPost, Authorize(Roles = "Administrador,Bodeguero")]
    public async Task<IActionResult> Aplicar(long id, byte[] version, bool confirmarDuplicado = false)
    {
        var importacion = await service.ImportacionAsync(id);
        if (importacion.Cabecera.ArchivoRepetido && !confirmarDuplicado)
            throw new Domain.ReglaNegocioException("Este archivo ya fue aplicado en esta bodega. Confirma expresamente que corresponde a otra recepción.");
        await service.AplicarImportacionAsync(id, version, UsuarioId, confirmarDuplicado);
        TempData["Success"] = "Ingreso aplicado. Las existencias y movimientos quedaron registrados.";
        return RedirectToAction(nameof(Revisar), new { id });
    }
    [HttpPost, Authorize(Roles = "Administrador,Bodeguero")]
    public async Task<IActionResult> Cancelar(long id)
    { await service.CancelarImportacionAsync(id, UsuarioId); return RedirectToAction(nameof(Revisar), new { id }); }
}
