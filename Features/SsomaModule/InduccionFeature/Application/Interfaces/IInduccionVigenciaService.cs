using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;

public interface IInduccionVigenciaService
{
    /// <summary>Estado de cada trabajador activo del proyecto: vigente, por vencer, vencida, sin inducción o con
    /// documentos actualizados por recibir.</summary>
    Task<IndCumplimientoDto> GetCumplimiento(int proyectoId);

    Task SetGate(int proyectoId, bool activo, int userId);
    Task CrearExencion(IndExencionCrearDto dto, int userId);
    Task RevocarExencion(int exencionId, int userId);

    /// <summary>Gancho para el ATS: si el proyecto tiene el bloqueo ENCENDIDO y el trabajador no tiene inducción al
    /// día (ni exención), lanza AbrilException 403 con el motivo. Con el bloqueo apagado no hace nada.</summary>
    Task ExigirParaAts(int workerId, int proyectoId);

    Task<IndExpedienteDto> GetExpediente(int workerId);

    /// <summary>Resumen semanal a SSOMA de las inducciones por renovar (lo llama el segundo plano).</summary>
    Task EnviarAlertasAsync(CancellationToken ct = default);
}
