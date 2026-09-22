using System.Security.Cryptography;
using Domain.Entidades;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Herramienta manual para la base local del portafolio. No se ejecuta al iniciar Web.
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ComercialStock;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
}).Build();
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddInfrastructure(configuration);
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
await using var transaction = await db.Database.BeginTransactionAsync();
await db.Database.ExecuteSqlRawAsync("""
    DECLARE @resultado INT;
    EXEC @resultado = sys.sp_getapplock
        @Resource = N'ComercialStock.AdministradorInicial',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
    IF @resultado < 0 THROW 51000, N'No se pudo bloquear la carga de ejemplo.', 1;
    """);

var catalogo = new (string Sku, string Nombre, string Descripcion, decimal Precio)[]
{
    ("MUE-001", "Escritorio de melamina 120 cm", "Escritorio de 120 x 60 cm, cubierta blanca y estructura metalica.", 79990m),
    ("MUE-002", "Escritorio de melamina 150 cm", "Escritorio de 150 x 70 cm, cubierta color roble.", 109990m),
    ("MUE-003", "Mesa de reuniones para 6 personas", "Mesa de 180 x 90 cm, cubierta de melamina y patas metalicas.", 189990m),
    ("MUE-004", "Silla de madera tapizada", "Silla de madera de pino con asiento tapizado gris.", 45990m),
    ("MUE-005", "Estante metalico de 5 niveles", "Estante de 180 x 90 x 40 cm, acabado negro.", 69990m),
    ("MUE-006", "Cajonera movil de 3 cajones", "Cajonera de melamina con ruedas y cierre centralizado.", 54990m),
    ("MUE-007", "Gabinete de almacenamiento", "Gabinete de dos puertas, 160 x 80 x 40 cm.", 129990m),
    ("MUE-008", "Mesa auxiliar cuadrada", "Mesa auxiliar de 50 x 50 cm en madera y metal.", 29990m),
    ("MUE-009", "Banco de trabajo industrial", "Banco de 150 x 70 cm con cubierta reforzada.", 249990m),
    ("MUE-010", "Repisa mural de 80 cm", "Repisa de melamina con soportes incluidos.", 15990m),
    ("MUE-011", "Panel divisorio de escritorio", "Panel de 120 x 40 cm con fijaciones.", 34990m),
    ("MUE-012", "Mostrador de atencion", "Mostrador de 120 cm con espacio de almacenamiento interior.", 169990m)
};

var productosCreados = 0;
foreach (var producto in catalogo)
{
    productosCreados += await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO dbo.Producto (SKU, Nombre, Descripcion, UnidadMedida, PrecioReferencia, Activo)
        SELECT {producto.Sku}, {producto.Nombre}, {producto.Descripcion}, 'UN', {producto.Precio}, CAST(1 AS bit)
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Producto WITH (UPDLOCK, HOLDLOCK) WHERE SKU = {producto.Sku});
        """);
}

var cuentas = new (string Email, string Nombre, string Rol)[]
{
    ("admin@comercialstock.test", "Administrador Demo", "Administrador"),
    ("ventas@comercialstock.test", "Vendedor Demo", "Vendedor"),
    ("bodega@comercialstock.test", "Encargado de Bodega Demo", "Bodeguero")
};
var creadas = new List<(IdentityUser User, string Password, string Rol)>();
var existentes = new List<string>();
foreach (var cuenta in cuentas)
{
    // No restablece contrasenas, modifica perfiles ni aumenta permisos de cuentas existentes.
    if (await users.FindByEmailAsync(cuenta.Email) is not null)
    {
        existentes.Add(cuenta.Email);
        continue;
    }
    if (!await roles.RoleExistsAsync(cuenta.Rol))
        Check(await roles.CreateAsync(new IdentityRole(cuenta.Rol)));

    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    var password = "Cs9!" + new string(Enumerable.Range(0, 12)
        .Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
    var identity = new IdentityUser { UserName = cuenta.Email, Email = cuenta.Email, LockoutEnabled = true };
    Check(await users.CreateAsync(identity, password));
    Check(await users.AddToRoleAsync(identity, cuenta.Rol));
    db.Usuarios.Add(new Usuario { IdentidadId = identity.Id, Nombre = cuenta.Nombre });
    creadas.Add((identity, password, cuenta.Rol));
}
await db.SaveChangesAsync();

// Verifica credenciales con el mismo hasher de Identity y su perfil activo antes de confirmar.
foreach (var cuenta in creadas)
{
    if (!await users.CheckPasswordAsync(cuenta.User, cuenta.Password)
        || !await users.IsInRoleAsync(cuenta.User, cuenta.Rol)
        || !await db.Usuarios.AnyAsync(x => x.IdentidadId == cuenta.User.Id && x.Activo))
        throw new InvalidOperationException("Fallo la validacion de una cuenta de ejemplo.");
}
await transaction.CommitAsync();
Console.WriteLine($"Productos agregados: {productosCreados}. Precios ficticios netos de referencia en CLP por unidad.");
Console.WriteLine($"Usuarios creados y verificados: {creadas.Count}.");
Console.WriteLine("Las contrasenas se muestran solo en esta ejecucion y no se guardan en archivos.");
foreach (var cuenta in creadas)
    Console.WriteLine($"{cuenta.User.Email} | {cuenta.Rol} | {cuenta.Password}");
foreach (var email in existentes)
    Console.WriteLine($"Sin cambios (ya existia): {email}");

static void Check(IdentityResult result)
{
    if (!result.Succeeded)
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
}
