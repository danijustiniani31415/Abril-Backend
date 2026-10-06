using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure;

/// <summary>
/// Cada 6 horas revisa la vigencia de las inducciones de los proyectos con sesiones cerradas y le manda a SSOMA UN
/// resumen por proyecto y por semana (campanita) con lo que está vencido, por vencer o con documentos por actualizar.
/// El «una vez por semana» lo garantiza la tabla ss_ind_alerta, así que reiniciar el servidor no repite avisos.
/// </summary>
public class InduccionAlertaService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<InduccionAlertaService> _log;

    public InduccionAlertaService(IServiceScopeFactory scopes, ILogger<InduccionAlertaService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IInduccionVigenciaService>().EnviarAlertasAsync(ct);
            }
            catch (Exception ex) { _log.LogError(ex, "[Inducción] Error en el resumen semanal de vigencia"); }
            await Task.Delay(Intervalo, ct);
        }
    }
}
