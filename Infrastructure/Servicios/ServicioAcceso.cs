using Application.DTO;
using Application.Interfaces;
using Domain.Entidades;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Servicios;

public sealed class ServicioAcceso(
    ApplicationDBContext db,
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    SignInManager<IdentityUser> signIn) : IServicioAcceso
{
    private const string CredencialesInvalidas =
        "No pudimos iniciar sesión. Revisa tus credenciales o inténtalo más tarde.";

    public Task<bool> TieneUsuariosAsync(CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(cancellationToken);

    public Task<bool> PerfilActivoAsync(string identidadId, CancellationToken cancellationToken = default) =>
        db.Usuarios.AnyAsync(x => x.IdentidadId == identidadId && x.Activo, cancellationToken);

    public async Task<AccesoResultado> IniciarSesionAsync(string email, string password, bool recordar)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !await PerfilActivoAsync(user.Id))
            return new(false, CredencialesInvalidas);

        var result = await signIn.PasswordSignInAsync(user, password, recordar, lockoutOnFailure: true);
        return result.Succeeded ? new(true) : new(false, CredencialesInvalidas);
    }

    public Task CerrarSesionAsync() => signIn.SignOutAsync();

    public async Task<AccesoResultado> CrearAdministradorInicialAsync(string nombre, string email, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // El bloqueo pertenece a esta transaccion. Dos solicitudes no pueden crear
        // simultaneamente dos administradores iniciales, incluso en procesos distintos.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @resultado INT;
            EXEC @resultado = sys.sp_getapplock
                @Resource = N'ComercialStock.AdministradorInicial',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @resultado < 0 THROW 51000, N'No se pudo bloquear la configuracion inicial.', 1;
            """);

        if (await db.Users.AnyAsync())
            return new(false, "La configuración inicial ya fue completada.");

        var admin = new IdentityUser { UserName = email.Trim(), Email = email.Trim(), LockoutEnabled = true };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
            return new(false, string.Join(" ", created.Errors.Select(x => x.Description)));

        if (!await roles.RoleExistsAsync("Administrador"))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole("Administrador"));
            if (!roleResult.Succeeded)
                return new(false, "No se pudo crear el rol de administrador.");
        }

        var assigned = await users.AddToRoleAsync(admin, "Administrador");
        if (!assigned.Succeeded)
            return new(false, "No se pudo asignar el rol de administrador.");

        db.Usuarios.Add(new Usuario { IdentidadId = admin.Id, Nombre = nombre.Trim() });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true);
    }
}
