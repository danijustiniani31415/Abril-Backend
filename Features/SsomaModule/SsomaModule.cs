using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories;
using Abril_Backend.Features.Ssoma.Paso.Services;
using Abril_Backend.Features.Ssoma.Rac.Services;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Interfaces;

using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.InduccionProgramacionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionProgramacionFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.InduccionProgramacionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionProgramacionFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.Shared.DescansoCertificados;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using Abril_Backend.Shared.Services.Graph.Services;
using Abril_Backend.Features.Ssoma.Penalidad.Services;
using Abril_Backend.Features.Ssoma.Penalidad.Infrastructure;
using Abril_Backend.Shared.Services;

namespace Abril_Backend.Features.Ssoma
{
    public static class SsomaModule
    {
        public static IServiceCollection AddSsomaModule(this IServiceCollection services)
        {
            // Checklist SSOMA
            services.AddScoped<IChecklistRepository, ChecklistRepository>();
            services.AddScoped<IChecklistService, ChecklistService>();

            // Proyectos habilitados para SSOMA
            services.AddScoped<IProyectoHabilitadoRepository, ProyectoHabilitadoRepository>();
            services.AddScoped<IProyectoHabilitadoService, ProyectoHabilitadoService>();

            // Inhabilitaciones y Escuelitas
            services.AddScoped<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services.SsomaInhabilitacionService>();
            services.AddScoped<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services.SsomaEscuelitaService>();

            // Catalogos
            services.AddScoped<ICatalogosRepository, CatalogosRepository>();
            services.AddScoped<ICatalogosService, CatalogosService>();

            // Clinica usuarios
            services.AddScoped<IClinicaUsuarioService, ClinicaUsuarioService>();

            // EMO
            services.AddScoped<IEmoRepository, EmoRepository>();
            services.AddScoped<IEmoService, EmoService>();

            // Convalidacion
            services.AddScoped<IConvalidacionRepository, ConvalidacionRepository>();
            services.AddScoped<IConvalidacionService, ConvalidacionService>();

            // Programacion EMO
            services.AddScoped<IProgramacionEmoRepository, ProgramacionEmoRepository>();
            services.AddScoped<IProgramacionEmoService, ProgramacionEmoService>();

            // Interconsulta
            services.AddScoped<IInterconsultaRepository, InterconsultaRepository>();
            services.AddScoped<IInterconsultaService, InterconsultaService>();

            // Dashboard
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IDashboardService, DashboardService>();

            // Workers search
            services.AddScoped<IWorkerSearchRepository, WorkerSearchRepository>();
            services.AddScoped<IWorkerSearchService, WorkerSearchService>();

            // Validación de los correos del trabajador: el corporativo contra el directorio de Abril
            // (Microsoft Graph, app-only) y contra los ya asignados en workers, el personal solo en
            // formato, y la regla de que quede al menos uno de los dos.
            services.AddScoped<IWorkerEmailValidator, WorkerEmailValidator>();
            services.AddScoped<IGraphUserService, GraphUserService>();

            // Alertas EMO (cron)
            services.AddScoped<IEmoAlertaService, EmoAlertaService>();

            // Alertas SSOMA (cron) — accidentes, descansos, reinducción, casos sociales
            services.AddScoped<ISsomaReminderService, SsomaReminderService>();

            // Auto-programación EMO (cron)
            services.AddScoped<IEmoAutoProgramacionService, EmoAutoProgramacionService>();

            // Configuración de EMOs — matriz de destinatarios de los correos de EMO
            // (correo × perfil del trabajador × destinatario) y el resolver que la
            // aplica: única fuente de verdad de a quién le llega cada correo.
            services.AddScoped<IEmoCorreoConfigRepository, EmoCorreoConfigRepository>();
            services.AddScoped<IEmoCorreoConfigService, EmoCorreoConfigService>();
            services.AddScoped<IEmoDestinatariosResolver, EmoDestinatariosResolver>();

            // Aviso del resultado del EMO (evento RESULTADO de esa misma matriz): sale al
            // registrar el examen, solo con un veredicto cerrado.
            services.AddScoped<IEmoResultadoNotificacionService, EmoResultadoNotificacionService>();

            // Resumen diario EMO (cron 4:30pm)
            services.AddScoped<IEmoResumenDiarioService, EmoResumenDiarioService>();

            // Aviso al ingresar: interconsultas pendientes + EMOs vencidos de los proyectos del
            // Administrador/Coordinador SSOMA logueado. Calculado en vivo, sin cron.
            services.AddScoped<IAlertaLoginSsomaService, AlertaLoginSsomaService>();

            // PASO — Programa Anual de Seguridad
            services.AddScoped<IPasoService, PasoService>();

            // RAC — Reporte de Actos y Condiciones Subestándar
            services.AddScoped<IRacService, RacService>();
            services.AddScoped<IRacSharePointService, RacSharePointService>();
            services.AddScoped<IRacNotificationService, RacNotificationService>();

            // Penalidades — independiente de RAC (ver PenalidadFeature)
            services.AddScoped<IProyectoResponsablesResolver, ProyectoResponsablesResolver>();
            services.AddScoped<IPenalidadService, PenalidadService>();
            services.AddScoped<IPenalidadNotificationService, PenalidadNotificationService>();
            services.AddScoped<IGestionPreviaService, GestionPreviaService>();
            // Bitácora de estados de la penalidad: registra por quién y cuándo pasó cada cambio,
            // en el mismo SaveChanges que lo mueve. Se engancha en Program.cs (AddInterceptors).
            services.AddSingleton<PenalidadEstadoHistorialInterceptor>();

            // OPT — Observación Planeada de Tarea
            services.AddScoped<IOptRepository, OptRepository>();
            services.AddScoped<IOptSharePointService, OptSharePointService>();
            services.AddScoped<IOptService, OptService>();

            // Inspecciones
            services.AddScoped<IInspeccionSharePointService, InspeccionSharePointService>();
            services.AddScoped<IInspeccionRepository, InspeccionRepository>();
            services.AddScoped<IInspeccionService, InspeccionService>();
            services.AddScoped<InspeccionPdfService>();

            // Tópico Médico
            services.AddScoped<ITopicoRepository, TopicoRepository>();
            services.AddScoped<ITopicoService, TopicoService>();

            // Accidentes de Trabajo
            services.AddScoped<IAccidenteTrabajoRepository, AccidenteTrabajoRepository>();
            services.AddScoped<IAccidenteTrabajoService, AccidenteTrabajoService>();

            // Seguimiento Médico de Accidentes (citas, equipos, alta)
            services.AddScoped<ICitaMedicaRepository, CitaMedicaRepository>();
            services.AddScoped<ICitaMedicaService, CitaMedicaService>();
            services.AddScoped<IEquipoPrestadoRepository, EquipoPrestadoRepository>();
            services.AddScoped<IEquipoPrestadoService, EquipoPrestadoService>();
            services.AddScoped<IAltaMedicaRepository, AltaMedicaRepository>();
            services.AddScoped<IAltaMedicaService, AltaMedicaService>();

            // Descansos Médicos
            services.AddScoped<IDescansoMedicoRepository, DescansoMedicoRepository>();
            services.AddScoped<IDescansoMedicoService, DescansoMedicoService>();

            // Certificados médicos: lo comparten Descansos y Mi Salud, y el destino es la
            // carpeta de SharePoint configurada en ss_descanso_carpeta.
            services.AddScoped<Abril_Backend.Shared.Services.SharePoint.Interfaces.IGraphSharePointService,
                Abril_Backend.Shared.Services.SharePoint.Services.GraphSharePointService>();
            services.AddScoped<IDescansoCertificadoStorage, DescansoCertificadoStorage>();

            // SCTR — Asistente Social
            services.AddScoped<ISctrGestionRepository, SctrGestionRepository>();
            services.AddScoped<ISctrGestionService, SctrGestionService>();

            // Mi Salud (self-service staff)
            services.AddScoped<IMiSaludRepository, MiSaludRepository>();
            services.AddScoped<IMiSaludService, MiSaludService>();

            // Asistente Social — Casos Sociales
            services.AddScoped<ICasoSocialRepository, CasoSocialRepository>();
            services.AddScoped<ISeguimientoRepository, SeguimientoRepository>();
            services.AddScoped<ICasoSocialService, CasoSocialService>();

            // Charlas y Capacitaciones
            services.AddScoped<ICharlaService, CharlaService>();
            services.AddScoped<Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces.ICharlaContratistaService,
                Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Services.CharlaContratistaService>();

            // Accidentes e Incidentes
            services.AddScoped<IAccidenteIncidenteRepository, AccidenteIncidenteRepository>();
            services.AddScoped<IAccidenteIncidenteService, AccidenteIncidenteService>();

            // Auditoría ATS
            services.AddScoped<IAuditoriaAtsRepository, AuditoriaAtsRepository>();
            services.AddScoped<IAuditoriaAtsService, AuditoriaAtsService>();

            // ATS Digital — cada staff llena y firma su propio Análisis de Trabajo Seguro,
            // con selfie + geolocalización + firma como evidencia de trazabilidad.
            services.AddScoped<IAtsRepository, AtsRepository>();
            services.AddScoped<IAtsService, AtsService>();

            // Inducción digital — reemplaza SSO-FO-118/119/031/091 y GTH-FO-17: sesión con QR, entrega de
            // documentos versionados, evaluación, firma (selfie + GPS) y acta PDF inmutable.
            services.AddScoped<IInduccionRepository, InduccionRepository>();
            services.AddScoped<IInduccionService, InduccionService>();
            services.AddScoped<IInduccionActaPdfService, InduccionActaPdfService>();
            // Fase 2: sello de tiempo externo (RFC 3161) + respaldo en otra cuenta; reintentos en segundo plano.
            services.AddScoped<IInduccionPostCierreRepository, InduccionPostCierreRepository>();
            services.AddScoped<IInduccionSelloService, InduccionSelloService>();
            services.AddScoped<IInduccionRespaldoService, InduccionRespaldoService>();
            // Fase 3: vigencia anual, re-aceptación por cambio material, bloqueo del ATS (apagado por defecto), expediente.
            services.AddScoped<IInduccionVigenciaRepository, InduccionVigenciaRepository>();
            services.AddScoped<IInduccionVigenciaService, InduccionVigenciaService>();
            services.AddHostedService<Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.InduccionAlertaService>();
            services.AddHostedService<Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.InduccionPostCierreService>();

            // Check list digital de inspección pre-uso de equipos (arnés SSO-FO-049, minicargador SSO-FO-056…):
            // QR por proyecto/equipo, llenado sin login (DNI + selfie + firma), cadena de firmas y bloqueo de equipos.
            services.AddScoped<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Interfaces.IInspeccionEquipoRepository,
                Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Repositories.InspeccionEquipoRepository>();
            services.AddScoped<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Interfaces.IInspeccionEquipoService,
                Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Services.InspeccionEquipoService>();

            // PETAR — Permiso Escrito de Trabajo de Alto Riesgo, enlazado al ATS que lo origina.
            services.AddScoped<IPetarRepository, PetarRepository>();
            services.AddScoped<IPetarService, PetarService>();

            // Amonestaciones y Suspensiones
            services.AddScoped<IAmonestacionRepository, AmonestacionRepository>();
            services.AddScoped<IAmonestacionService, AmonestacionService>();
            services.AddScoped<AmonestacionNotificationService>();

            // Indicadores Proactivos
            services.AddScoped<IIndicadoresProactivosRepository, IndicadoresProactivosRepository>();
            services.AddScoped<IIndicadoresProactivosService, IndicadoresProactivosService>();
            services.AddSingleton<Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.ReactivosCacheVersion>();

            // Desempeño Supervisor
            services.AddScoped<DesempenoSupervisorRepository>();

            // Presupuesto de Materiales SSOMA
            services.AddScoped<ICatalogoMaterialesRepository, CatalogoMaterialesRepository>();
            services.AddScoped<ICatalogoMaterialesService, CatalogoMaterialesService>();
            services.AddScoped<IConsumoRepository, ConsumoRepository>();
            services.AddScoped<IEstandarizacionRepository, EstandarizacionRepository>();
            services.AddScoped<IEstandarizacionService, EstandarizacionService>();
            services.AddScoped<IConsumoService, ConsumoService>();
            services.AddScoped<IRevisionMaterialesService, RevisionMaterialesService>();
            services.AddScoped<IRatioRepository, RatioRepository>();
            services.AddScoped<IRatioService, RatioService>();
            services.AddScoped<IDriversService, DriversService>();
            services.AddScoped<IPresupuestoRepository, PresupuestoRepository>();
            services.AddScoped<IPresupuestoService, PresupuestoService>();
            services.AddScoped<IControlConsumoRepository, ControlConsumoRepository>();
            services.AddScoped<IControlConsumoService, ControlConsumoService>();
            services.AddScoped<IPersonalHitoRepository, PersonalHitoRepository>();
            services.AddScoped<IPersonalHitoService, PersonalHitoService>();
            services.AddScoped<IVigilanciaHitoRepository, VigilanciaHitoRepository>();
            services.AddScoped<IVigilanciaHitoService, VigilanciaHitoService>();
            services.AddScoped<IServicioFijoRepository, ServicioFijoRepository>();
            services.AddScoped<IServicioFijoService, ServicioFijoService>();
            services.AddScoped<IKitRepository, KitRepository>();
            services.AddScoped<IKitService, KitService>();
            services.AddScoped<IPresupuestoResumenExportService, PresupuestoResumenExportService>();
            services.AddScoped<IEpiStaffRepository, EpiStaffRepository>();
            services.AddScoped<IEpiStaffCalculoService, EpiStaffCalculoService>();
            services.AddScoped<ICostoFijoManualRepository, CostoFijoManualRepository>();
            services.AddScoped<ICostoFijoManualService, CostoFijoManualService>();
            services.AddScoped<IRatioDriverRepository, RatioDriverRepository>();
            services.AddScoped<IRatioDriverService, RatioDriverService>();
            services.AddScoped<IHhCargaRepository, HhCargaRepository>();
            services.AddScoped<IHhCargaService, HhCargaService>();

            // Horas Hombre (a partir del Tareo de Control de Acceso)
            services.AddScoped<IHorasHombreRepository, HorasHombreRepository>();
            services.AddScoped<IHorasHombreService, HorasHombreService>();

            // Programación de Inducciones (rotación por proyecto + aviso automático)
            services.AddScoped<IInduccionProgramacionRepository, InduccionProgramacionRepository>();
            services.AddScoped<IInduccionProgramacionService, InduccionProgramacionService>();

            // Programación de Inspecciones Cruzadas (rotación mensual por anillo de proyectos)
            services.AddScoped<IInspeccionCruzadaProgramacionRepository, InspeccionCruzadaProgramacionRepository>();
            services.AddScoped<IInspeccionCruzadaProgramacionService, InspeccionCruzadaProgramacionService>();

            // PETS — catálogo de pasos, piloto para que OPT (y a futuro Estándares/IPERC)
            // jalen automáticamente la estructura en vez de tipearla a mano.
            services.AddScoped<IPetsRepository, PetsRepository>();
            services.AddScoped<IPetsService, PetsService>();
            services.AddScoped<IPetsImportService, PetsImportService>();

            // Activos Rotativos — equipos que circulan entre proyectos (tambores retráctiles,
            // frenos de cuerda, etc.). Solo visibilidad + contacto, sin flujo de aprobación.
            services.AddScoped<IActivoRotativoRepository, ActivoRotativoRepository>();
            services.AddScoped<IActivoRotativoService, ActivoRotativoService>();

            // Cumplimiento SSOMA — guía de actividades mínimas diarias/semanales/mensuales
            // del Coordinador SSOMA y el Prevencionista, por proyecto.
            services.AddScoped<ICumplimientoRepository, CumplimientoRepository>();
            services.AddScoped<ICumplimientoService, CumplimientoService>();

            // EPP — Catálogo Autorizado (SSOMA + Logística)
            services.AddScoped<IEppRepository, EppRepository>();
            services.AddScoped<IEppService, EppService>();
            services.AddScoped<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces.IEppPedidoRepository,
                Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Repositories.EppPedidoRepository>();
            services.AddScoped<Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces.IEppPedidoService,
                Abril_Backend.Features.SsomaModule.EppFeature.Application.Services.EppPedidoService>();

            // Hoja de Ruta de Contratista — resumen semanal de cumplimiento para valorización,
            // agregador de solo lectura sobre Habilitación (trabajador/empresa/equipo), Dossier,
            // Charlas, RAC y Entregables de accidente. No escribe nada nuevo.
            services.AddScoped<IHojaRutaService, HojaRutaService>();

            // Gestión de Residuos de Obra — viajes/retiros, catálogos de tipo de residuo y EO-RS,
            // autorizaciones DME, declaración anual SIGERSOL/DAMRS, constancias mensuales/finales y
            // documentos de referencia.
            services.AddScoped<IResiduoTipoRepository, ResiduoTipoRepository>();
            services.AddScoped<IResiduoTipoService, ResiduoTipoService>();
            services.AddScoped<IResiduoEoRsRepository, ResiduoEoRsRepository>();
            services.AddScoped<IResiduoEoRsService, ResiduoEoRsService>();
            services.AddScoped<IResiduoAutorizacionDmeRepository, ResiduoAutorizacionDmeRepository>();
            services.AddScoped<IResiduoAutorizacionDmeService, ResiduoAutorizacionDmeService>();
            services.AddScoped<IResiduoViajeRepository, ResiduoViajeRepository>();
            services.AddScoped<IResiduoViajeService, ResiduoViajeService>();
            services.AddScoped<IResiduoDeclaracionRepository, ResiduoDeclaracionRepository>();
            services.AddScoped<IResiduoDeclaracionService, ResiduoDeclaracionService>();
            services.AddScoped<IResiduoConstanciaRepository, ResiduoConstanciaRepository>();
            services.AddScoped<IResiduoConstanciaService, ResiduoConstanciaService>();
            services.AddScoped<IResiduoConstanciaFinalRepository, ResiduoConstanciaFinalRepository>();
            services.AddScoped<IResiduoConstanciaFinalService, ResiduoConstanciaFinalService>();
            services.AddScoped<IResiduoDocumentoReferenciaRepository, ResiduoDocumentoReferenciaRepository>();
            services.AddScoped<IResiduoDocumentoReferenciaService, ResiduoDocumentoReferenciaService>();

            return services;
        }
    }
}
