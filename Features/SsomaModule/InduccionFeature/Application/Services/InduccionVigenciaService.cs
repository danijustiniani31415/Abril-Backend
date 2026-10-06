using System.Globalization;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Services;

/// <summary>
/// Vigencia de la inducción de cada trabajador, por proyecto. Reglas:
/// <list type="bullet">
/// <item>Base = su última inducción o re-inducción APROBADA en una sesión cerrada del proyecto. Vence a los
///   <c>Induccion:VigenciaMeses</c> (12 por defecto); «por vencer» = faltan 30 días o menos.</item>
/// <item>Re-aceptación: si desde su inducción se publicó una versión MATERIAL de un documento que recibió, o se
///   agregó un documento obligatorio nuevo, queda «ActualizacionPendiente» hasta que firme una sesión de
///   Actualización con esos documentos (sin evaluación).</item>
/// <item>Bloqueo del ATS: por proyecto, APAGADO por defecto. Encendido, solo firman ATS los habilitados
///   (inducción al día, con o sin aviso de vencimiento) o exentos.</item>
/// </list>
/// </summary>
public class InduccionVigenciaService : IInduccionVigenciaService
{
    private const int DiasAvisoVencimiento = 30;

    private readonly IInduccionVigenciaRepository _repository;
    private readonly IAtsRepository _atsRepository;
    private readonly INotificacionesService _notificaciones;
    private readonly IConfiguration _config;
    private readonly ILogger<InduccionVigenciaService> _log;

    public InduccionVigenciaService(
        IInduccionVigenciaRepository repository,
        IAtsRepository atsRepository,
        INotificacionesService notificaciones,
        IConfiguration config,
        ILogger<InduccionVigenciaService> log)
    {
        _repository = repository;
        _atsRepository = atsRepository;
        _notificaciones = notificaciones;
        _config = config;
        _log = log;
    }

    private int VigenciaMeses => int.TryParse(_config["Induccion:VigenciaMeses"], out var m) && m > 0 ? m : 12;

    // ════════════════════════════════════════════════════════════════════════
    // Cumplimiento del proyecto
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IndCumplimientoDto> GetCumplimiento(int proyectoId)
    {
        var nombre = await _repository.GetProyectoNombre(proyectoId) ?? throw new AbrilException("Proyecto no encontrado.", 404);
        var datos = await _repository.CargarDatos(proyectoId, null, conRoster: true);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var items = datos.Roster.Select(w => Evaluar(w, datos, hoy)).ToList();
        return new IndCumplimientoDto
        {
            ProyectoId = proyectoId,
            ProyectoNombre = nombre,
            GateActivo = await _repository.GateActivo(proyectoId),
            VigenciaMeses = VigenciaMeses,
            Items = items,
            Resumen = new IndCumplimientoResumenDto
            {
                Total = items.Count,
                Vigentes = items.Count(i => i.Estado == "Vigente"),
                PorVencer = items.Count(i => i.Estado == "PorVencer"),
                Vencidas = items.Count(i => i.Estado == "Vencida"),
                SinInduccion = items.Count(i => i.Estado == "SinInduccion"),
                ActualizacionPendiente = items.Count(i => i.Estado == "ActualizacionPendiente"),
                Exentos = items.Count(i => i.Exento),
            },
        };
    }

