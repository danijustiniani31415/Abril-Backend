using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;

public interface IInduccionService
{
    // ── Catálogos (Coordinador SSOMA) ───────────────────────────────────────
    Task<List<IndDocumentoDto>> ListarDocumentos(bool soloActivos);
    Task<IndDocumentoDto> GuardarDocumento(IndDocumentoGuardarDto dto);
    Task<IndDocumentoVersionDto> SubirVersionDocumento(int documentoId, Stream archivo, string nombreArchivo, bool esCambioMaterial, string? nota, int userId);
    Task<List<IndDocumentoVersionDto>> ListarVersiones(int documentoId);

    Task<List<IndTemaDto>> ListarTemas(bool soloActivos);
    Task<IndTemaDto> GuardarTema(IndTemaGuardarDto dto);

    Task<List<IndPreguntaDto>> ListarPreguntas(bool soloActivas);
    Task<IndPreguntaDto> GuardarPregunta(IndPreguntaGuardarDto dto);
    Task<IndEvaluacionVersionDto> PublicarEvaluacion(IndEvaluacionPublicarDto dto, int userId);
    Task<List<IndEvaluacionVersionDto>> ListarEvaluaciones();

    // ── Sesión (instructor) ─────────────────────────────────────────────────
    Task<List<IndWorkerOpcionDto>> GetWorkersProyecto(int proyectoId);
    Task<IndSesionDetalleDto> CrearSesion(int userId, IndSesionCrearDto dto, string? ip);
    Task<IndSesionDetalleDto> GetSesion(int id);
    Task<IndSesionListaDto> ListarSesiones(IndSesionFiltroDto filtro);
    Task CerrarSesion(int id, int userId, IndCerrarSesionDto body, string? ip);
    Task AnularSesion(int id, int userId, string motivo, string? ip);
    Task<IndSelloDto> ReintentarSello(int sesionId);
    Task<IndRespaldoDto> ReintentarRespaldo(int sesionId);
    Task<(byte[] Bytes, string Nombre)> DescargarSello(int sesionId);
    Task ValidarAsistencia(int sesionId, int asistenciaId, int userId, string? ip);
    Task RechazarAsistencia(int sesionId, int asistenciaId, int userId, string motivo, string? ip);

    // ── Público (sin login: el token del QR es el candado) ──────────────────
    Task<IndPublicoResumenDto> GetResumenPublico(Guid token);
    Task<List<IndWorkerOpcionDto>> GetTrabajadoresPublico(Guid token);
    Task<IndEstadoTrabajadorDto> GetEstadoPublico(Guid token, IndIdentidadDto body);
    Task RegistrarDocumentoRecibido(Guid token, IndDocumentoRecibidoDto body);
    Task<IndEvaluacionPublicaDto> GetEvaluacionPublica(Guid token, IndIdentidadDto body);
    Task<IndResultadoEvaluacionDto> ResponderEvaluacion(Guid token, IndResponderEvaluacionDto body);
    Task Firmar(Guid token, IndFirmarDto body, string? ip, string? userAgent);

    /// <summary>Verificación pública del acta (QR). <paramref name="hash"/> es el prefijo del hash del acta.</summary>
    Task<IndVerificacionDto> Verificar(int sesionId, string? hash);
}
