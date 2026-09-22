using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Web.Options;

namespace ComercialStock.Tests;

// Pruebas reales contra SQL Server. Cada prueba usa y elimina su propia base temporal.
public sealed partial class AccesoTests : IAsyncLifetime
{
    private readonly string databaseName = "ComercialStock_Test_" + Guid.NewGuid().ToString("N");
    private TestApplication app = null!;
    private string masterConnection = string.Empty;
    private bool created;
    private const string Email = "admin@example.test";
    private const string Password = "Prueba-Segura-2026!"; // Solo base efimera de pruebas.

    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COMERCIALSTOCK_TEST_SQL")
            ?? "Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10");
        connection.InitialCatalog = "master";
        masterConnection = connection.ConnectionString;
        await using (var master = new SqlConnection(masterConnection))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", master);
            await command.ExecuteNonQueryAsync();
            created = true;
        }
        connection.InitialCatalog = databaseName;
        app = new TestApplication(connection.ConnectionString);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        await ExecuteBatches(db, db.Database.GenerateCreateScript());
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Database"), "*.sql").Order())
            await ExecuteBatches(db, await File.ReadAllTextAsync(file));
    }

    private static async Task ExecuteBatches(ApplicationDBContext db, string script)
    {
        await db.Database.OpenConnectionAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
        await db.Database.CloseConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
        if (!created) return;
        SqlConnection.ClearAllPools();
        // El nombre solo procede de este prefijo fijo y un GUID generado aqui.
        if (!Regex.IsMatch(databaseName, @"^ComercialStock_Test_[a-f0-9]{32}$")) throw new InvalidOperationException();
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();
        await using var command = new SqlCommand($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];", master);
        await command.ExecuteNonQueryAsync();
    }

    private HttpClient Client() => app.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> Token(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "El formulario debe incluir token antiforgery.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string password = Password, string? returnUrl = null, bool recordar = false)
    {
        var token = await Token(client, "/Login");
        return await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = Email, ["Password"] = password, ["ReturnUrl"] = returnUrl ?? "/", ["Recordar"] = recordar.ToString(),
            ["__RequestVerificationToken"] = token
        }));
    }

    private async Task Seed()
    {
        using var scope = app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IServicioAcceso>()
            .CrearAdministradorInicialAsync("Administrador de pruebas", Email, Password);
        Assert.True(result.Exitoso, result.Error);
    }

    [Fact]
    public async Task Configuracion_Login_Logout_ProtegenRutasYNoRedirigenFuera()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
        var csrf = await Token(client, "/Login/Configuracion");
        var setup = await client.PostAsync("/Login/Configuracion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Administrador de pruebas", ["Email"] = Email, ["Password"] = Password,
            ["ConfirmarPassword"] = Password, ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, setup.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Login/Configuracion")).StatusCode);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var identity = await db.Users.SingleAsync();
            var perfil = await db.Usuarios.SingleAsync();
            Assert.Equal(identity.Id, perfil.IdentidadId);
            Assert.NotEqual(Password, identity.PasswordHash);
            Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().IsInRoleAsync(identity, "Administrador"));
        }

        var login = await Login(client, returnUrl: "https://example.org/");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.DoesNotContain("example.org", login.Headers.Location!.ToString());
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains("__Host-ComercialStock.Auth=") && cookie.Contains("secure") && cookie.Contains("httponly"));
        var homeToken = await Token(client, "/");
        var logout = await client.PostAsync("/Login/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = homeToken }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task FormulariosSinAntiforgerySonRechazados()
    {
        using var client = Client();
        var response = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = Email, ["Password"] = Password }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await client.PostAsync("/Login/Configuracion", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CincoIntentosFallidosBloqueanLaCuenta()
    {
        await Seed();
        using var client = Client();
        for (var index = 0; index < 5; index++)
        {
            var response = await Login(client, "Incorrecta-2026!");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No pudimos iniciar", await response.Content.ReadAsStringAsync());
        }
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByEmailAsync(Email))!));
        Assert.Equal(HttpStatusCode.OK, (await Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task DesactivarPerfilRevocaSesionExistente()
    {
        await Seed();
        using var client = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            (await db.Usuarios.SingleAsync()).Activo = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(client)).StatusCode);
    }

    [Fact]
    public async Task ConfiguracionInicialSoloAceptaUnAdministradorInclusoEnParalelo()
    {
        using var first = app.Services.CreateScope();
        using var second = app.Services.CreateScope();
        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<IServicioAcceso>().CrearAdministradorInicialAsync("Primero", Email, Password),
            second.ServiceProvider.GetRequiredService<IServicioAcceso>().CrearAdministradorInicialAsync("Segundo", "otro@example.test", Password));
        Assert.Single(results, x => x.Exitoso);
        using var check = app.Services.CreateScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<ApplicationDBContext>().Users.CountAsync());
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<ApplicationDBContext>().Usuarios.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TicketVenceConActividadInclusoAlRecordarYRedirigeAlLogin(bool recordar)
    {
        await Seed();
        using var client = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(client, recordar: recordar)).StatusCode);
        var first = await client.GetFromJsonAsync<EstadoSesion>("/Sesion/Estado");
        Assert.NotNull(first);
        Assert.InRange((first.ExpiresAt - first.ServerNow).TotalSeconds, 119, 120);

        app.Clock.Advance(TimeSpan.FromSeconds(80));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        var active = await client.GetFromJsonAsync<EstadoSesion>("/Sesion/Estado");
        Assert.Equal(first.ExpiresAt, active!.ExpiresAt);

        app.Clock.Advance(TimeSpan.FromSeconds(41));
        var status = await client.GetAsync("/Sesion/Estado");
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Contains("no-store", status.Headers.CacheControl!.ToString());
        var expired = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
        Assert.Contains("sesionExpirada=true", expired.Headers.Location!.ToString());
        var loginPage = await client.GetStringAsync(expired.Headers.Location);
        Assert.Contains("ya no est", loginPage);

        using var ajax = new HttpRequestMessage(HttpMethod.Get, "/");
        ajax.Headers.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(ajax)).StatusCode);
    }

    private sealed record EstadoSesion(DateTimeOffset ExpiresAt, DateTimeOffset ServerNow);

    private sealed class RelojPrueba : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan time) => now += time;
    }

    private sealed class TestApplication(string connection) : WebApplicationFactory<Program>
    {
        public RelojPrueba Clock { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDBContext>>();
                services.AddDbContext<ApplicationDBContext>(options => options.UseSqlServer(connection));
                services.AddSingleton<IStartupFilter, LoopbackFilter>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.PostConfigure<SesionOptions>(options => options.DuracionMinutos = 2);
                services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = Clock);
                // Fuerza la validacion del sello para comprobar que no renueva el plazo.
                services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
            });
        }
    }

    private sealed class LoopbackFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) => { context.Connection.RemoteIpAddress = IPAddress.Loopback; return nextMiddleware(context); });
            next(app);
        };
    }
}