    /// <summary>Estado de UN trabajador. Función pura sobre los datos ya cargados.</summary>
    private IndCumplimientoItemDto Evaluar(VigRoster w, DatosVigencia datos, DateOnly hoy)
    {
        var item = new IndCumplimientoItemDto
        {
            WorkerId = w.WorkerId, Nombre = w.Nombre, DocumentoIdentidad = w.Dni, Categoria = w.Categoria,
        };

        var exencion = datos.Exenciones.FirstOrDefault(e => e.WorkerId == w.WorkerId);
        if (exencion != null)
        {
            item.Exento = true;
            item.ExencionId = exencion.Id;
            item.ExencionMotivo = exencion.Motivo;
            item.ExencionHasta = exencion.Hasta;
        }

        var registros = datos.Registros.Where(r => r.WorkerId == w.WorkerId).ToList();

        // Base: última inducción o re-inducción aprobada (una Actualización no renueva la vigencia).
        var baseReg = registros
            .Where(r => r.Tipo != "Actualizacion" && r.Aprobado)
            .OrderByDescending(r => r.Fecha).ThenByDescending(r => r.SesionId)
            .FirstOrDefault();

        if (baseReg == null)
        {
            item.Estado = "SinInduccion";
        }
        else
        {
            item.UltimaInduccionFecha = baseReg.Fecha;
            item.SesionId = baseReg.SesionId;
            item.SesionCodigo = baseReg.SesionCodigo;
            item.VigenteHasta = baseReg.Fecha.AddMonths(VigenciaMeses);

            if (item.VigenteHasta < hoy) item.Estado = "Vencida";
            else
            {
                item.Pendientes = Pendientes(baseReg, registros, datos.Catalogo);
                item.Estado = item.Pendientes.Count > 0 ? "ActualizacionPendiente"
                    : item.VigenteHasta <= hoy.AddDays(DiasAvisoVencimiento) ? "PorVencer"
                    : "Vigente";
            }
        }

        item.Habilitado = item.Exento || item.Estado is "Vigente" or "PorVencer";
        return item;
    }

