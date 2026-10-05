using Abril_Backend.Features.SsomaModule.OptFeature.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure;

/// <summary>
/// Elimina las OPT que quedaron en borrador y nunca se finalizaron, para que no
/// ensucien la lista. Corre al arrancar y luego cada hora.
/// </summary>
public class OptBorradoresPurgeService : BackgroundService
{
    // Un borrador sobrevive como máximo este tiempo desde su creación.
    private static readonly TimeSpan Vigencia = TimeSpan.FromDays(2);
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    private readonly IServiceProvider _services;
    private readonly ILogger<OptBorradoresPurgeService> _log;

    public OptBorradoresPurgeService(IServiceProvider services, ILogger<OptBorradoresPurgeService> log)
    {
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IOptRepository>();
                var n = await repo.PurgarBorradoresAsync(DateTime.UtcNow - Vigencia);
                if (n > 0) _log.LogInformation("[OPT] {N} borrador(es) vencido(s) eliminado(s).", n);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[OPT] Error limpiando borradores vencidos.");
            }

            await Task.Delay(Intervalo, ct);
        }
    }
}
