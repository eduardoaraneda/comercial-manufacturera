using Domain.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("dbo");

        // Evita claves compuestas superiores al limite de SQL Server.
        builder.Entity<IdentityUser>().Property(x => x.Id).HasMaxLength(128);
        builder.Entity<IdentityRole>().Property(x => x.Id).HasMaxLength(128);
        builder.Entity<IdentityUserClaim<string>>().Property(x => x.UserId).HasMaxLength(128);
        builder.Entity<IdentityRoleClaim<string>>().Property(x => x.RoleId).HasMaxLength(128);
        builder.Entity<IdentityUserRole<string>>().Property(x => x.UserId).HasMaxLength(128);
        builder.Entity<IdentityUserRole<string>>().Property(x => x.RoleId).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.UserId).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.ProviderKey).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.UserId).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.Name).HasMaxLength(128);

        builder.Entity<Usuario>(entity =>
        {
            // Esta tabla ya se creo con los scripts de negocio; Identity no la recrea.
            entity.ToTable("Usuario", "dbo", table => table.ExcludeFromMigrations());
            entity.HasKey(x => x.UsuarioId);
            entity.Property(x => x.IdentidadId).HasMaxLength(450).IsRequired();
            entity.HasIndex(x => x.IdentidadId).IsUnique();
            entity.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(x => x.CreadoEn).HasColumnType("datetime2(3)");
        });
    }
}