    /// <summary>Documentos que debe volver a recibir: cambio MATERIAL desde la versión que recibió, o documento
    /// obligatorio nuevo publicado después de su inducción.</summary>
    private static List<IndPendienteDocDto> Pendientes(VigRegistro baseReg, List<VigRegistro> registros, List<VigDocumento> catalogo)
    {
        // Lo recibido desde su inducción base (incluye las actualizaciones posteriores).
        var recibidas = registros.Where(r => r.Fecha >= baseReg.Fecha).SelectMany(r => r.Docs)
            .GroupBy(d => d.DocumentoId).ToDictionary(g => g.Key, g => g.Max(d => d.Version));

        var pendientes = new List<IndPendienteDocDto>();
        foreach (var doc in catalogo)
        {
            var vigente = doc.Versiones.FirstOrDefault(v => v.Vigente);
            if (vigente == null) continue;

            if (!recibidas.TryGetValue(doc.DocumentoId, out var version))
            {
                // Nunca lo recibió: solo cuenta si es obligatorio y se publicó DESPUÉS de su inducción.
                if (doc.Obligatorio && DateOnly.FromDateTime(vigente.PublicadoEn.ToLocalTime()) > baseReg.Fecha)
                    pendientes.Add(new IndPendienteDocDto
                    {
                        DocumentoId = doc.DocumentoId, Codigo = doc.Codigo, Nombre = doc.Nombre,
                        VersionVigente = vigente.Version, VersionRecibida = null, Motivo = "Documento nuevo",
                    });
            }
            else if (vigente.Version > version && doc.Versiones.Any(v => v.Version > version && v.Version <= vigente.Version && v.Material))
            {
                pendientes.Add(new IndPendienteDocDto
                {
                    DocumentoId = doc.DocumentoId, Codigo = doc.Codigo, Nombre = doc.Nombre,
                    VersionVigente = vigente.Version, VersionRecibida = version, Motivo = "Cambio material",
                });
            }
        }
        return pendientes;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Bloqueo del ATS, exenciones
    // ════════════════════════════════════════════════════════════════════════

    public Task SetGate(int proyectoId, bool activo, int userId) => _repository.SetGate(proyectoId, activo, userId);

    public async Task CrearExencion(IndExencionCrearDto dto, int userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo)) throw new AbrilException("Indica el motivo de la exención.", 400);
        if (dto.Hasta is { } h && h < DateOnly.FromDateTime(DateTime.Today)) throw new AbrilException("La fecha de la exención ya pasó.", 400);
        await _repository.CrearExencion(dto, userId);
    }

    public Task RevocarExencion(int exencionId, int userId) => _repository.RevocarExencion(exencionId, userId);

    public async Task ExigirParaAts(int workerId, int proyectoId)
    {
        // Apagado (o sin tabla todavía, p. ej. si el SQL de la Fase 3 no se corrió): el ATS sigue como siempre. Un fallo
        // TÉCNICO al consultar el bloqueo nunca debe impedir firmar un ATS; el bloqueo es una regla, no una dependencia.
        bool gateActivo;
        try { gateActivo = await _repository.GateActivo(proyectoId); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Inducción] No se pudo consultar el bloqueo del proyecto {Id}; el ATS continúa sin bloqueo", proyectoId);
            return;
        }
        if (!gateActivo) return;

        var datos = await _repository.CargarDatos(proyectoId, [workerId], conRoster: false);
        var item = Evaluar(new VigRoster(workerId, string.Empty, null, null), datos, DateOnly.FromDateTime(DateTime.Today));
        if (item.Habilitado) return;

        var motivo = item.Estado switch
        {
            "Vencida" => $"Tu inducción de seguridad venció el {item.VigenteHasta:dd/MM/yyyy}. Necesitas una re-inducción antes de firmar un ATS.",
            "ActualizacionPendiente" => $"Hay documentos de seguridad actualizados que debes recibir antes de firmar un ATS: {string.Join(", ", item.Pendientes.Select(p => p.Codigo))}.",
            _ => "No tienes una inducción de seguridad vigente en este proyecto, así que todavía no puedes firmar un ATS.",
        };
        throw new AbrilException($"{motivo} Acércate al Coordinador SSOMA del proyecto.", 403);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Expediente
    // ════════════════════════════════════════════════════════════════════════

    public Task<IndExpedienteDto> GetExpediente(int workerId) => _repository.GetExpediente(workerId, VigenciaMeses);

    // ════════════════════════════════════════════════════════════════════════
    // Alertas semanales
    // ════════════════════════════════════════════════════════════════════════

    public async Task EnviarAlertasAsync(CancellationToken ct = default)
    {
        var periodo = PeriodoSemana(DateTime.Today);
        foreach (var proyectoId in await _repository.ProyectosConInducciones())
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var c = await GetCumplimiento(proyectoId);
                var r = c.Resumen;
                var hayQueRenovar = r.Vencidas + r.PorVencer + r.ActualizacionPendiente > 0;
                var hayBloqueados = c.GateActivo && r.SinInduccion > 0;
                if (!hayQueRenovar && !hayBloqueados) continue;

                var partes = new List<string>();
                if (r.Vencidas > 0) partes.Add($"{r.Vencidas} vencidas");
                if (r.PorVencer > 0) partes.Add($"{r.PorVencer} por vencer");
                if (r.ActualizacionPendiente > 0) partes.Add($"{r.ActualizacionPendiente} con documentos por actualizar");
                if (hayBloqueados) partes.Add($"{r.SinInduccion} sin inducción (no pueden firmar ATS)");
                var detalle = string.Join(" · ", partes);

                if (!await _repository.RegistrarAlerta(proyectoId, "ResumenSemanal", periodo, detalle)) continue; // ya se avisó esta semana

                var emails = (await _atsRepository.GetResponsables(proyectoId)).SsomaEmails;
                if (emails.Count == 0) continue;

                await _notificaciones.CrearPorCorreosAsync(
                    "INDUCCION_POR_RENOVAR",
                    emails,
                    origenUserId: null,
                    items: [new NuevaNotificacionDto
                    {
                        Titulo = $"Inducciones por renovar — {c.ProyectoNombre}",
                        Subtitulo = detalle,
                        Descripcion = "Revisa la pestaña Cumplimiento para programar la re-inducción o la actualización de documentos.",
                        Referencia = $"/ssoma/gestion/induccion?tab=cumplimiento&proyecto={proyectoId}",
                    }]);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[Inducción] No se pudo generar el resumen de vigencia del proyecto {Id}", proyectoId);
            }
        }
    }

    private static string PeriodoSemana(DateTime fecha)
        => $"{ISOWeek.GetYear(fecha)}-W{ISOWeek.GetWeekOfYear(fecha):D2}";
}
