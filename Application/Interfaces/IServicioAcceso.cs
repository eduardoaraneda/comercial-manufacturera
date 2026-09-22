using Application.DTO;

namespace Application.Interfaces;

public interface IServicioAcceso
{
    Task<AccesoResultado> IniciarSesionAsync(string email, string password, bool recordar);
    Task CerrarSesionAsync();
    Task<bool> TieneUsuariosAsync(CancellationToken cancellationToken = default);
    Task<AccesoResultado> CrearAdministradorInicialAsync(string nombre, string email, string password);
    Task<bool> PerfilActivoAsync(string identidadId, CancellationToken cancellationToken = default);
}
