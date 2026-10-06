using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;

public interface IInduccionRepository
{
    // ── Catálogo de documentos ──────────────────────────────────────────────
    Task<List<SsIndDocumento>> ListarDocumentos(bool soloActivos);
    Task<SsIndDocumento> GuardarDocumento(IndDocumentoGuardarDto dto);
    /// <summary>Marca como no vigente la versión anterior y crea la siguiente (max + 1). INMUTABLE después.</summary>
    Task<SsIndDocumentoVersion> PublicarVersion(int documentoId, string archivoUrl, string archivoHash, bool esCambioMaterial, string? nota, int? userId);
    Task<List<SsIndDocumentoVersion>> ListarVersiones(int documentoId);

    // ── Temario ─────────────────────────────────────────────────────────────
    Task<List<SsIndTema>> ListarTemas(bool soloActivos);
    Task<SsIndTema> GuardarTema(IndTemaGuardarDto dto);

    // ── Banco de preguntas y evaluación publicada ───────────────────────────
    Task<List<SsIndPregunta>> ListarPreguntas(bool soloActivas);
    Task<SsIndPregunta> GuardarPregunta(IndPreguntaGuardarDto dto);
    Task<SsIndEvaluacionVersion> PublicarEvaluacion(string contenidoJson, string contenidoHash, decimal notaMinima, decimal notaMaxima, int duracionMin, int? userId);
    Task<SsIndEvaluacionVersion?> GetEvaluacionVigente();
    Task<SsIndEvaluacionVersion?> GetEvaluacionVersion(int id);
    Task<List<SsIndEvaluacionVersion>> ListarEvaluaciones();

    // ── Sesión ──────────────────────────────────────────────────────────────
    Task<SsIndSesion> CrearSesion(SsIndSesion sesion, List<int> documentoVersionIds, List<SsIndTema> temas, List<int> workerIdsEsperados, int? userId, string? ip);
    /// <summary>Con documentos (y su versión), temas y asistencias. Sin seguimiento.</summary>
    Task<SsIndSesion?> GetSesion(int id);
    Task<SsIndSesion?> GetSesionPorToken(Guid token);
    Task<IndSesionListaDto> ListarSesiones(IndSesionFiltroDto filtro);
    Task<string?> GetProyectoNombre(int proyectoId);
    Task<List<IndWorkerOpcionDto>> GetWorkersParaInduccion(int proyectoId);

    // ── Trabajador ──────────────────────────────────────────────────────────
    Task<SsIndAsistencia?> GetAsistencia(int sesionId, int workerId);
    /// <summary>Crea la fila "Pendiente" con snapshot de identidad si el trabajador aún no estaba en la sesión.</summary>
    Task<SsIndAsistencia> GetOCrearAsistencia(int sesionId, int workerId);
    Task RegistrarDocumentoRecibido(int asistenciaId, int documentoVersionId, int? segundosAbierto);
    Task<List<int>> GetDocumentosRecibidos(int asistenciaId);
    Task<SsIndIntento> GuardarIntento(int asistenciaId, int evaluacionVersionId, string respuestasJson, decimal nota, bool aprobado, DateTime? iniciadoEn);
    Task Firmar(int asistenciaId, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash,
        DateTime horaServidor, IndFirmarDto body, string? ip, string? userAgent, string declaracionVersion);
    Task<bool> ExisteSelfieHash(string selfieHash);

    // ── Instructor / cierre ─────────────────────────────────────────────────
    Task CerrarSesion(int sesionId, string firmaUrl, string firmaHash, string actaPdfUrl, string actaPdfHash, string actaPdfSha256, int? userId, string? ip);
    /// <summary>Ids de asistencia (de esta sesión) cuya selfie ya se usó en otra firma — de esta u otra sesión, o de un ATS.</summary>
    Task<HashSet<int>> GetAsistenciasConSelfieRepetida(int sesionId);
    Task AnularSesion(int sesionId, string motivo, int? userId, string? ip);
    Task ValidarAsistencia(int asistenciaId, int userId, string? ip);
    Task RechazarAsistencia(int asistenciaId, string motivo, int userId, string? ip);
    Task SetInstructorInfo(int sesionId, string? nombre, string? cargo);

    // ── Datos de apoyo ──────────────────────────────────────────────────────
    Task<(string? Nombre, string? Cargo)> GetNombreYCargo(int workerId);

    // ── Auditoría (hash encadenado) ─────────────────────────────────────────
    Task AgregarAudit(int sesionId, string evento, int? userId, string? ip, string? detalle);
}
