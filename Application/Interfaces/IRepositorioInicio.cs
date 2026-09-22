using Application.DTO;

namespace Application.Interfaces;

public interface IRepositorioInicio
{
    Task<ResumenInicio> ObtenerResumenAsync(CancellationToken cancellationToken = default);
}
