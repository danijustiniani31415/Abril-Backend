using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure;

/// <summary>
/// Recordatorio de cierre: una cuadrilla ATS que lleva horas ABIERTA con al menos un trabajador firmado no puede
/// validarse (Capataz → Residente/Producción → SSOMA) hasta que su autor la cierre. Este servicio le avisa UNA vez
/// al autor (campanita + push) para que no quede olvidada. Queda registrado como evento «RecordatorioCierre».
/// Horas configurables en <c>Ats:RecordatorioCierreHoras</c> (por defecto 4).
/// </summary>
public class AtsCierreRecordatorioService : BackgroundService
{
    private const string Evento = "RecordatorioCierre";
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<AtsCierreRecordatorioService> _log;

    public AtsCierreRecordatorioService(IServiceScopeFactory scopes, IConfiguration config, ILogger<AtsCierreRecordatorioService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await RecordarAsync(ct); }
            catch (Exception ex) { _log.LogError(ex, "[ATS] Error en el recordatorio de cierre de cuadrillas"); }
            await Task.Delay(Intervalo, ct);
        }
    }

    private async Task RecordarAsync(CancellationToken ct)
    {
        var horas = double.TryParse(_config["Ats:RecordatorioCierreHoras"], out var h) && h > 0 ? h : 4;
        var limite = DateTime.UtcNow.AddHours(-horas);

        using var scope = _scopes.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var notificaciones = scope.ServiceProvider.GetRequiredService<INotificacionesService>();
        using var ctx = factory.CreateDbContext();

        var pendientes = await ctx.SsAtsGrupo.AsNoTracking()
            .Where(g => g.Estado == "Activo" && g.CreatedAt <= limite
                && ctx.SsAts.Any(a => a.AtsGrupoId == g.Id && a.Estado == "Firmado")
                && !ctx.SsAtsGrupoEvento.Any(e => e.AtsGrupoId == g.Id && e.Evento == Evento))
            .Select(g => new { g.Id, g.Codigo, g.Actividad, g.CreadoPorWorkerId })
            .Take(50)
            .ToListAsync(ct);

        foreach (var g in pendientes)
        {
            var email = await ctx.Worker.Where(w => w.Id == g.CreadoPorWorkerId).Select(w => w.EmailCorporativo).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    await notificaciones.CrearPorCorreosAsync(
                        "ATS_PENDIENTE_FIRMA",
                        [email],
                        origenUserId: null,
                        items: [new NuevaNotificacionDto
                        {
                            Titulo = "Cuadrilla ATS abierta: ¿ya se unieron todos?",
                            Subtitulo = g.Codigo ?? g.Actividad,
                            Descripcion = "Tu cuadrilla lleva horas abierta. Ciérrala para habilitar la validación (Capataz, Residente/Producción y SSOMA).",
                            Referencia = $"/ssoma/gestion/ats/grupo/{g.Id}",
                        }]);
                }
                catch (Exception ex) { _log.LogWarning(ex, "[ATS] No se pudo enviar el recordatorio de la cuadrilla {Id}", g.Id); }
            }

            // Se registra aunque no haya correo, para no reintentar cada 30 minutos.
            ctx.SsAtsGrupoEvento.Add(new SsAtsGrupoEvento { AtsGrupoId = g.Id, Evento = Evento, WorkerId = null, Detalle = "Recordatorio al autor para cerrar la cuadrilla", CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync(ct);
        }
    }
}
