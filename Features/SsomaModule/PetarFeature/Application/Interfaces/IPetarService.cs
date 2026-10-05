using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;

public interface IPetarService
{
    Task<int> ResolverWorkerId(int userId);
    Task<PetarInitDto> GetInit(int atsId);
    /// <summary>Catálogo de tipos+checklist sin requerir un ATS individual — para el PETAR
    /// grupal, que nace de un ATS grupal, no de un ATS de una sola persona.</summary>
    Task<List<PetarTipoDto>> GetTiposCatalogo();

    Task<int> Crear(int workerId, PetarGuardarRequestDto dto);
    Task Editar(int id, int workerId, PetarGuardarRequestDto dto);
    Task<PetarResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin);
    Task<PetarListResponseDto> Listar(PetarFiltroDto filtro, int callerUserId, int workerId, bool esAdmin);

    Task Firmar(int id, int workerId, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task FirmarCapataz(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task FirmarSupervisor(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task Cerrar(int id, int workerId, PetarCerrarRequestDto body);

    Task<byte[]> GenerarPdf(int id);
    Task<byte[]> GenerarPdfGrupo(int petarGrupoId);
    Task<PetarVerificacionPublicaDto> VerificarPublicoGrupo(int petarGrupoId, string? hash);
    Task<PetarVerificacionPublicaDto> VerificarPublico(int id, string? hash);

    // ── PETAR Grupal ──────────────────────────────────────────────────────
    Task<PetarGrupoCrearResponseDto> CrearGrupo(int workerId, PetarGrupoCrearRequestDto dto);
    Task<List<PetarGrupoResumenPublicoDto>> GetGruposPublicoPorAtsToken(Guid atsToken);
    Task<List<PetarGrupoEstadoDto>> GetEstadosPorAtsGrupo(int atsGrupoId, int callerUserId, int workerId, bool esAdmin);
    Task<PetarGrupoEstadoDto> GetEstadoGrupo(int id, int callerUserId, int workerId, bool esAdmin);
    Task CerrarGrupo(int id, int workerId, bool esAdmin);
    Task FirmarCapatazGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task<List<PetarGrupoCapatazPublicoDto>> GetCapatazPublicoPetares(Guid atsToken);
    Task<PetarGrupoCrearResponseDto> CrearGrupoPublico(Guid atsToken, PetarGrupoCrearPublicoDto body);
    Task FirmarCapatazGrupoPublico(int petarGrupoId, PetarGrupoCapatazFirmarPublicoDto body);
    Task FirmarSupervisorGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task FirmarSsomaGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task<int> UnirseAGrupo(int petarGrupoId, PetarGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent);
}
