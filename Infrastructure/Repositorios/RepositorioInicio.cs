using Application.DTO;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositorios;

public sealed class RepositorioInicio(ApplicationDBContext db) : IRepositorioInicio
{
    public async Task<ResumenInicio> ObtenerResumenAsync(CancellationToken cancellationToken = default)
    {
        var result = await db.Database.SqlQueryRaw<ResumenFila>("""
            SELECT
                (SELECT COUNT(*) FROM dbo.Producto WHERE Activo = 1) AS Productos,
                (SELECT COUNT(*) FROM dbo.Bodega WHERE Activa = 1) AS Bodegas,
                (SELECT COUNT(*) FROM dbo.Cotizacion) AS Cotizaciones,
                (SELECT COUNT(*) FROM dbo.Venta) AS Ventas
            """).SingleAsync(cancellationToken);
        return new(result.Productos, result.Bodegas, result.Cotizaciones, result.Ventas);
    }

    private sealed class ResumenFila
    {
        public int Productos { get; set; }
        public int Bodegas { get; set; }
        public int Cotizaciones { get; set; }
        public int Ventas { get; set; }
    }
}
