using System.Data.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HomeController(IRepositorioInicio inicio, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try { return View(new HomeViewModel(await inicio.ObtenerResumenAsync(cancellationToken), true)); }
        catch (DbException ex)
        {
            logger.LogError(ex, "No se pudo cargar el resumen comercial.");
            return View(new HomeViewModel(null, false));
        }
    }
}
