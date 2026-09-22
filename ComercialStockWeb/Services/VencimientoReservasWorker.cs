using Application.Interfaces;

namespace Web.Services;

public sealed class VencimientoReservasWorker(IServiceScopeFactory scopes, ILogger<VencimientoReservasWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IComercialService>().ExpirarReservasAsync();
                }
                catch (Exception ex) { logger.LogError(ex, "No se pudieron cerrar las reservas vencidas; se reintentará."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
