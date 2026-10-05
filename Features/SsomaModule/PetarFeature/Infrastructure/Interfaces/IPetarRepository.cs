using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;

public interface IPetarRepository
{
    Task<List<PetarTipoDto>> GetTiposConItems();

    Task<(int ProyectoId, string? Lugar, string Actividad, string Estado)> GetDatosAts(int atsId);

    Task<int> Crear(int workerId, PetarGuardarRequestDto dto);
    Task Editar(int id, int workerId, PetarGuardarRequestDto dto);
    Task<SsPetar?> GetEntidad(int id);
    Task<PetarResponseDto?> GetPorId(int id);
    Task<PetarListResponseDto> Listar(PetarFiltroDto filtro);

    Task Firmar(SsPetar petar, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash, DateTime horaServidor, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task FirmarVisto(int petarId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor);
    Task Cerrar(int petarId, int workerId, string firmaUrl, string firmaHash, string? observaciones, DateTime horaServidor);

    Task GuardarPdf(int id, string pdfUrl, string pdfHash);
    Task<string?> GetUltimoHashAuditLog(int petarId);
    Task AgregarAuditLog(int petarId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash);
    Task<bool> ExisteSelfieHash(string selfieHash);

    // ── PETAR Grupal ──────────────────────────────────────────────────────
    Task<SsPetarGrupo> CrearGrupo(int creadoPorWorkerId, int proyectoId, PetarGrupoCrearRequestDto dto);
    Task<List<PetarGrupoResumenPublicoDto>> GetGruposActivosPorAtsGrupo(int atsGrupoId);
    Task<List<PetarGrupoEstadoDto>> GetEstadosPorAtsGrupo(int atsGrupoId);
    Task<SsPetarGrupo?> GetGrupoEntidad(int id);
    Task<SsPetarGrupo?> GetGrupoParaPdf(int id);
    Task<PetarGrupoEstadoDto?> GetEstadoGrupo(int id);
    Task<bool> EsAutorDeGrupo(int petarGrupoId, int workerId);
    Task CerrarGrupo(int petarGrupoId);
    Task FirmarVistoGrupo(int petarGrupoId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor, PetarEvidenciaPresencia? evidencia = null);

    /// <summary>Crea el PETAR individual de este trabajador copiando el checklist ya snapshoteado
    /// del grupo — deja Estado="Borrador", listo para que el servicio llame a Firmar() enseguida,
    /// igual que el flujo de un PETAR individual normal.</summary>
    Task<int> CrearDesdeGrupo(int workerId, int atsIdPropio, SsPetarGrupo grupo);
    /// <summary>true si el trabajador ya firmó (como ejecutante) un PETAR de este PETAR grupal.</summary>
    Task<bool> YaFirmoPetarGrupo(int petarGrupoId, int workerId);
}
