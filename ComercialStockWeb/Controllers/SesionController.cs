using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SesionController(IOptionsMonitor<CookieAuthenticationOptions> cookies) : Controller
{
    // Devuelve solo fechas, nunca el ticket, contrasena ni datos de identidad.
    // AllowAnonymous permite responder 401 JSON en lugar de redirigir el fetch.
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Estado()
    {
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded || result.Properties?.ExpiresUtc is not { } expiresAt)
            return Unauthorized(new { sesionActiva = false });

        var clock = cookies.Get(IdentityConstants.ApplicationScheme).TimeProvider ?? TimeProvider.System;
        var serverNow = clock.GetUtcNow();
        if (expiresAt <= serverNow) return Unauthorized(new { sesionActiva = false });
        return Json(new { sesionActiva = true, expiresAt, serverNow });
    }
}
