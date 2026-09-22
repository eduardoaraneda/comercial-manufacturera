using Application.DTO;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[Authorize(Roles = "Administrador,Vendedor,Bodeguero")]
public sealed class CatalogosController(IComercialService service) : Controller
{
    private int UsuarioId => int.Parse(User.FindFirst("UsuarioId")!.Value, System.Globalization.CultureInfo.InvariantCulture);
    [HttpGet] public async Task<IActionResult> Productos(int? id)
    {
        var items = await service.ProductosAsync();
        return View("Index", new CatalogoViewModel { Tipo = "Productos", Productos = items, Producto = items.SingleOrDefault(x => x.ProductoId == id) ?? new() });
    }
    [HttpPost, Authorize(Roles = "Administrador")]
    public async Task<IActionResult> GuardarProducto([Bind(Prefix = "Producto")] ProductoFicha model)
    {
        if (!ModelState.IsValid) return View("Index", new CatalogoViewModel { Tipo = "Productos", Productos = await service.ProductosAsync(), Producto = model });
        await service.GuardarProductoAsync(model, UsuarioId); TempData["Success"] = "Producto guardado."; return RedirectToAction(nameof(Productos));
    }
    [HttpGet, Authorize(Roles = "Administrador,Vendedor")] public async Task<IActionResult> Clientes(int? id)
    {
        var items = await service.ClientesAsync();
        return View("Index", new CatalogoViewModel { Tipo = "Clientes", Clientes = items, Cliente = items.SingleOrDefault(x => x.ClienteId == id) ?? new() });
    }
    [HttpPost, Authorize(Roles = "Administrador,Vendedor")]
    public async Task<IActionResult> GuardarCliente([Bind(Prefix = "Cliente")] ClienteFicha model)
    {
        if (!ModelState.IsValid) return View("Index", new CatalogoViewModel { Tipo = "Clientes", Clientes = await service.ClientesAsync(), Cliente = model });
        await service.GuardarClienteAsync(model, UsuarioId); TempData["Success"] = "Cliente guardado."; return RedirectToAction(nameof(Clientes));
    }
    [HttpGet, Authorize(Roles = "Administrador,Bodeguero")] public async Task<IActionResult> Bodegas(int? id)
    {
        var items = await service.BodegasAsync();
        return View("Index", new CatalogoViewModel { Tipo = "Bodegas", Bodegas = items, Bodega = items.SingleOrDefault(x => x.BodegaId == id) ?? new() });
    }
    [HttpPost, Authorize(Roles = "Administrador,Bodeguero")]
    public async Task<IActionResult> GuardarBodega([Bind(Prefix = "Bodega")] BodegaFicha model)
    {
        if (!ModelState.IsValid) return View("Index", new CatalogoViewModel { Tipo = "Bodegas", Bodegas = await service.BodegasAsync(), Bodega = model });
        await service.GuardarBodegaAsync(model, UsuarioId); TempData["Success"] = "Bodega guardada."; return RedirectToAction(nameof(Bodegas));
    }
}
