using System.Security.Claims;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Servicios;

public sealed class UsuarioClaimsFactory(
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    IOptions<IdentityOptions> options,
    ApplicationDBContext db) : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var perfil = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.IdentidadId == user.Id);
        if (perfil is not null)
        {
            identity.AddClaim(new Claim("UsuarioId", perfil.UsuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            identity.AddClaim(new Claim("NombreCompleto", perfil.Nombre));
        }
        return identity;
    }
}
