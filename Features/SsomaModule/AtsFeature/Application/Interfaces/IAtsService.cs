using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;

public interface IAtsService
{
    Task<int> ResolverWorkerId(int userId);
    Task<AtsInitDto> GetInit(int workerId);
    Task<List<AtsCategoriaPasoDto>> GetPasosPorPuesto(int puestoId, int workerId);
    Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto);

    Task<int> Crear(int workerId, AtsGuardarRequestDto dto);
    Task Editar(int id, int workerId, AtsGuardarRequestDto dto);
    Task<AtsResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin);
    Task<int> FirmarVistoGrupo(int grupoId, string rol, int callerUserId, bool esAdmin);
    Task<byte[]> GenerarPdfGrupo(int grupoId, int workerId, bool esAdmin);
    Task ReabrirGrupo(int grupoId, int workerId, bool esAdmin);
    Task<int> UnirseAGrupoLogueado(Guid token, int callerUserId, AtsGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent);
    Task<int?> GetMiAtsLogueado(Guid token, int callerUserId);
    Task<AtsResponseDto> GetContenidoGrupo(int grupoId, int workerId, bool esAdmin);
    Task<AtsObservacionesDto> GetObservacionesAts(int atsId, int callerUserId, bool esAdmin);
    Task<AtsObservacionesDto> GetObservacionesGrupo(int grupoId, int callerUserId, bool esAdmin);
    Task CrearObservacionAts(int atsId, string texto, int callerUserId, bool esAdmin);
    Task CrearObservacionGrupo(int grupoId, string texto, int callerUserId, bool esAdmin);
    Task ResolverObservacion(int observacionId, string respuesta, int callerUserId, bool esAdmin);
    Task AnularAts(int atsId, string motivo, int callerUserId, bool esAdmin);
    Task AnularGrupo(int grupoId, string motivo, int callerUserId, bool esAdmin);
    Task<List<AtsGrupoWorkerOpcionDto>> GetCandidatosGrupo(int grupoId, int workerId, bool esAdmin);
    Task SetIntegrantesGrupo(int grupoId, List<int> workerIds, int workerId, bool esAdmin);
    Task<int?> GetMiAtsPublico(Guid token, AtsGrupoMiAtsRequestDto body);
    Task<AtsGrupoListResponseDto> ListarGrupos(AtsFiltroDto filtro, int workerId, bool esAdmin);
    Task<AtsListaInitDto> GetListaInit(int workerId);

    Task Firmar(int id, int workerId, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task<byte[]> GenerarPdf(int id);
    Task<AtsVerificacionPublicaDto> VerificarPublico(int id, string? hash);

    Task<List<AtsPlantillaDto>> GetPlantillas();
    Task<List<AtsPuestoDto>> GetPuestos();
    Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo();
    Task SetPasoPuestos(int pasoId, List<int> puestoIds);
    Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo();
    Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds);
    Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto);
    Task<AtsEppDto> CrearEpp(AtsCatalogoItemCrearDto dto);
    Task<AtsHerramientaDto> CrearHerramienta(AtsCatalogoItemCrearDto dto);
    Task<AtsPeligroDto> CrearPeligro(AtsCatalogoItemCrearDto dto);
    Task<AtsRiesgoDto> CrearRiesgo(int peligroId, AtsCatalogoItemCrearDto dto);
    Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto);
    Task DesactivarPlantilla(int id);
    Task<List<AtsPeligroDto>> GetPeligrosConRiesgos();
    Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar);

    // ── Actividades/pasos por plantilla ──────────────────────────────────
    Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId);
    Task<int> CrearActividad(int plantillaId, AtsPlantillaActividadGuardarRequestDto dto);
    Task EditarActividad(int actividadId, AtsPlantillaActividadGuardarRequestDto dto);
    Task EliminarActividad(int actividadId);
    Task<int> CrearPaso(int actividadId, AtsPlantillaPasoGuardarRequestDto dto);
    Task EditarPaso(int pasoId, AtsPlantillaPasoGuardarRequestDto dto);
    Task EliminarPaso(int pasoId);
    Task SetActividadPeligros(int actividadId, AtsPlantillaActividadPeligrosRequestDto dto);

    // ── Controles sugeridos por riesgo ───────────────────────────────────
    Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles();
    Task<int> CrearControl(int riesgoId, AtsRiesgoControlGuardarRequestDto dto);
    Task EditarControl(int controlId, AtsRiesgoControlGuardarRequestDto dto);
    Task EliminarControl(int controlId);

    // ── Autorización de uso de firma digital e imagen (firmada en físico) — gate para poder hacer ATS ────
    Task<bool> TieneAutorizacionPermiso(int workerId);
    Task GuardarEmailPersonalAutorizacion(int workerId, AtsAutorizacionEmailRequestDto body);
    Task CrearCuentaCapataz(int workerId);
    Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion();
    Task SubirAutorizacionPermiso(int workerId, Stream archivo, string nombreArchivo, int subidoPorUserId);
    Task CapturarFirmaDigitalAutorizacion(int workerId, AtsAutorizacionFirmaDigitalRequestDto body, int capturadoPorUserId);
    Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId);
    Task<byte[]?> GetFirmaDigitalAutorizacionImagen(int workerId);
    Task<byte[]> GenerarPlantillaAutorizacionPdf(int workerId);

    // ── ATS Grupal ────────────────────────────────────────────────────────
    Task<AtsGrupoCrearResponseDto> CrearGrupo(int workerId, AtsGuardarRequestDto dto, bool esAdmin = false);
    Task<AtsGrupoEstadoDto> GetEstadoGrupo(int id, int workerId, bool esAdmin);
    Task CerrarGrupo(int id, int workerId, bool esAdmin);
    Task CerrarGrupoPublico(Guid token, AtsGrupoCerrarPublicoRequestDto body);
    Task<AtsGrupoResumenPublicoDto> GetResumenPublico(Guid token);
    Task<List<AtsGrupoWorkerOpcionDto>> GetWorkersParaAdhesion(Guid token);
    Task<int> UnirseAGrupo(Guid token, AtsGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent);

    Task<AtsGrupoCapatazPublicoDto> GetCapatazPublico(Guid token);
    Task FirmarCapatazPublico(Guid token, AtsGrupoCapatazFirmarRequestDto body);
    Task FirmarCapatazGrupoLogueado(int grupoId, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body);

    // ── QR fijo por proyecto (crear ATS Grupal sin login) ───────────────────
    Task<string> GetOrCrearQrProyecto(int proyectoId);
    Task<AtsGrupoProyectoPublicoDto> GetResumenProyectoPublico(Guid tokenProyecto);
    Task<AtsInitDto> GetInitPublico(Guid tokenProyecto, AtsGrupoInitPublicoRequestDto body);
    Task<AtsGrupoCrearResponseDto> CrearGrupoPublico(Guid tokenProyecto, AtsGrupoCrearPublicoRequestDto body);
}
