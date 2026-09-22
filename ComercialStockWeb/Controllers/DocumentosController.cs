using Application.DTO;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[Authorize(Roles = "Administrador,Vendedor")]
public sealed class DocumentosController(IComercialService service, IConfiguration configuration) : Controller
{
    private int UsuarioId => int.Parse(User.FindFirst("UsuarioId")!.Value, System.Globalization.CultureInfo.InvariantCulture);
    private int Horas => Math.Clamp(configuration.GetValue("Comercial:HorasReserva", 24), 1, 720);

    [HttpGet] public async Task<IActionResult> Cotizaciones(string? buscar) => View("Index", new DocumentosViewModel(false, await service.DocumentosAsync(false, buscar), buscar));
    [HttpGet] public async Task<IActionResult> Ventas(string? buscar) => View("Index", new DocumentosViewModel(true, await service.DocumentosAsync(true, buscar), buscar));
    [HttpGet] public async Task<IActionResult> Detalle(int id, bool venta = false) => View(new DocumentoViewModel(venta, await service.DocumentoAsync(venta, id), Horas));

    private async Task<DocumentoForm> Preparar(DocumentoForm form)
    {
        form.Productos = (await service.ProductosAsync()).Where(x => x.Activo).ToList();
        form.Clientes = (await service.ClientesAsync()).Where(x => x.Activo).ToList();
        form.Bodegas = (await service.BodegasAsync()).Where(x => x.Activa).ToList();
        return form;
    }
    [HttpGet] public async Task<IActionResult> Crear(bool venta = false, int? copiar = null)
    {
        var form = new DocumentoForm { Venta = venta };
        form.Documento.HorasReserva = Horas;
        if (copiar.HasValue)
        {
            var old = await service.DocumentoAsync(false, copiar.Value);
            form.Documento.ClienteId = old.Cabecera.ClienteId; form.Documento.BodegaId = old.Cabecera.BodegaId;
            form.Documento.Observaciones = old.Cabecera.Observaciones;
            form.Documento.Lineas = old.Lineas.Select(x => new LineaSolicitud { ProductoId = x.ProductoId, Cantidad = x.Cantidad, PrecioUnitario = x.PrecioUnitario, DescuentoUnitario = x.DescuentoUnitario, ImpuestoPorcentaje = x.TasaImpuesto * 100 }).ToList();
        }
        return View("Formulario", await Preparar(form));
    }
    [HttpPost] public async Task<IActionResult> Crear(DocumentoForm form)
    {
        if (!ModelState.IsValid) return View("Formulario", await Preparar(form));
        int id;
        try { id = form.Venta ? await service.CrearVentaAsync(form.Documento, UsuarioId) : await service.CrearCotizacionAsync(form.Documento, UsuarioId); }
        catch (Domain.ReglaNegocioException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Formulario", await Preparar(form));
        }
        TempData["Success"] = form.Venta ? "Venta confirmada y stock descontado." : "Borrador guardado. Emítelo para reservar stock.";
        return RedirectToAction(nameof(Detalle), new { id, venta = form.Venta });
    }
    [HttpGet] public async Task<IActionResult> Editar(int id)
    {
        var doc = await service.DocumentoAsync(false, id);
        if (doc.Cabecera.Estado != "Borrador") return RedirectToAction(nameof(Detalle), new { id });
        return View("Formulario", await Preparar(new DocumentoForm
        {
            Id = id, Version = doc.Cabecera.Version,
            Documento = new DocumentoSolicitud { ClienteId = doc.Cabecera.ClienteId, BodegaId = doc.Cabecera.BodegaId, Observaciones = doc.Cabecera.Observaciones,
                Lineas = doc.Lineas.Select(x => new LineaSolicitud { ProductoId = x.ProductoId, Cantidad = x.Cantidad, PrecioUnitario = x.PrecioUnitario, DescuentoUnitario = x.DescuentoUnitario, ImpuestoPorcentaje = x.TasaImpuesto * 100 }).ToList() }
        }));
    }
    [HttpPost] public async Task<IActionResult> Editar(DocumentoForm form)
    {
        form.Venta = false;
        if (!ModelState.IsValid) return View("Formulario", await Preparar(form));
        await service.ActualizarCotizacionAsync(form.Id, form.Documento, form.Version, UsuarioId);
        TempData["Success"] = "Borrador actualizado."; return RedirectToAction(nameof(Detalle), new { id = form.Id });
    }
    [HttpPost] public async Task<IActionResult> Emitir(int id, int horas, byte[] version)
    {
        try { await service.EmitirCotizacionAsync(id, horas, version, UsuarioId); }
        catch (Domain.ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detalle), new { id });
        }
        TempData["Success"] = "Cotización emitida y stock reservado."; return RedirectToAction(nameof(Detalle), new { id });
    }
    [HttpPost] public async Task<IActionResult> Cancelar(int id, byte[] version)
    {
        await service.CancelarCotizacionAsync(id, version, UsuarioId);
        TempData["Success"] = "Cotización cancelada. Sus reservas fueron liberadas."; return RedirectToAction(nameof(Detalle), new { id });
    }
    [HttpPost] public async Task<IActionResult> Convertir(int id, Guid clave)
    {
        int venta;
        try { venta = await service.VenderCotizacionAsync(id, clave, UsuarioId); }
        catch (Domain.ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detalle), new { id });
        }
        TempData["Success"] = "Venta confirmada. El inventario fue actualizado."; return RedirectToAction(nameof(Detalle), new { id = venta, venta = true });
    }
}
