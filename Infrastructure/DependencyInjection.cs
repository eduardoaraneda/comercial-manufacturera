using Application.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Repositorios;
using Infrastructure.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configura ConnectionStrings:DefaultConnection.");
        services.AddDbContext<ApplicationDBContext>(options => options.UseSqlServer(connection));
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<ApplicationDBContext>()
        .AddClaimsPrincipalFactory<UsuarioClaimsFactory>()
        .AddErrorDescriber<IdentityErroresEspanol>()
        .AddDefaultTokenProviders();

        services.AddScoped<IServicioAcceso, ServicioAcceso>();
        services.AddScoped<IRepositorioInicio, RepositorioInicio>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IComercialService, ComercialService>();
        services.AddSingleton<IArchivoInventario, ArchivoInventario>();
        return services;
    }
}
