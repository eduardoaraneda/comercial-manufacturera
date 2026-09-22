using System.Security.Claims;
using System.Threading.RateLimiting;
using Application.Interfaces;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Web.Options;

var builder = WebApplication.CreateBuilder(args);
// El archivo local es opcional; las variables de entorno conservan prioridad.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add(new ResponseCacheAttribute { NoStore = true, Location = ResponseCacheLocation.None });
    options.Filters.Add<Web.Filters.OperacionExceptionFilter>();
});
builder.Services.AddHostedService<Web.Services.VencimientoReservasWorker>();
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-ComercialStock.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddOptions<SesionOptions>()
    .Bind(builder.Configuration.GetSection(SesionOptions.SectionName))
    .Validate(options => options.DuracionMinutos is >= 1 and <= 1440,
        "Autenticacion:DuracionMinutos debe estar entre 1 y 1440.")
    .ValidateOnStart();
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<IOptions<SesionOptions>>((options, sesion) =>
{
    options.Cookie.Name = "__Host-ComercialStock.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Login/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(sesion.Value.DuracionMinutos);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")
            || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(QueryHelpers.AddQueryString(context.RedirectUri, "sesionExpirada", "true"));
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        // Descarta tickets anteriores cuya duracion supera el nuevo limite.
        if (context.Properties.IssuedUtc is not { } issued
            || context.Properties.ExpiresUtc is not { } expires
            || expires > issued.Add(context.Options.ExpireTimeSpan))
        {
            context.RejectPrincipal();
            await context.HttpContext.RequestServices.GetRequiredService<IServicioAcceso>().CerrarSesionAsync();
            return;
        }
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        // Identity puede solicitar renovacion tras validar el sello de seguridad.
        // Conservamos el limite absoluto del ticket emitido en el login.
        context.ShouldRenew = false;
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is not null && !await context.HttpContext.RequestServices
                .GetRequiredService<IServicioAcceso>().PerfilActivoAsync(id, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.RequestServices.GetRequiredService<IServicioAcceso>().CerrarSesionAsync();
        }
    };
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});

var app = builder.Build();

// Exportar solo el DDL de Identity sin conectarse ni modificar la base.
if (args.Contains("--script-auth", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    Console.Write(scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().Database.GenerateCreateScript());
    return;
}

app.UseExceptionHandler("/Login/Error");
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US", "es-CL"),
    SupportedCultures = [System.Globalization.CultureInfo.GetCultureInfo("en-US")],
    SupportedUICultures = [System.Globalization.CultureInfo.GetCultureInfo("es-CL")],
    RequestCultureProviders = []
});
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
