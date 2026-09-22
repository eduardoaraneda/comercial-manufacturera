using System.Data.Common;
using System.Net;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Web.Models;

namespace Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LoginController(
    IServicioAcceso acceso,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<LoginController> logger) : Controller
{
    private bool PermiteConfiguracionLocal => environment.IsDevelopment()
        && configuration.GetValue<bool>("Bootstrap:AllowLocalSetup")
        && HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Index(string? returnUrl, bool sesionExpirada = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        var model = new LoginViewModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            SesionExpirada = sesionExpirada
        };
        await PrepararVistaAsync(model);
        return View(model);
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Index(LoginViewModel model)
    {
        if (!ModelState.IsValid) { await PrepararVistaAsync(model); return View(model); }
        try
        {
            var result = await acceso.IniciarSesionAsync(model.Email, model.Password, model.Recordar);
            if (result.Exitoso)
                return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "No se pudo consultar el servicio de autenticación.");
            ModelState.AddModelError(string.Empty, "No pudimos conectar con el servicio. Inténtalo nuevamente en unos momentos.");
        }
        model.Password = string.Empty;
        await PrepararVistaAsync(model);
        return View(model);
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Configuracion()
    {
        if (!PermiteConfiguracionLocal || await acceso.TieneUsuariosAsync()) return NotFound();
        return View(new ConfiguracionViewModel());
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Configuracion(ConfiguracionViewModel model)
    {
        if (!PermiteConfiguracionLocal || await acceso.TieneUsuariosAsync()) return NotFound();
        if (!ModelState.IsValid) return View(model);
        var result = await acceso.CrearAdministradorInicialAsync(model.Nombre, model.Email, model.Password);
        if (!result.Exitoso)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
        TempData["Success"] = "Tu cuenta de administrador está lista. Ya puedes iniciar sesión.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await acceso.CerrarSesionAsync();
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccesoDenegado() { Response.StatusCode = 403; return View(); }

    [AllowAnonymous, IgnoreAntiforgeryToken]
    public IActionResult Error()
    {
        Response.StatusCode = 500;
        ViewData["TraceId"] = HttpContext.TraceIdentifier;
        return View();
    }

    private async Task PrepararVistaAsync(LoginViewModel model)
    {
        try
        {
            var hasUsers = await acceso.TieneUsuariosAsync();
            model.MostrarConfiguracion = PermiteConfiguracionLocal && !hasUsers;
            model.BaseDisponible = true;
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "La base de autenticación no está disponible.");
            model.BaseDisponible = false;
        }
    }
}
