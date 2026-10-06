using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure;

/// <summary>
/// Segundo plano de la Inducción digital: cada pocos minutos busca sesiones cerradas que todavía no tienen su sello
/// de tiempo o su respaldo y los completa. Así un corte de la TSA o de la cuenta de respaldo en el momento del cierre
/// no deja a la sesión sin evidencia: se reintenta solo (hasta 10 veces por pieza, y luego queda en «Error» visible
/// en el panel para reintentarlo a mano).
/// </summary>
public class InduccionPostCierreService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<InduccionPostCierreService> _log;

    public InduccionPostCierreService(IServiceScopeFactory scopes, ILogger<InduccionPostCierreService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await CicloAsync(ct); }
            catch (Exception ex) { _log.LogError(ex, "[Inducción] Error en el ciclo de sello/respaldo"); }
            await Task.Delay(Intervalo, ct);
        }
    }

    private async Task CicloAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IInduccionPostCierreRepository>();
        var sello = scope.ServiceProvider.GetRequiredService<IInduccionSelloService>();
        var respaldo = scope.ServiceProvider.GetRequiredService<IInduccionRespaldoService>();

        foreach (var id in await repo.SesionesPendientesSello(10))
        {
            if (ct.IsCancellationRequested) return;
            await sello.SellarAsync(id, ct);
        }

        if (!respaldo.Configurado) return;
        foreach (var id in await repo.SesionesPendientesRespaldo(3))
        {
            if (ct.IsCancellationRequested) return;
            await respaldo.RespaldarAsync(id, ct);
        }
    }
}
