using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.LearningModule.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Features.Configuration.AdjudicacionFolderFeature.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkSpecialtyFeature.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Lugares.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.CursoModule.Infrastructure.Models;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.Paso.Entities;
using Abril_Backend.Features.Ssoma.Rac.Entities;
using Abril_Backend.Features.Ssoma.Penalidad.Entities;
using Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Models;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Models;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Infrastructure.Models;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.RevisionesFeature.Infrastructure.Models;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Models;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Models;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Abril_Backend.Features.SsomaModule.InduccionProgramacionFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _provider;
        public AppDbContext(DbContextOptions<AppDbContext> opciones, IConfiguration config) : base(opciones)
        {
            _provider = config["Database:DatabaseProvider"] ?? "SqlServer";
        }

        /// <summary>
        /// Mapea la función <c>unaccent(text)</c> de PostgreSQL (extensión unaccent) para usarla
        /// en consultas LINQ y hacer búsquedas insensibles a tildes. Solo se traduce a SQL; el
        /// cuerpo nunca se ejecuta en memoria. Registrada en <see cref="ConfigurePostgreSQL"/>.
        /// </summary>
        public static string Unaccent(string input) => throw new NotSupportedException();
        public DbSet<Area> Area { get; set; }
        public DbSet<SubArea> SubArea { get; set; }
        public DbSet<ConstructionSiteLogbookControl> ConstructionSiteLogbookControl { get;set; }
        public DbSet<DocumentIdentityType> DocumentIdentityType { get; set; }
        public DbSet<ImageType> ImageType { get; set; }
        public DbSet<IvtControlPdf> IvtControlPdf { get;set; }
        public DbSet<Lesson> Lesson { get; set; }
        public DbSet<LessonImages> LessonImages { get; set; }
        public DbSet<Milestone> Milestone { get; set; }
        public DbSet<MilestoneSchedule> MilestoneSchedule { get; set; }
        public DbSet<MilestoneScheduleHistory> MilestoneScheduleHistory { get; set; }
        public DbSet<Notificacion> Notificacion { get; set; }
        public DbSet<PushSuscripcion> PushSuscripcion { get; set; }
        public DbSet<NotificacionTipo> NotificacionTipo { get; set; }
        public DbSet<Person> Person { get; set; }
        /// <summary>
        /// La firma de cada persona, una por tipo (dibujada / subida como imagen). Sustituye a las
        /// columnas <c>person.signature_*</c>, que solo aguantaban una firma.
        /// </summary>
        public DbSet<PersonFirma> PersonFirma { get; set; }
        public DbSet<FirmaTipo> FirmaTipo { get; set; }
        public DbSet<Sexo> Sexo { get; set; }
        public DbSet<Project> Project { get; set; }
        public DbSet<ProjectTorre> ProjectTorre { get; set; }
        /// <summary>Inmuebles de los propietarios (app Convivir Abril, módulo Propietarios): uno por fila.</summary>
        public DbSet<Propietario> Propietario { get; set; }
        /// <summary>Catálogo de qué es cada proyecto: PROYECTO, FFT, OFICINA_CENTRAL, AREA_INTERNA o PRUEBA (ProjectTipoIds).</summary>
        public DbSet<ProjectTipo> ProjectTipo { get; set; }
        /// <summary>Catálogo del ciclo de vida del proyecto: activo, finalizado o inactivo (ProjectCicloVidaIds).</summary>
        public DbSet<ProjectCicloVida> ProjectCicloVida { get; set; }
        /// <summary>Bitácora de cambios del residente del proyecto (la escribe ResidenteHistorialInterceptor).</summary>
        public DbSet<ProjectResidenteHistorial> ProjectResidenteHistorial { get; set; }
        public DbSet<ResidentReportIncidence> ResidentReportIncidence {get;set;}
        public DbSet<ResidentReportIncidenceImage> ResidentReportIncidenceImage {get;set;}
        public DbSet<ResidentReportResponse> ResidentReportResponse {get;set;}
        public DbSet<ResidentReportResponseImage> ResidentReportResponseImage {get;set;}
        public DbSet<Role> Role {get;set;}
        public DbSet<State> State { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<UserPasswordToken> UserPasswordToken {get;set;}
        public DbSet<UserRole> UserRole { get; set; }
        public DbSet<UserSession> UserSession { get; set; }
        public DbSet<UserProject> UserProject { get; set; }
        public DbSet<Contract> Contract { get; set; }
        public DbSet<ContractType> ContractType { get; set; }
        public DbSet<ContractModality> ContractModality { get; set; }
        public DbSet<ContractOrigin> ContractOrigin { get; set; }
        public DbSet<PaymentMethod> PaymentMethod { get; set; }
        public DbSet<PaymentForm> PaymentForm { get; set; }
        public DbSet<Contributor> Contributor { get; set; }
        public DbSet<Invoice> Invoice { get; set; }
        public DbSet<InvoicePaymentForm> InvoicePaymentForm { get; set; }
        public DbSet<Contractor> Contractor { get; set; }
        public DbSet<ContractorEmail> ContractorEmail { get; set; }
        public DbSet<ContractorPersonType> ContractorPersonType { get; set; }
        public DbSet<ContractorState> ContractorState { get; set; }
        public DbSet<ContractorUser> ContractorUser { get; set; }
        public DbSet<ContractorUpdateState> ContractorUpdateState { get; set; }
        public DbSet<ContractorUpdateRequest> ContractorUpdateRequest { get; set; }
        public DbSet<ContractorUpdateRequestEmail> ContractorUpdateRequestEmail { get; set; }
        public DbSet<Currency> Currency { get; set; }
        public DbSet<WorkItemCategory> WorkItemCategory { get; set; }
        public DbSet<WorkItemCategoryClause> WorkItemCategoryClause { get; set; }
        public DbSet<WorkItemCategoryAnexo3Clause> WorkItemCategoryAnexo3Clause { get; set; }
        public DbSet<WorkItemCategoryAnexo4Clause> WorkItemCategoryAnexo4Clause { get; set; }
        public DbSet<WorkItem> WorkItem { get; set; }
        public DbSet<WorkItemValorizationForm> WorkItemValorizationForm { get; set; }
        public DbSet<ProjectSubContractor> ProjectSubContractor { get; set; }
        public DbSet<CostosCronograma> CostosCronograma { get; set; }
        public DbSet<CostosCronogramaActividad> CostosCronogramaActividad { get; set; }
        public DbSet<CostosCronogramaActividadNodo> CostosCronogramaActividadNodo { get; set; }
        public DbSet<ProjectAdjudicacionFolder> ProjectAdjudicacionFolder { get; set; }
        public DbSet<ProjectAdjudicacionFolderType> ProjectAdjudicacionFolderType { get; set; }
        public DbSet<InvoiceFolder> InvoiceFolder { get; set; }
        public DbSet<Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models.InvoiceDocumentType> InvoiceDocumentType { get; set; }
        public DbSet<Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models.InvoiceStatus> InvoiceStatus { get; set; }
        public DbSet<Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models.InvoiceObservationReason> InvoiceObservationReason { get; set; }
        public DbSet<WorkSpecialty> WorkSpecialty { get; set; }
        public DbSet<ProjectSubContractorQuotationFile> ProjectSubContractorQuotationFile { get; set; }
        public DbSet<ProjectSubContractorComparativeFile> ProjectSubContractorComparativeFile { get; set; }
        public DbSet<ProjectSubContractorStatus> ProjectSubContractorStatus { get; set; }
        public DbSet<ProjectSubContractorStepOption> ProjectSubContractorStepOption { get; set; }
        public DbSet<ProjectSubContractorContract> ProjectSubContractorContract { get; set; }
        public DbSet<ProjectSubContractorSummarySheet> ProjectSubContractorSummarySheet { get; set; }
        public DbSet<ProjectSubContractorBudget> ProjectSubContractorBudget { get; set; }
        public DbSet<ProjectSubContractorSchedule> ProjectSubContractorSchedule { get; set; }
        public DbSet<ProjectSubContractorAttachedQuotation> ProjectSubContractorAttachedQuotation { get; set; }
        public DbSet<ProjectSubContractorServiceOrder> ProjectSubContractorServiceOrder { get; set; }
        public DbSet<ProjectSubContractorFileStatus> ProjectSubContractorFileStatus { get; set; }
        public DbSet<ProjectSubContractorPromissoryNote> ProjectSubContractorPromissoryNote { get; set; }
        public DbSet<ProjectSubContractorScannedDoc> ProjectSubContractorScannedDoc { get; set; }
        public DbSet<ProjectSubContractorPackage> ProjectSubContractorPackage { get; set; }
        public DbSet<ProjectSubContractorInstructivo> ProjectSubContractorInstructivo { get; set; }
        public DbSet<ProjectSubContractorNonConformingOutput> ProjectSubContractorNonConformingOutput { get; set; }
        public DbSet<ProjectSubContractorToleranceChart> ProjectSubContractorToleranceChart { get; set; }
        public DbSet<ProjectSubContractorFichaTecnica> ProjectSubContractorFichaTecnica { get; set; }
        public DbSet<ProjectSubContractorAnexo> ProjectSubContractorAnexo { get; set; }
        public DbSet<StaffProjectEmail> StaffProjectEmail { get; set; }
        public DbSet<CostosPresupuestosEmail> CostosPresupuestosEmail { get; set; }
        public DbSet<StaffProjectEmailType> StaffProjectEmailType { get; set; }
        public DbSet<ProjectLink> ProjectLink { get; set; }
        public DbSet<ProjectLinkType> ProjectLinkType { get; set; }
        public DbSet<AcActividad> AcActividad { get; set; }
        public DbSet<AcAvanceSemanal> AcAvanceSemanal { get; set; }
        public DbSet<AcRankingSemanal> AcRankingSemanal { get; set; }
        public DbSet<AcCargaSemanal> AcCargaSemanal { get; set; }
        public DbSet<AcTareoEnrolamiento> AcTareoEnrolamiento { get; set; }
        public DbSet<AcTareoRegistro> AcTareoRegistro { get; set; }
        public DbSet<AcTareoAutorizacion> AcTareoAutorizacion { get; set; }
        public DbSet<AcEtapa> AcEtapa { get; set; }
        public DbSet<AcActividadPlantilla> AcActividadPlantilla { get; set; }
        public DbSet<AcCategoria> AcCategoria { get; set; }
        public DbSet<AcEspecialidad> AcEspecialidad { get; set; }
        public DbSet<Worker> Worker { get; set; }

        /// <summary>Catálogo único de categorías (campo de lógica). Ver <see cref="Categoria"/>.</summary>
        public DbSet<Categoria> Categoria => Set<Categoria>();

        /// <summary>Catálogo único de puestos (campo de presentación). Ver <see cref="Puesto"/>.</summary>
        public DbSet<Puesto> Puesto => Set<Puesto>();

        /// <summary>Congelado: reemplazado por <see cref="Categoria"/>. Solo lectura histórica.</summary>
        public DbSet<WorkersCategory> WorkersCategory => Set<WorkersCategory>();
        public DbSet<WorkersObraOficinaStaff> WorkersObraOficinaStaff => Set<WorkersObraOficinaStaff>();

        /// <summary>Catalogo de estados de la ficha de trabajador. Reemplaza al texto plano <c>workers.estado</c>.</summary>
        public DbSet<WorkersEstado> WorkersEstado => Set<WorkersEstado>();
        public DbSet<WorkerEmo> WorkerEmo { get; set; }
        public DbSet<WorkerEmoConvalidacion> WorkerEmoConvalidacion { get; set; }
        public DbSet<WorkerVinculacion> WorkerVinculacion { get; set; }

        /// <summary>
        /// Pasos del trabajador por Abril (ingreso → retiro). Reemplaza a
        /// <c>workers.fecha_ingreso</c> / <c>workers.fecha_retiro</c>: ver
        /// <see cref="WorkersPeriodoLaboral"/>.
        /// </summary>
        public DbSet<WorkersPeriodoLaboral> WorkersPeriodoLaboral => Set<WorkersPeriodoLaboral>();
        public DbSet<Features.SsomaModule.CharlasFeature.Infrastructure.Models.SsCharlaContratista> SsCharlaContratista { get; set; }
        public DbSet<WorkerProyecto> WorkerProyecto { get; set; }
        public DbSet<SsClinica> SsClinica { get; set; }
        public DbSet<SsClinicaResetToken> SsClinicaResetToken => Set<SsClinicaResetToken>();
        public DbSet<SsMedicoOcupacional> SsMedicoOcupacional { get; set; }
        public DbSet<Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsConvalidacionFirmaLog> SsConvalidacionFirmaLog => Set<Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsConvalidacionFirmaLog>();
        public DbSet<SsEmoTipo> SsEmoTipo { get; set; }
        public DbSet<SsExamenTipo> SsExamenTipo { get; set; }
        public DbSet<SsRestriccionTipo> SsRestriccionTipo { get; set; }
        public DbSet<SsAgenteRiesgo> SsAgenteRiesgo => Set<SsAgenteRiesgo>();
        public DbSet<SsEmoExamenDetalle> SsEmoExamenDetalle { get; set; }
        public DbSet<SsEmoRestriccion> SsEmoRestriccion { get; set; }
        public DbSet<SsInterconsulta> SsInterconsulta { get; set; }
        public DbSet<SsProgramacionEmo> SsProgramacionEmo { get; set; }
        public DbSet<SsSeguimientoMedico> SsSeguimientoMedico { get; set; }
        public DbSet<SsAlertaEmo> SsAlertaEmo { get; set; }
        public DbSet<SsItemTrabajador> SsItemTrabajador => Set<SsItemTrabajador>();
        public DbSet<SsItemEmpresa> SsItemEmpresa => Set<SsItemEmpresa>();
        public DbSet<SsItemEquipo> SsItemEquipo => Set<SsItemEquipo>();
        public DbSet<SsTipoEquipo> SsTipoEquipo => Set<SsTipoEquipo>();
        public DbSet<SsCriterioEvaluacion> SsCriterioEvaluacion => Set<SsCriterioEvaluacion>();
        public DbSet<SsEmpresaProyecto> SsEmpresaProyecto => Set<SsEmpresaProyecto>();
        public DbSet<SsHabTrabajador> SsHabTrabajador => Set<SsHabTrabajador>();
        public DbSet<SsHabEmpresa> SsHabEmpresa => Set<SsHabEmpresa>();
        public DbSet<SsSctrVidaley> SsSctrVidaley => Set<SsSctrVidaley>();
        public DbSet<SsSctrVidaLeyWorker> SsSctrVidaLeyWorker => Set<SsSctrVidaLeyWorker>();
        public DbSet<SsEquipo> SsEquipo => Set<SsEquipo>();
        public DbSet<SsHabEquipo> SsHabEquipo => Set<SsHabEquipo>();
        public DbSet<SsEvalSupervisor> SsEvalSupervisor => Set<SsEvalSupervisor>();
        public DbSet<SsEvalSupervisorItem> SsEvalSupervisorItem => Set<SsEvalSupervisorItem>();
        public DbSet<SsInduccion> SsInduccion => Set<SsInduccion>();
        public DbSet<SsInduccionRotacionProyecto> SsInduccionRotacionProyecto => Set<SsInduccionRotacionProyecto>();
        public DbSet<SsInduccionProgramacion> SsInduccionProgramacion => Set<SsInduccionProgramacion>();
        public DbSet<SsInduccionRotacionCursor> SsInduccionRotacionCursor => Set<SsInduccionRotacionCursor>();
        public DbSet<SsInspeccionCruzadaAnillo> SsInspeccionCruzadaAnillo => Set<SsInspeccionCruzadaAnillo>();
        public DbSet<SsInspeccionCruzadaRotacion> SsInspeccionCruzadaRotacion => Set<SsInspeccionCruzadaRotacion>();
        public DbSet<SsInspeccionCruzadaCursor> SsInspeccionCruzadaCursor => Set<SsInspeccionCruzadaCursor>();
        public DbSet<SsInspeccionCruzadaProgramacion> SsInspeccionCruzadaProgramacion => Set<SsInspeccionCruzadaProgramacion>();
        public DbSet<SsRegistroModelo> SsRegistroModelo => Set<SsRegistroModelo>();

        // ATS Digital (AtsFeature) — catálogos
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsCategoriaPaso> SsAtsCategoriaPaso => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsCategoriaPaso>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPaso> SsAtsPaso => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPaso>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPasoPuesto> SsAtsPasoPuesto => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPasoPuesto>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPeligro> SsAtsPeligro => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPeligro>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsRiesgo> SsAtsRiesgo => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsRiesgo>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsEpp> SsAtsEpp => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsEpp>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsHerramienta> SsAtsHerramienta => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsHerramienta>();

        // ATS Digital — plantillas
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantilla> SsAtsPlantilla => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantilla>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaPuesto> SsAtsPlantillaPuesto => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaPuesto>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaPeligro> SsAtsPlantillaPeligro => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaPeligro>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaEpp> SsAtsPlantillaEpp => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaEpp>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaHerramienta> SsAtsPlantillaHerramienta => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaHerramienta>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaActividad> SsAtsPlantillaActividad => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaActividad>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaPaso> SsAtsPlantillaPaso => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaPaso>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaActividadPeligro> SsAtsPlantillaActividadPeligro => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPlantillaActividadPeligro>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsRiesgoControl> SsAtsRiesgoControl => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsRiesgoControl>();

        // ATS Digital — instancia
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAts> SsAts => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAts>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPasoSeleccionado> SsAtsPasoSeleccionado => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsPasoSeleccionado>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsEppSeleccionado> SsAtsEppSeleccionado => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsEppSeleccionado>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsHerramientaSeleccionada> SsAtsHerramientaSeleccionada => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsHerramientaSeleccionada>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsRiesgoDetalle> SsAtsRiesgoDetalle => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsRiesgoDetalle>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsObservacion> SsAtsObservacion => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsObservacion>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoIntegrante> SsAtsGrupoIntegrante => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoIntegrante>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoEvento> SsAtsGrupoEvento => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoEvento>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupo> SsAtsGrupo => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupo>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoPasoSeleccionado> SsAtsGrupoPasoSeleccionado => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoPasoSeleccionado>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoEppSeleccionado> SsAtsGrupoEppSeleccionado => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoEppSeleccionado>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoHerramientaSeleccionada> SsAtsGrupoHerramientaSeleccionada => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoHerramientaSeleccionada>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoRiesgoDetalle> SsAtsGrupoRiesgoDetalle => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsGrupoRiesgoDetalle>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsAuditLog> SsAtsAuditLog => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsAuditLog>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsProyectoQr> SsAtsProyectoQr => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsProyectoQr>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsConsentimiento> SsAtsConsentimiento => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsConsentimiento>();

        // Check list digital de inspección pre-uso de equipos (InspeccionEquipoFeature)
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipoTipo> SsInspEquipoTipo => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipoTipo>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipoTipoVariante> SsInspEquipoTipoVariante => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipoTipoVariante>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspPlantillaItem> SsInspPlantillaItem => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspPlantillaItem>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspItemMarcador> SsInspItemMarcador => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspItemMarcador>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipo> SsInspEquipo => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipo>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspInspeccion> SsInspInspeccion => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspInspeccion>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspInspeccionVariante> SsInspInspeccionVariante => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspInspeccionVariante>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspRespuesta> SsInspRespuesta => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspRespuesta>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipoEvento> SsInspEquipoEvento => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspEquipoEvento>();
        public DbSet<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspAuditLog> SsInspAuditLog => Set<Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models.SsInspAuditLog>();

        // PETAR — Permiso Escrito de Trabajo de Alto Riesgo (PetarFeature)
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarTipo> SsPetarTipo => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarTipo>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarItem> SsPetarItem => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarItem>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetar> SsPetar => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetar>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarItemRespuesta> SsPetarItemRespuesta => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarItemRespuesta>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarAuditLog> SsPetarAuditLog => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarAuditLog>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarIzajeGrua> SsPetarIzajeGrua => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarIzajeGrua>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarGrupo> SsPetarGrupo => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarGrupo>();
        public DbSet<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarGrupoItemRespuesta> SsPetarGrupoItemRespuesta => Set<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarGrupoItemRespuesta>();
        public DbSet<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsAutorizacionPermiso> SsAtsAutorizacionPermiso => Set<Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models.SsAtsAutorizacionPermiso>();
        // Inducción digital (InduccionFeature)
        public DbSet<SsIndDocumento> SsIndDocumento => Set<SsIndDocumento>();
        public DbSet<SsIndDocumentoVersion> SsIndDocumentoVersion => Set<SsIndDocumentoVersion>();
        public DbSet<SsIndTema> SsIndTema => Set<SsIndTema>();
        public DbSet<SsIndPregunta> SsIndPregunta => Set<SsIndPregunta>();
        public DbSet<SsIndPreguntaOpcion> SsIndPreguntaOpcion => Set<SsIndPreguntaOpcion>();
        public DbSet<SsIndEvaluacionVersion> SsIndEvaluacionVersion => Set<SsIndEvaluacionVersion>();
        public DbSet<SsIndSesion> SsIndSesion => Set<SsIndSesion>();
        public DbSet<SsIndSesionDocumento> SsIndSesionDocumento => Set<SsIndSesionDocumento>();
        public DbSet<SsIndSesionTema> SsIndSesionTema => Set<SsIndSesionTema>();
        public DbSet<SsIndAsistencia> SsIndAsistencia => Set<SsIndAsistencia>();
        public DbSet<SsIndAsistenciaDocumento> SsIndAsistenciaDocumento => Set<SsIndAsistenciaDocumento>();
        public DbSet<SsIndIntento> SsIndIntento => Set<SsIndIntento>();
        public DbSet<SsIndAuditLog> SsIndAuditLog => Set<SsIndAuditLog>();
        public DbSet<SsIndSello> SsIndSello => Set<SsIndSello>();
        public DbSet<SsIndRespaldo> SsIndRespaldo => Set<SsIndRespaldo>();
        public DbSet<SsIndGateProyecto> SsIndGateProyecto => Set<SsIndGateProyecto>();
        public DbSet<SsIndExencion> SsIndExencion => Set<SsIndExencion>();
        public DbSet<SsIndAlerta> SsIndAlerta => Set<SsIndAlerta>();
        public DbSet<SsItemTrabajadorRegla> SsItemTrabajadorRegla => Set<SsItemTrabajadorRegla>();
        public DbSet<SsHabBloqueoLog> SsHabBloqueoLog => Set<SsHabBloqueoLog>();
        public DbSet<SsRetiroAutomaticoLog> SsRetiroAutomaticoLog => Set<SsRetiroAutomaticoLog>();
        public DbSet<AuditoriaCambio> AuditoriaCambios => Set<AuditoriaCambio>();
        public DbSet<SsHabDocumentoVersion> SsHabDocumentoVersion => Set<SsHabDocumentoVersion>();
        public DbSet<SsHabDocumentoArchivo> SsHabDocumentoArchivo => Set<SsHabDocumentoArchivo>();
        public DbSet<SsResetToken> SsResetToken => Set<SsResetToken>();
        public DbSet<SsTareo> SsTareo => Set<SsTareo>();
        public DbSet<SsTareoPartida> SsTareoPartida => Set<SsTareoPartida>();
        public DbSet<SsTareoDetalleCasa> SsTareoDetalleCasa => Set<SsTareoDetalleCasa>();
        public DbSet<SsTareoDetalleContratista> SsTareoDetalleContratista => Set<SsTareoDetalleContratista>();
        public DbSet<CatSubarea> CatSubarea => Set<CatSubarea>();
        public DbSet<CatCategoria> CatCategoria => Set<CatCategoria>();
        public DbSet<CatOcupacion> CatOcupacion => Set<CatOcupacion>();
        public DbSet<WorkerEvento> WorkerEvento => Set<WorkerEvento>();
        public DbSet<SsTrabajadorRestringido> SsTrabajadorRestringido => Set<SsTrabajadorRestringido>();
        // CatJefatura (cat_jefatura) eliminado: la resolución del jefe de un trabajador
        // pasó a IJefeRevisorResolver (configuración global de revisores).
        public DbSet<SsClinicaEmail> SsClinicaEmail => Set<SsClinicaEmail>();
        public DbSet<GaHoraOpcion> GaHoraOpcion { get; set; }
        public DbSet<GaLugar> GaLugar { get; set; }
        public DbSet<GaMotivoSalida> GaMotivoSalida { get; set; }
        public DbSet<GaSolicitudSalida> GaSolicitudSalida { get; set; }
        public DbSet<GaSolicitudTrayecto> GaSolicitudTrayecto { get; set; }
        public DbSet<GaSolicitudTrayectoAdjunto> GaSolicitudTrayectoAdjunto { get; set; }
        public DbSet<GaSolicitudCaptura> GaSolicitudCaptura { get; set; }
        public DbSet<GaRendicion> GaRendicion { get; set; }
        public DbSet<GaTrayecto> GaTrayecto { get; set; }
        // Override manual de "qué áreas ve este trabajador", por ámbito (salidas / rendiciones).
        public DbSet<GaVisibilidadAmbito> GaVisibilidadAmbito { get; set; }
        public DbSet<GaVisibilidadArea> GaVisibilidadArea { get; set; }
        /// <summary>
        /// Los cinco actores del ciclo de una salida (aprobar la salida, enterarse, 1.ª revisión,
        /// consolidar, firmar el consolidado) y los tipos de trabajador para los que se resuelven.
        /// Catálogos de ids fijos: <c>ActorIds</c> y <c>ActorCasoIds</c>.
        /// </summary>
        public DbSet<GaActor> GaActor { get; set; }
        public DbSet<GaActorCaso> GaActorCaso { get; set; }

        /// <summary>
        /// Lo personalizado por área (Configuración → Revisores de Áreas). Reemplazó a
        /// area_revisores, area_revisores_rendicion y area_consolidadores (2026-09-25).
        /// </summary>
        public DbSet<AreaActorAsignacion> AreaActorAsignacion { get; set; }

        /// <summary>
        /// Lo personalizado por trabajador (ficha del trabajador). Reemplazó a workers_revisores,
        /// el "jefe personalizado" (2026-09-25).
        /// </summary>
        public DbSet<WorkersActorAsignacion> WorkersActorAsignacion { get; set; }
        public DbSet<GaSalidasAreaConfig> GaSalidasAreaConfig { get; set; }
        public DbSet<GaAdjuntoFolder> GaAdjuntoFolder { get; set; }
        public DbSet<GaCapturaFolder> GaCapturaFolder { get; set; }
        public DbSet<GaRendicionFolder> GaRendicionFolder { get; set; }
        public DbSet<GaConsolidadoS10> GaConsolidadoS10 { get; set; }
        // Qué planillas cubre cada Consolidado del S10: un consolidado puede agrupar varias.
        public DbSet<GaConsolidadoS10Rendicion> GaConsolidadoS10Rendicion { get; set; }

        /// <summary>
        /// La planilla grupal que prepara el consolidador ANTES de subir el Consolidado del S10, y
        /// qué planillas de rendición cubre. El consolidado hereda de acá su código.
        /// </summary>
        public DbSet<GaPlanillaGrupal> GaPlanillaGrupal { get; set; }
        public DbSet<GaPlanillaGrupalRendicion> GaPlanillaGrupalRendicion { get; set; }

        /// <summary>
        /// Las firmas estampadas sobre un consolidado. Son filas y no una columna porque en obra
        /// firman dos personas sobre las mismas planillas (administrador de obra y residente).
        /// </summary>
        public DbSet<GaConsolidadoS10Firma> GaConsolidadoS10Firma { get; set; }
        // Solicitudes de corrección del Consolidado del S10 al Coordinador ERP (bandeja
        // "Correcciones S10"). Van por planilla, igual que el consolidado.
        public DbSet<GaCorreccionS10> GaCorreccionS10 { get; set; }
        // ── Configuración de correos de salidas (destinatarios por correo) ──────
        // ga_correo_pantalla agrupa los correos por la pantalla donde se originan: cada una
        // administra los suyos desde su propio botón «Configuración».
        public DbSet<GaCorreoPantalla> GaCorreoPantalla { get; set; }
        // ga_correo_grupo reparte los correos de una misma pantalla en secciones: los del flujo
        // (CORREOS) y los que dispara el cron del plazo de rendición (RECORDATORIOS).
        public DbSet<GaCorreoGrupo> GaCorreoGrupo { get; set; }
        public DbSet<GaCorreoEvento> GaCorreoEvento { get; set; }
        public DbSet<GaCorreoTipoDestinatario> GaCorreoTipoDestinatario { get; set; }
        public DbSet<GaCorreoRegla> GaCorreoRegla { get; set; }
        // Plazo de rendición configurable (fila única). Lo lee CalendarioNoLaborable.
        public DbSet<GaRendicionConfig> GaRendicionConfig { get; set; }
        public DbSet<GaRendicionAlcance> GaRendicionAlcance { get; set; }
        // ── Lecciones aprendidas / Áreas (wip/lecciones-aprendidas) ─────────────
        public DbSet<CatalogType> CatalogType => Set<CatalogType>();
        public DbSet<CatalogItem> CatalogItem => Set<CatalogItem>();
        public DbSet<ScopeItem> ScopeItem => Set<ScopeItem>();
        public DbSet<ScopeTemplate> ScopeTemplate => Set<ScopeTemplate>();
        public DbSet<ScopeTemplateItem> ScopeTemplateItem => Set<ScopeTemplateItem>();
        public DbSet<AreaType> AreaType => Set<AreaType>();
        public DbSet<AreaItem> AreaItem => Set<AreaItem>();
        public DbSet<Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Models.HolidayType> HolidayType => Set<Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Models.HolidayType>();
        public DbSet<Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Models.Holiday> Holiday => Set<Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Models.Holiday>();
        public DbSet<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope> AreaScope => Set<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>();
        // Catálogo de bancos (Configuración → Bancos): de acá sale el banco de cada razón social
        // del grupo, que es el que el formulario de bienvenida le muestra al nuevo colaborador.
        public DbSet<Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Models.Banco> Banco => Set<Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Models.Banco>();
        public DbSet<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models.LessonArea> LessonArea => Set<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models.LessonArea>();
        public DbSet<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.ProjectStaffReminder> ProjectStaffReminder => Set<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.ProjectStaffReminder>();
        public DbSet<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.LessonJefeReminder> LessonJefeReminder => Set<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.LessonJefeReminder>();

        // ── Tabla filtro única de proyectos por funcionalidad (patrón lesson_area) ──
        // project es la tabla matriz; una fila (project_id, funcionalidad_id) con
        // active = false oculta el proyecto SOLO en esa funcionalidad. Fila ausente =
        // proyecto visible. IDs del catálogo en Shared/Constants/ProyectoFiltroFuncionalidades.
        public DbSet<ProyectoFiltro> ProyectoFiltro => Set<ProyectoFiltro>();
        public DbSet<ProyectoFiltroFuncionalidad> ProyectoFiltroFuncionalidad => Set<ProyectoFiltroFuncionalidad>();

        // ── master ─────────────────────────────────────────────────────────────
        public DbSet<SsClinicaUsuario> SsClinicaUsuario => Set<SsClinicaUsuario>();
        public DbSet<SsClinicaToken> SsClinicaToken => Set<SsClinicaToken>();
        public DbSet<SsClinicaAuditoria> SsClinicaAuditoria => Set<SsClinicaAuditoria>();
        public DbSet<SsContratistaRol> SsContratistaRoles { get; set; }
        public DbSet<SsContratistaUsuario> SsContratistaUsuarios { get; set; }
        public DbSet<SsContratistaUsuarioProyecto> SsContratistaUsuarioProyectos { get; set; }
        public DbSet<ProjectActivity> ProjectActivity { get; set; }
        public DbSet<EvPeriodo> EvPeriodos => Set<EvPeriodo>();

        // ── CursoModule (cursos de capacitación interactivos, evaluación auditable SUNAFIL) ──
        public DbSet<Curso> Cursos => Set<Curso>();
        public DbSet<CursoSlide> CursoSlides => Set<CursoSlide>();
        public DbSet<CursoIntento> CursoIntentos => Set<CursoIntento>();
        public DbSet<CursoIntentoRespuesta> CursoIntentoRespuestas => Set<CursoIntentoRespuesta>();
        public DbSet<CursoIntentoEvidencia> CursoIntentoEvidencias => Set<CursoIntentoEvidencia>();
        public DbSet<CursoPreguntaBanco> CursoPreguntasBanco => Set<CursoPreguntaBanco>();
        public DbSet<EvPlantilla> EvPlantillas => Set<EvPlantilla>();
        public DbSet<EvEvaluacionResidente> EvEvaluacionesResidente => Set<EvEvaluacionResidente>();
        public DbSet<EvEvaluacionResidenteDetalle> EvEvaluacionesResidenteDetalle => Set<EvEvaluacionResidenteDetalle>();
        public DbSet<EvNoAplica> EvNoAplica => Set<EvNoAplica>();
        public DbSet<EvRecordatorioLog> EvRecordatorioLogs => Set<EvRecordatorioLog>();
        public DbSet<EvAsignacionSupervisor> EvAsignacionesSupervisor => Set<EvAsignacionSupervisor>();
        public DbSet<EvContratistaPlantilla> EvContratistaPlantillas => Set<EvContratistaPlantilla>();
        public DbSet<EvEvaluacionContratista> EvEvaluacionesContratista => Set<EvEvaluacionContratista>();
        public DbSet<EvEvaluacionContratistaDetalle> EvEvaluacionesContratistaDetalle => Set<EvEvaluacionContratistaDetalle>();
        public DbSet<EvSupervisorContratistaPlantilla> EvSupervisorContratistaPlantillas => Set<EvSupervisorContratistaPlantilla>();
        public DbSet<EvEvaluacionSupervisorContratista> EvEvaluacionesSupervisorContratista => Set<EvEvaluacionSupervisorContratista>();
        public DbSet<EvEvaluacionSupervisorContratistaDetalle> EvEvaluacionesSupervisorContratistaDetalle => Set<EvEvaluacionSupervisorContratistaDetalle>();
        public DbSet<EvJefeSsomaPlantilla> EvJefeSsomaPlantillas => Set<EvJefeSsomaPlantilla>();
        public DbSet<EvEvaluacionJefeSsoma> EvEvaluacionesJefeSsoma => Set<EvEvaluacionJefeSsoma>();
        public DbSet<EvEvaluacionJefeSsomaDetalle> EvEvaluacionesJefeSsomaDetalle => Set<EvEvaluacionJefeSsomaDetalle>();
        public DbSet<EvEvaluacionJefeSsomaCumplimiento> EvEvaluacionesJefeSsomaCumplimiento => Set<EvEvaluacionJefeSsomaCumplimiento>();
        public DbSet<EvJefeSsomaPlanAccion> EvJefeSsomaPlanAccion => Set<EvJefeSsomaPlanAccion>();
        public DbSet<EvGestionSsomaPlanAccion> EvGestionSsomaPlanAccion => Set<EvGestionSsomaPlanAccion>();
        public DbSet<EvPrevencionistaPlantilla> EvPrevencionistaPlantillas => Set<EvPrevencionistaPlantilla>();
        public DbSet<EvEvaluacionPrevencionista> EvEvaluacionesPrevencionista => Set<EvEvaluacionPrevencionista>();
        public DbSet<EvEvaluacionPrevencionistaDetalle> EvEvaluacionesPrevencionistaDetalle => Set<EvEvaluacionPrevencionistaDetalle>();
        public DbSet<EvGestionSsomaPlantilla> EvGestionSsomaPlantillas => Set<EvGestionSsomaPlantilla>();
        public DbSet<EvEvaluacionGestionSsoma> EvEvaluacionesGestionSsoma => Set<EvEvaluacionGestionSsoma>();
        public DbSet<EvEvaluacionGestionSsomaDetalle> EvEvaluacionesGestionSsomaDetalle => Set<EvEvaluacionGestionSsomaDetalle>();
        public DbSet<EvEvaluacionGestionSsomaCumplimiento> EvEvaluacionesGestionSsomaCumplimiento => Set<EvEvaluacionGestionSsomaCumplimiento>();
        public DbSet<EvStaffPlantilla> EvStaffPlantillas => Set<EvStaffPlantilla>();
        public DbSet<EvEvaluacionStaff> EvEvaluacionesStaff => Set<EvEvaluacionStaff>();
        public DbSet<EvEvaluacionStaffDetalle> EvEvaluacionesStaffDetalle => Set<EvEvaluacionStaffDetalle>();
        public DbSet<SsomaPasoCategoria> SsomaPasoCategorias { get; set; }
        public DbSet<SsomaPaso> SsomaPasos { get; set; }
        public DbSet<SsomaPasoActividad> SsomaPasoActividades { get; set; }
        public DbSet<SsomaPasoEjecucion> SsomaPasoEjecuciones { get; set; }
        public DbSet<SsomaPasoAuditoria> SsomaPasoAuditorias { get; set; }
        public DbSet<SsomaPasoEjecucionArchivo> SsomaPasoEjecucionArchivos { get; set; }
        // ── RAC — Reporte de Actos y Condiciones Subestándar ─────────────────
        public DbSet<SsomaRacCategoria> SsomaRacCategorias { get; set; }
        public DbSet<SsomaRacInfraccion> SsomaRacInfracciones { get; set; }
        public DbSet<SsomaUitAnio> SsomaUitAnios { get; set; }
        public DbSet<SsomaRac> SsomaRacs { get; set; }
        public DbSet<SsomaRacFoto> SsomaRacFotos { get; set; }
        public DbSet<SsomaPenalidad> SsomaPenalidades { get; set; }
        public DbSet<SsomaPenalidadEstadoHistorial> SsomaPenalidadEstadoHistorial { get; set; }
        public DbSet<GestionPreviaEmpresa> GestionPreviaEmpresas { get; set; }
        public DbSet<SsomaOpt> SsomaOpt { get; set; }
        public DbSet<SsomaOptTrabajador> SsomaOptTrabajador { get; set; }
        public DbSet<SsomaPet> SsomaPet { get; set; }
        public DbSet<SsomaPetVersion> SsomaPetVersion { get; set; }
        public DbSet<SsomaPetPaso> SsomaPetPaso { get; set; }
        public DbSet<SsomaPetPasoImagen> SsomaPetPasoImagen { get; set; }
        public DbSet<SsomaCatalogoItem> SsomaCatalogoItem { get; set; }
        public DbSet<SsomaPetItemSeleccionado> SsomaPetItemSeleccionado { get; set; }
        public DbSet<SsomaPetSeccionTexto> SsomaPetSeccionTexto { get; set; }
        public DbSet<SsomaPetFirma> SsomaPetFirma { get; set; }
        public DbSet<SsomaPetAnexo> SsomaPetAnexo { get; set; }
        public DbSet<SsomaOptCriterioVerificacion> SsomaOptCriterioVerificacion { get; set; }
        public DbSet<SsomaOptVerificacion> SsomaOptVerificacion { get; set; }
        public DbSet<SsomaOptPaso> SsomaOptPaso { get; set; }
        public DbSet<SsomaOptFotoArea> SsomaOptFotoArea => Set<SsomaOptFotoArea>();
        public DbSet<Feriado> Feriados { get; set; }
        public DbSet<ActivityPredecessor> ActivityPredecessors { get; set; }
        public DbSet<UserCronogramaPreference> UserCronogramaPreferences { get; set; }
        public DbSet<CronogramaTemplateItem> CronogramaTemplateItems { get; set; }
        public DbSet<SsHabAuditoria> SsHabAuditorias { get; set; }
        // ── Dossier Semanal ────────────────────────────────────────────────────
        public DbSet<SsDossierSemana> SsDossierSemana => Set<SsDossierSemana>();
        public DbSet<SsDossierDocumento> SsDossierDocumento => Set<SsDossierDocumento>();
        public DbSet<SsDossierDocumentoArchivo> SsDossierDocumentoArchivo => Set<SsDossierDocumentoArchivo>();
        // ── Accidentes e Incidentes / Flash Report ─────────────────────────────
        public DbSet<SsomaFlashTipo> SsomaFlashTipo => Set<SsomaFlashTipo>();
        public DbSet<SsomaFlashEtapaProyecto> SsomaFlashEtapaProyecto => Set<SsomaFlashEtapaProyecto>();
        public DbSet<SsomaFlashParteAfectada> SsomaFlashParteAfectada => Set<SsomaFlashParteAfectada>();
        public DbSet<SsomaEmpresaAbril> SsomaEmpresaAbril => Set<SsomaEmpresaAbril>();
        public DbSet<SsomaFlashPartida> SsomaFlashPartida => Set<SsomaFlashPartida>();
        public DbSet<SsomaAccidenteIncidente> SsomaAccidenteIncidente => Set<SsomaAccidenteIncidente>();
        public DbSet<SsomaFlashDescanso> SsomaFlashDescanso => Set<SsomaFlashDescanso>();
        public DbSet<SsomaAccidenteDocumento> SsomaAccidenteDocumento => Set<SsomaAccidenteDocumento>();
        public DbSet<SsomaEntregableTipo> SsomaEntregableTipo => Set<SsomaEntregableTipo>();
        public DbSet<SsomaEntregable> SsomaEntregable => Set<SsomaEntregable>();
        public DbSet<SsomaEntregableResponsable> SsomaEntregableResponsable => Set<SsomaEntregableResponsable>();
        public DbSet<SsomaEntregableArchivo> SsomaEntregableArchivo => Set<SsomaEntregableArchivo>();
        public DbSet<SsomaInvestigacionRm050> SsomaInvestigacionRm050 => Set<SsomaInvestigacionRm050>();
        public DbSet<SsomaAccionCorrectiva> SsomaAccionCorrectiva => Set<SsomaAccionCorrectiva>();
        // ── Inspecciones ───────────────────────────────────────────────────────
        public DbSet<SsomaInspeccionTipo> SsomaInspeccionTipo => Set<SsomaInspeccionTipo>();
        public DbSet<SsomaInspeccionChecklistItem> SsomaInspeccionChecklistItem => Set<SsomaInspeccionChecklistItem>();
        public DbSet<SsomaInspeccion> SsomaInspeccion => Set<SsomaInspeccion>();
        public DbSet<SsomaInspeccionRespuesta> SsomaInspeccionRespuesta => Set<SsomaInspeccionRespuesta>();
        public DbSet<SsomaInspeccionHallazgo> SsomaInspeccionHallazgo => Set<SsomaInspeccionHallazgo>();
        public DbSet<SsomaInspeccionHallazgoFoto> SsomaInspeccionHallazgoFoto => Set<SsomaInspeccionHallazgoFoto>();
        public DbSet<SsomaInspeccionFotoArea> SsomaInspeccionFotoArea => Set<SsomaInspeccionFotoArea>();
        public DbSet<SsomaInspeccionParticipante> SsomaInspeccionParticipante => Set<SsomaInspeccionParticipante>();
        // ── Indicadores Proactivos ─────────────────────────────────────────────
        public DbSet<SsomaProgInspeccionEmpresa> SsomaProgInspeccionEmpresa => Set<SsomaProgInspeccionEmpresa>();
        public DbSet<SsomaMetaAnual> SsomaMetaAnual => Set<SsomaMetaAnual>();
        // ── Auditoría ATS ──────────────────────────────────────────────────────
        public DbSet<SsomaAuditoriaAtsPregunta> SsomaAuditoriaAtsPregunta => Set<SsomaAuditoriaAtsPregunta>();
        public DbSet<SsomaAuditoriaAts> SsomaAuditoriaAts => Set<SsomaAuditoriaAts>();
        public DbSet<SsomaAuditoriaAtsRespuesta> SsomaAuditoriaAtsRespuesta => Set<SsomaAuditoriaAtsRespuesta>();
        public DbSet<SsomaAuditoriaAtsFoto> SsomaAuditoriaAtsFoto => Set<SsomaAuditoriaAtsFoto>();
        // ── Desempeño Supervisor ───────────────────────────────────────────────
        public DbSet<Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Models.SsDesempenoSupervisorExcluido> SsDesempenoSupervisorExcluidos => Set<Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Models.SsDesempenoSupervisorExcluido>();
        // ── Indicadores Proactivos (Seguimiento) ────────────────────────────────
        public DbSet<Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Models.SsIndicadorEmpresaExcluida> SsIndicadorEmpresaExcluidas => Set<Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Models.SsIndicadorEmpresaExcluida>();
        // ── Charlas y Capacitaciones ───────────────────────────────────────────────
        public DbSet<SsCharlaPrograma> SsCharlaProgramas { get; set; }
        public DbSet<SsCharla> SsCharlas { get; set; }
        public DbSet<SsCharlaAsistencia> SsCharlaAsistencias { get; set; }
        public DbSet<SsCharlaArchivo> SsCharlaArchivos { get; set; }
        // ── Amonestaciones y Suspensiones ──────────────────────────────────────────
        public DbSet<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacionTipoSancion> SsomaAmonestacionTipoSanciones => Set<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacionTipoSancion>();
        public DbSet<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacionInfraccionTipo> SsomaAmonestacionInfraccionTipos => Set<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacionInfraccionTipo>();
        public DbSet<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacion> SsomaAmonestaciones => Set<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacion>();
        public DbSet<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacionFoto> SsomaAmonestacionFotos => Set<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacionFoto>();

        // ── Vecinos ──────────────────────────────────────────────────────────────
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoLote> VecinoLote => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoLote>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.Vecino> Vecino => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.Vecino>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoColindancia> VecinoColindancia => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoColindancia>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoTipoConstruccion> VecinoTipoConstruccion => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoTipoConstruccion>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoUso> VecinoUso => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoUso>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRelacionTipo> VecinoRelacionTipo => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRelacionTipo>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoPersona> VecinoPersona => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoPersona>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoImagen> VecinoImagen => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoImagen>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoSolicitud> VecinoSolicitud => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoSolicitud>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoSolicitudEstado> VecinoSolicitudEstado => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoSolicitudEstado>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromiso> VecinoCompromiso => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromiso>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromisoEntregable> VecinoCompromisoEntregable => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromisoEntregable>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromisoEstado> VecinoCompromisoEstado => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromisoEstado>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoEntregableTipo> VecinoEntregableTipo => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoEntregableTipo>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoEntregableEstado> VecinoEntregableEstado => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoEntregableEstado>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromisoNormativa> VecinoCompromisoNormativa => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoCompromisoNormativa>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoLimpiezaTipo> VecinoLimpiezaTipo => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoLimpiezaTipo>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoLimpieza> VecinoLimpieza => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoLimpieza>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlTipo> VecinoLicenciaControlTipo => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlTipo>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlEstado> VecinoLicenciaControlEstado => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlEstado>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControl> VecinoLicenciaControl => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControl>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlHistorial> VecinoLicenciaControlHistorial => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlHistorial>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlDestinatario> VecinoLicenciaControlDestinatario => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlDestinatario>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlRecordatorio> VecinoLicenciaControlRecordatorio => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlRecordatorio>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlVisita> VecinoLicenciaControlVisita => Set<Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models.VecinoLicenciaControlVisita>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models.ProjectCroquis> ProjectCroquis => Set<Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models.ProjectCroquis>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models.ProjectCroquisLote> ProjectCroquisLote => Set<Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models.ProjectCroquisLote>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisitoTipo> VecinoRequisitoTipo => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisitoTipo>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisitoEstado> VecinoRequisitoEstado => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisitoEstado>();
        public DbSet<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisito> VecinoRequisito => Set<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisito>();

        // ── Actas de Reunión (Unidad de Proyectos) ──────────────────────
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.Reunion> Reunion => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.Reunion>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionEstado> ReunionEstado => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionEstado>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTema> ReunionTema => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTema>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTemaPuesto> ReunionTemaPuesto => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTemaPuesto>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTemaRegla> ReunionTemaRegla => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTemaRegla>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTemaReglaWorkerExcluido> ReunionTemaReglaWorkerExcluido => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionTemaReglaWorkerExcluido>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionReprogramacion> ReunionReprogramacion => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionReprogramacion>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionParticipante> ReunionParticipante => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionParticipante>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAcuerdo> ReunionAcuerdo => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAcuerdo>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAcuerdoEstado> ReunionAcuerdoEstado => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAcuerdoEstado>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAcuerdoResponsable> ReunionAcuerdoResponsable => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAcuerdoResponsable>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionArchivo> ReunionArchivo => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionArchivo>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionFolder> ReunionFolder => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionFolder>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAgendaItem> ReunionAgendaItem => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionAgendaItem>();
        public DbSet<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionRecordatorioLog> ReunionRecordatorioLog => Set<Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models.ReunionRecordatorioLog>();

        // ── Salud Ocupacional: Tópico, Accidentes, Descansos ──────────
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.TopicoAtencion>     SsTopicoAtencion     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.TopicoAtencion>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.TopicoTipoAtencion> SsTopicoTipoAtencion => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.TopicoTipoAtencion>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAccidenteTrabajo>     SsAccidenteTrabajo     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAccidenteTrabajo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAccidenteSeguimiento> SsAccidenteSeguimiento => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAccidenteSeguimiento>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoMedico>          SsDescansoMedico          => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoMedico>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoMotivo>          SsDescansoMotivo          => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoMotivo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoTipo>            SsDescansoTipo            => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoTipo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoMedicoAdjunto>   SsDescansoMedicoAdjunto   => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoMedicoAdjunto>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoSeguimiento>    SsDescansoSeguimiento     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoSeguimiento>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoCarpeta>       SsDescansoCarpeta         => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoCarpeta>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoCorreoConfig>  SsDescansoCorreoConfig    => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoCorreoConfig>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoCaso>          SsDescansoCaso            => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsDescansoCaso>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsSeguimientoTipo>       SsSeguimientoTipo         => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsSeguimientoTipo>();
        public DbSet<Cie10Catalogo>                                                                              Cie10Catalogo             => Set<Cie10Catalogo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoTipo>          SsEmoCorreoTipo           => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoTipo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoDestinatario>  SsEmoCorreoDestinatario   => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoDestinatario>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoEvento>        SsEmoCorreoEvento         => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoEvento>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoPerfil>        SsEmoCorreoPerfil         => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoPerfil>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoRegla>         SsEmoCorreoRegla          => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEmoCorreoRegla>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsTopicoEvolucion>        SsTopicoEvolucion         => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsTopicoEvolucion>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCasoSocial>            SsCasoSocial            => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCasoSocial>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCasoSocialSeguimiento> SsCasoSocialSeguimiento => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCasoSocialSeguimiento>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCitaTipo>       SsCitaTipo       => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCitaTipo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEquipoTipo>     SsEquipoTipo     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEquipoTipo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAltaTipo>       SsAltaTipo       => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAltaTipo>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsSctrEstado>     SsSctrEstado     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsSctrEstado>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAlertaSsoma>   SsAlertaSsoma    => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAlertaSsoma>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCitaMedica>     SsCitaMedica     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsCitaMedica>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEquipoPrestado> SsEquipoPrestado => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsEquipoPrestado>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAltaMedica>     SsAltaMedica     => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsAltaMedica>();
        public DbSet<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsSctrGestion>       SsSctrGestion       => Set<Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models.SsSctrGestion>();

        // Amonestaciones e Inhabilitaciones
        public DbSet<SsomaInhabilitacion> SsomaInhabilitaciones => Set<SsomaInhabilitacion>();
        public DbSet<SsomaEscuelita> SsomaEscuelitas => Set<SsomaEscuelita>();
        public DbSet<Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models.SsomaAccidenteTrabajador> SsomaAccidenteTrabajador => Set<Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models.SsomaAccidenteTrabajador>();

        // Checklist SSOMA
        public DbSet<SsChecklistPartida> SsChecklistPartida => Set<SsChecklistPartida>();
        public DbSet<SsChecklistPlantilla> SsChecklistPlantilla => Set<SsChecklistPlantilla>();
        public DbSet<SsChecklistPlantillaItem> SsChecklistPlantillaItem => Set<SsChecklistPlantillaItem>();
        public DbSet<SsChecklistPlantillaItemImagen> SsChecklistPlantillaItemImagen => Set<SsChecklistPlantillaItemImagen>();
        public DbSet<SsChecklistProyecto> SsChecklistProyecto => Set<SsChecklistProyecto>();
        public DbSet<SsChecklistProyectoItem> SsChecklistProyectoItem => Set<SsChecklistProyectoItem>();

        // Activos Rotativos SSOMA
        public DbSet<Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models.SsActivoRotativoMaterial> SsActivoRotativoMaterial => Set<Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models.SsActivoRotativoMaterial>();
        public DbSet<Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models.SsActivoRotativo> SsActivoRotativo => Set<Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models.SsActivoRotativo>();
        public DbSet<Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models.SsActivoRotativoMovimiento> SsActivoRotativoMovimiento => Set<Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models.SsActivoRotativoMovimiento>();

        // EPP — Catálogo Autorizado (SSOMA + Logística)
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppCategoria> SsEppCategoria => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppCategoria>();
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppFamilia> SsEppFamilia => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppFamilia>();
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppItem> SsEppItem => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppItem>();
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppModelo> SsEppModelo => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppModelo>();
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppAuditoria> SsEppAuditoria => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppAuditoria>();
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppPedido> SsEppPedido => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppPedido>();
        public DbSet<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppPedidoLinea> SsEppPedidoLinea => Set<Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models.SsEppPedidoLinea>();

        // Cumplimiento SSOMA
        public DbSet<Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models.SsCumplimientoActividad> SsCumplimientoActividad => Set<Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models.SsCumplimientoActividad>();
        public DbSet<Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models.SsCumplimientoRegistro> SsCumplimientoRegistro => Set<Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models.SsCumplimientoRegistro>();

        // Habilitación de proyectos para SSOMA
        public DbSet<Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Infrastructure.Models.SsProyectoHabilitado> SsProyectoHabilitado => Set<Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Infrastructure.Models.SsProyectoHabilitado>();

        // ── Presupuesto de Materiales SSOMA ───────────────────────────────────
        public DbSet<SsMaterialTipo> SsMaterialTipo => Set<SsMaterialTipo>();
        public DbSet<SsMaterialHito> SsMaterialHito => Set<SsMaterialHito>();
        public DbSet<SsMaterialFamilia> SsMaterialFamilia => Set<SsMaterialFamilia>();
        public DbSet<SsMaterialItem> SsMaterialItem => Set<SsMaterialItem>();
        public DbSet<SsMaterialAlias> SsMaterialAlias => Set<SsMaterialAlias>();
        public DbSet<SsConsumoCarga> SsConsumoCarga => Set<SsConsumoCarga>();
        public DbSet<SsConsumoLinea> SsConsumoLinea => Set<SsConsumoLinea>();
        public DbSet<SsHhCarga> SsHhCarga => Set<SsHhCarga>();
        public DbSet<SsHhCargaLinea> SsHhCargaLinea => Set<SsHhCargaLinea>();
        public DbSet<SsRatioProyecto> SsRatioProyecto => Set<SsRatioProyecto>();
        public DbSet<SsPresupuesto> SsPresupuesto => Set<SsPresupuesto>();
        public DbSet<SsPresupuestoDetalle> SsPresupuestoDetalle => Set<SsPresupuestoDetalle>();
        public DbSet<SsPresupuestoSeleccionRatio> SsPresupuestoSeleccionRatio => Set<SsPresupuestoSeleccionRatio>();
        public DbSet<SsPresupuestoItemMetrado> SsPresupuestoItemMetrado => Set<SsPresupuestoItemMetrado>();
        public DbSet<SsPresupuestoPersonalHito> SsPresupuestoPersonalHito => Set<SsPresupuestoPersonalHito>();
        public DbSet<SsControlSemana> SsControlSemana => Set<SsControlSemana>();
        public DbSet<SsControlSemanaLinea> SsControlSemanaLinea => Set<SsControlSemanaLinea>();
        public DbSet<SsKit> SsKit => Set<SsKit>();
        public DbSet<SsKitItem> SsKitItem => Set<SsKitItem>();

        public DbSet<SsResiduoTipo> SsResiduoTipo => Set<SsResiduoTipo>();
        public DbSet<SsResiduoTipoFactor> SsResiduoTipoFactor => Set<SsResiduoTipoFactor>();
        public DbSet<SsResiduoEoRs> SsResiduoEoRs => Set<SsResiduoEoRs>();
        public DbSet<SsResiduoEoRsDocumento> SsResiduoEoRsDocumento => Set<SsResiduoEoRsDocumento>();
        public DbSet<SsResiduoAutorizacionDme> SsResiduoAutorizacionDme => Set<SsResiduoAutorizacionDme>();
        public DbSet<SsResiduoViaje> SsResiduoViaje => Set<SsResiduoViaje>();
        public DbSet<SsResiduoDeclaracion> SsResiduoDeclaracion => Set<SsResiduoDeclaracion>();
        public DbSet<SsResiduoDeclaracionDetalle> SsResiduoDeclaracionDetalle => Set<SsResiduoDeclaracionDetalle>();
        public DbSet<SsResiduoDeclaracionEoRs> SsResiduoDeclaracionEoRs => Set<SsResiduoDeclaracionEoRs>();
        public DbSet<SsResiduoConstancia> SsResiduoConstancia => Set<SsResiduoConstancia>();
        public DbSet<SsResiduoConstanciaFinal> SsResiduoConstanciaFinal => Set<SsResiduoConstanciaFinal>();
        public DbSet<SsResiduoDocumentoReferencia> SsResiduoDocumentoReferencia => Set<SsResiduoDocumentoReferencia>();

        public DbSet<AcObservacion> AcObservaciones => Set<AcObservacion>();
        public DbSet<AcObservacionFoto> AcObservacionFotos => Set<AcObservacionFoto>();
        public DbSet<AcCatalogoItem> AcCatalogoItems => Set<AcCatalogoItem>();

        public DbSet<AcRevision> AcRevisiones => Set<AcRevision>();
        public DbSet<AcRevisionObservacion> AcRevisionObservaciones => Set<AcRevisionObservacion>();
        public DbSet<AcRevisionObservacionFoto> AcRevisionObservacionFotos => Set<AcRevisionObservacionFoto>();
        // ── Costos (Arquitectura Comercial): conteo semanal + proyección mensual ──
        public DbSet<AcCostoRegistro> AcCostoRegistros => Set<AcCostoRegistro>();
        public DbSet<AcCostoProyeccion> AcCostoProyecciones => Set<AcCostoProyeccion>();
        public DbSet<AcCostoMetaMensual> AcCostoMetaMensuales => Set<AcCostoMetaMensual>();
        public DbSet<AcCostoPresupuesto> AcCostoPresupuestos => Set<AcCostoPresupuesto>();
        public DbSet<AcCostoCierre> AcCostoCierres => Set<AcCostoCierre>();
        // ── Almacén (Logística): módulo nuevo e independiente ─────────────
        public DbSet<AlmacenMaterial> AlmacenMateriales => Set<AlmacenMaterial>();
        public DbSet<AlmacenMovimiento> AlmacenMovimientos => Set<AlmacenMovimiento>();
        public DbSet<AlmacenOrdenCompra> AlmacenOrdenesCompra => Set<AlmacenOrdenCompra>();

        public DbSet<DecolectaToken> DecolectaToken => Set<DecolectaToken>();

        // ── Gestión GTH · Reclutamiento ────────────────────────────────────────
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPuesto> GthPuesto => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPuesto>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoRequerimiento> GthTipoRequerimiento => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoRequerimiento>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoRequerimiento> GthEstadoRequerimiento => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoRequerimiento>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPrioridad> GthPrioridad => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPrioridad>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSolicitud> GthSolicitud => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSolicitud>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimiento> GthRequerimiento => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimiento>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimientoEstadoHistorial> GthRequerimientoEstadoHistorial => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimientoEstadoHistorial>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSustentoFolder> GthSustentoFolder => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSustentoFolder>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoDestinatario> GthCorreoDestinatario => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoDestinatario>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoTipo> GthCorreoTipo => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoTipo>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Shared.Models.GthResponsableProceso> GthResponsableProceso => Set<Abril_Backend.Features.GestionGthModule.Shared.Models.GthResponsableProceso>();
        // Configuración propia de qué áreas ve cada trabajador en Solicitud de Personal.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSolicitudPersonalVisibilidadArea> GthSolicitudPersonalVisibilidadArea => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSolicitudPersonalVisibilidadArea>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoProceso> GthTipoProceso => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoProceso>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCanalPublicacion> GthCanalPublicacion => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCanalPublicacion>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimientoCanal> GthRequerimientoCanal => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimientoCanal>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEstado> GthCandidatoEstado => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEstado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato> GthCandidato => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>();
        // Portafolio/anexos del candidato de la long list (N archivos además del CV).
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoAnexo> GthCandidatoAnexo => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoAnexo>();
        // Programación de entrevistas (fecha/hora/lugar por candidato) + catálogo de lugares.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthLugarEntrevista> GthLugarEntrevista => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthLugarEntrevista>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevista> GthEntrevista => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevista>();
        // Respuesta del candidato a su citación (CONFIRMADA / RECHAZADA), desde el correo.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevistaRespuesta> GthEntrevistaRespuesta => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevistaRespuesta>();
        // Evaluación de la entrevista (puntajes + comentarios del informe de finalista) + catálogo de resultados.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoResultado> GthCandidatoResultado => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoResultado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacion> GthCandidatoEvaluacion => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacion>();
        // Archivos del informe de la entrevista (informe final y evaluación de conocimientos) + su catálogo.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEvaluacionArchivoTipo> GthEvaluacionArchivoTipo => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEvaluacionArchivoTipo>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacionArchivo> GthCandidatoEvaluacionArchivo => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacionArchivo>();
        // Formulario de información del postulante (público por token) + sus catálogos.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormulario> GthPostulanteFormulario => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormulario>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormularioEstado> GthPostulanteFormularioEstado => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormularioEstado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoCivil> GthEstadoCivil => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoCivil>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoDocumento> GthTipoDocumento => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoDocumento>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthGradoAcademico> GthGradoAcademico => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthGradoAcademico>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDisponibilidad> GthDisponibilidad => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDisponibilidad>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthMotivoCese> GthMotivoCese => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthMotivoCese>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthUniversidad> GthUniversidad => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthUniversidad>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDistrito> GthDistrito => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDistrito>();
        // Aprobación de la solicitud de personal (gerente del área + GG) + su catálogo y detalle.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgEstado> GthAprobacionGgEstado => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgEstado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGg> GthAprobacionGg => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGg>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgDetalle> GthAprobacionGgDetalle => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgDetalle>();

        // ── Gestión GTH · Carta oferta (último paso de Reclutamiento) ───────────
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOferta> GthCartaOferta => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOferta>();

        // Las viñetas del bloque de condiciones de contrato que imprime la carta, una fila por viñeta.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOfertaCondicion> GthCartaOfertaCondicion => Set<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOfertaCondicion>();

        // Biblioteca del file de colaboradores: la abre la carta oferta y la sigue usando Onboarding,
        // así que el modelo vive en el Shared/ del módulo.
        public DbSet<Abril_Backend.Features.GestionGthModule.Shared.Models.GthCartaOfertaFolder> GthCartaOfertaFolder => Set<Abril_Backend.Features.GestionGthModule.Shared.Models.GthCartaOfertaFolder>();

        // ── Gestión GTH · Onboarding (la fase que sigue a Reclutamiento) ────────
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFase> GthOnboardingFase => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFase>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingEstado> GthOnboardingEstado => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingEstado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboarding> GthOnboarding => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboarding>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingActividad> GthOnboardingActividad => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingActividad>();
        // Formulario «Nuevos Talentos» del colaborador (público por token) + sus catálogos propios.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormulario> GthOnboardingFormulario => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormulario>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormularioEstado> GthOnboardingFormularioEstado => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormularioEstado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingUbicacion> GthOnboardingUbicacion => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingUbicacion>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthRentaQuinta> GthRentaQuinta => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthRentaQuinta>();
        // Tallas: la de calzado nace con este formulario; la de camisa (`talla`) existía desde la
        // data maestra (person.talla_id) pero nadie la leía todavía, así que no estaba mapeada.
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.TallaCalzado> TallaCalzado => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.TallaCalzado>();
        public DbSet<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.Talla> Talla => Set<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.Talla>();

        // ── Centro de aprendizaje y guías (videos-guía por área/módulo) ──────────
        public DbSet<LearningSurface> LearningSurface => Set<LearningSurface>();
        public DbSet<LearningCategory> LearningCategory => Set<LearningCategory>();
        public DbSet<LearningVideo> LearningVideo => Set<LearningVideo>();
        public DbSet<LearningCategoryRole> LearningCategoryRole => Set<LearningCategoryRole>();
        public DbSet<LearningVideoFolder> LearningVideoFolder => Set<LearningVideoFolder>();

        // ── Planeamiento BIM ──────────────────────────────────────────────────
        public DbSet<BimMacroActividad> BimMacroActividad => Set<BimMacroActividad>();
        public DbSet<BimActividad> BimActividad => Set<BimActividad>();
        public DbSet<BimCausaNoCumplimiento> BimCausaNoCumplimiento => Set<BimCausaNoCumplimiento>();
        public DbSet<BimFase> BimFase => Set<BimFase>();
        public DbSet<BimProyectoTorre> BimProyectoTorre => Set<BimProyectoTorre>();
        public DbSet<BimTorreNivel> BimTorreNivel => Set<BimTorreNivel>();
        public DbSet<BimZonaSector> BimZonaSector => Set<BimZonaSector>();
        public DbSet<BimProyectoFase> BimProyectoFase => Set<BimProyectoFase>();
        public DbSet<BimRegistroDiario> BimRegistroDiario => Set<BimRegistroDiario>();
        public DbSet<BimEvidenciaFoto> BimEvidenciaFoto => Set<BimEvidenciaFoto>();
        public DbSet<BimRestriccion> BimRestriccion => Set<BimRestriccion>();
        public DbSet<BimMetaSemanal> BimMetaSemanal => Set<BimMetaSemanal>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (_provider == "PostgreSQL")
            {
                ConfigurePostgreSQL(modelBuilder);
            }
            else
            {
                ConfigureSqlServer(modelBuilder);
            }

            base.OnModelCreating(modelBuilder);

            // ss_petar_izaje_grua es 1:1 con ss_petar (PK compartida) — solo existe la fila cuando
            // el tipo de PETAR es izaje con grúa (SSO-FO-043), ver SsPetarIzajeGrua.
            modelBuilder.Entity<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarIzajeGrua>(e =>
            {
                e.HasKey(x => x.PetarId);
                e.HasOne(x => x.Petar)
                    .WithOne(p => p.IzajeGrua)
                    .HasForeignKey<Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models.SsPetarIzajeGrua>(x => x.PetarId);
            });

            // Las fichas eliminadas de `workers` no existen para nadie. Va como filtro
            // global y no repetido en cada consulta porque son ~1600 lecturas de
            // ctx.Worker repartidas en 147 archivos: cualquier otra forma se olvida en
            // alguna, y una ficha duplicada que reaparece en UNA pantalla es peor que no
            // haberla dado de baja.
            //
            // Para leer el historial que quedó colgando de una ficha fusionada (EMOs,
            // inducciones, amonestaciones: ver workers_ficha_fusionada) hay que pedirlo
            // explícitamente con IgnoreQueryFilters().
            modelBuilder.Entity<Worker>().HasQueryFilter(w => w.State);

            // Una captura de movilidad eliminada (al subsanar una rendición observada) no existe
            // para nadie: no se lista, no se imprime en la planilla y no suma al importe rendido.
            // Va como filtro global por el mismo motivo que el de Worker: la tabla se lee desde
            // siete lugares (tres repositorios de salidas y ImporteRendidoLoader) y una captura
            // borrada que reaparece en UNO solo descuadraría el monto contra la planilla firmada.
            modelBuilder.Entity<GaSolicitudCaptura>().HasQueryFilter(c => c.State);

            modelBuilder.Entity<Person>()
                .HasOne(p => p.User)
                .WithOne(u => u.Person)
                .HasForeignKey<Person>(p => p.UserId);

            // Relaciones declaradas a propósito: sin inversa en Person/Project, la convención
            // podría inventar una FK sombra.
            modelBuilder.Entity<Propietario>(e =>
            {
                e.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId);
                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
            });

            // Centro de aprendizaje: llave compuesta de la relación N:M categoría↔rol.
            modelBuilder.Entity<LearningCategoryRole>()
                .HasKey(x => new { x.LearningCategoryId, x.RoleId });

            modelBuilder.Entity<SsProgramacionEmo>()
                .Property(e => e.Origen)
                .HasDefaultValue("Manual");

            // Un trabajador no puede tener dos programaciones "activas" (no terminales) del
            // mismo tipo de EMO a la vez. Create() y el auto-programador ya validan esto en
            // memoria antes de insertar, pero eso no cierra la ventana de carrera entre el
            // chequeo y el INSERT (dos requests casi simultáneos — cron + manual, o doble
            // click — pueden pasar ambos la validación antes de que el primero confirme). Este
            // índice es la garantía real, igual que ux_tareo_worker_fecha_tipo en
            // ArquitecturaComercialTareoRepository.
            modelBuilder.Entity<SsProgramacionEmo>()
                .HasIndex(p => new { p.WorkerId, p.TipoEmoId })
                .IsUnique()
                .HasDatabaseName("ux_programacion_emo_worker_tipo_activa")
                .HasFilter("state = true AND estado NOT IN ('Completado', 'Cancelado', 'Rechazado por Clínica', 'No se presentó')");

            modelBuilder.Entity<ResidentReportIncidenceImage>()
                .HasOne(i => i.ResidentReportIncidence)
                .WithMany(r => r.Images)
                .HasForeignKey(i => i.ResidentReportIncidenceId);

            modelBuilder.Entity<ResidentReportIncidence>()
                .HasOne(r => r.Project)
                .WithMany(p => p.Incidences)
                .HasForeignKey(r => r.ProjectId);

            modelBuilder.Entity<ResidentReportResponse>()
                .HasOne(r => r.ResidentReportIncidence)
                .WithMany(p => p.ResidentReportResponses)
                .HasForeignKey(r => r.ResidentReportIncidenceId);

            modelBuilder.Entity<ResidentReportResponseImage>()
                .HasOne(r => r.ResidentReportResponse)
                .WithMany(s => s.Images)
                .HasForeignKey(r => r.ResidentReportResponseId);

            modelBuilder.Entity<ResidentReportIncidence>()
                .HasOne(r => r.StateNavigation)
                .WithMany(s => s.ResidentReportIncidences)
                .HasForeignKey(r => r.StateId);

            modelBuilder.Entity<Contractor>()
                .HasOne(c => c.Contributor)
                .WithMany()
                .HasForeignKey(c => c.ContributorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contractor>()
                .HasOne(c => c.ContractorState)
                .WithMany()
                .HasForeignKey(c => c.ContractorStateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contractor>()
                .HasIndex(c => c.ContributorId)
                .IsUnique();

            // ── Contabilidad: Facturas ───────────────────────────────────────
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Contributor)
                .WithMany()
                .HasForeignKey(i => i.ContributorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.InvoicePaymentForm)
                .WithMany()
                .HasForeignKey(i => i.InvoicePaymentFormId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.InvoiceFolder)
                .WithMany()
                .HasForeignKey(i => i.InvoiceFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.AbrilContributor)
                .WithMany()
                .HasForeignKey(i => i.AbrilContributorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Currency)
                .WithMany()
                .HasForeignKey(i => i.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.InvoiceDocumentType)
                .WithMany()
                .HasForeignKey(i => i.InvoiceDocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.InvoiceStatus)
                .WithMany()
                .HasForeignKey(i => i.InvoiceStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.InvoiceObservationReason)
                .WithMany()
                .HasForeignKey(i => i.InvoiceObservationReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractorEmail>()
                .HasOne(e => e.Contractor)
                .WithMany(c => c.Emails)
                .HasForeignKey(e => e.ContractorId);

            modelBuilder.Entity<ContractorEmail>()
                .HasOne(e => e.PersonType)
                .WithMany()
                .HasForeignKey(e => e.ContractorPersonTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractorUser>()
                .HasOne(cu => cu.Contractor)
                .WithMany(c => c.Users)
                .HasForeignKey(cu => cu.ContractorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractorUser>()
                .HasOne(cu => cu.User)
                .WithMany()
                .HasForeignKey(cu => cu.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Solicitud de actualización de datos de contratista ──────────
            modelBuilder.Entity<ContractorUpdateRequest>()
                .HasMany(r => r.Emails)
                .WithOne()
                .HasForeignKey(e => e.ContractorUpdateRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectSubContractorQuotationFile>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.QuotationFiles)
                .HasForeignKey(f => f.ProjectSubContractorId);

            modelBuilder.Entity<ProjectSubContractorComparativeFile>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.ComparativeFiles)
                .HasForeignKey(f => f.ProjectSubContractorId);

            /*modelBuilder.Entity<ProjectSubContractorPackage>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.Packages)
                .HasForeignKey(f => f.ProjectSubContractorId);*/

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Project)
                .WithMany()
                .HasForeignKey(s => s.ProjectId);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Contractor)
                .WithMany()
                .HasForeignKey(s => s.ContractorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Contributor)
                .WithMany()
                .HasForeignKey(p => p.ContributorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Coordinador administrativo del proyecto. Se declara explícita (y no por
            // convención) porque Worker no tiene navegación de vuelta hacia Project: sin
            // esto EF podría inventarse una FK sombra en vez de usar workers_coord_admin_id.
            modelBuilder.Entity<Project>()
                .HasOne(p => p.CoordAdmin)
                .WithMany()
                .HasForeignKey(p => p.WorkersCoordAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tipo y ciclo de vida del proyecto: catálogos con id fijo. Explícitas por lo mismo que
            // CoordAdmin (las navegaciones no se llaman como la FK y los catálogos no tienen colección
            // de vuelta).
            modelBuilder.Entity<ProjectTipo>().ToTable("project_tipo");
            modelBuilder.Entity<ProjectCicloVida>().ToTable("project_ciclo_vida");
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Tipo)
                .WithMany()
                .HasForeignKey(p => p.ProjectTipoId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Project>()
                .HasOne(p => p.CicloVida)
                .WithMany()
                .HasForeignKey(p => p.ProjectCicloVidaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Bitácora del residente: FK explícita al proyecto por lo mismo que CoordAdmin (Project no
            // tiene la colección de vuelta). Los workers_id_* quedan como columnas simples.
            modelBuilder.Entity<ProjectResidenteHistorial>().ToTable("project_residente_historial");
            modelBuilder.Entity<ProjectResidenteHistorial>()
                .HasOne(h => h.Project)
                .WithMany()
                .HasForeignKey(h => h.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Contract)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorContractId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.SummarySheet)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorSummarySheetId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Budget)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorBudgetId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Schedule)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorScheduleId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.AttachedQuotation)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorAttachedQuotationId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.ServiceOrder)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorServiceOrderId)
                .IsRequired(false);

            // FK: cada tabla de documento → project_sub_contractor_file_status
            modelBuilder.Entity<ProjectSubContractorContract>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorSummarySheet>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorBudget>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorSchedule>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorAttachedQuotation>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorServiceOrder>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.PromissoryNote)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorPromissoryNoteId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Package)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorPackageId)
                .IsRequired(false);

            // Era la única navegación de documento que se resolvía por convención. Desde que las
            // tablas de documento guardan project_sub_contractor_id, la convención ve un candidato
            // de FK en ambos lados, se rinde e inventa una FK sombra
            // (instructivo_project_sub_contractor_instructivo_id) que no existe en la BD.
            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Instructivo)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorInstructivoId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.NonConformingOutput)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorNonConformingOutputId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.ToleranceChart)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorToleranceChartId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.FichaTecnica)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorFichaTecnicaId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Anexo)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorAnexoId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorNonConformingOutput>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorToleranceChart>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorFichaTecnica>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorAnexo>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<StaffProjectEmail>()
                .HasOne(s => s.EmailType)
                .WithMany()
                .HasForeignKey(s => s.StaffProjectEmailTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Lecciones aprendidas / Áreas (wip/lecciones-aprendidas) ─────
            // ScopeItem: self-referential parent/children
            modelBuilder.Entity<ScopeItem>()
                .HasOne(s => s.Parent)
                .WithMany(s => s.Children)
                .HasForeignKey(s => s.ScopeItemParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ScopeTemplateItem: self-referential parent/children
            modelBuilder.Entity<ScopeTemplateItem>()
                .HasOne(s => s.Parent)
                .WithMany(s => s.Children)
                .HasForeignKey(s => s.ScopeTemplateItemParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // AreaItem: FK a AreaType (sin jerarquía — eso vive en AreaScope)
            modelBuilder.Entity<AreaItem>()
                .HasOne(a => a.AreaType)
                .WithMany()
                .HasForeignKey(a => a.AreaTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Holiday: FK a HolidayType
            modelBuilder.Entity<Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Models.Holiday>()
                .HasOne(h => h.HolidayType)
                .WithMany()
                .HasForeignKey(h => h.HolidayTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // AreaScope: árbol con FK a AreaItem + self-referential parent
            modelBuilder.Entity<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>()
                .HasOne(s => s.AreaItem)
                .WithMany()
                .HasForeignKey(s => s.AreaItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>()
                .HasOne(s => s.Parent)
                .WithMany()
                .HasForeignKey(s => s.AreaScopeParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Las dos áreas del puesto: la que puede pedirlo y a la que entra el postulante.
            // Restrict en ambas: el área nunca se borra de verdad (soft delete), así que un
            // cascade no tendría a quién aplicarse.
            modelBuilder.Entity<Puesto>()
                .HasOne(x => x.AreaSolicitanteScope)
                .WithMany()
                .HasForeignKey(x => x.AreaSolicitanteScopeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Puesto>()
                .HasOne(x => x.AreaDestinoScope)
                .WithMany()
                .HasForeignKey(x => x.AreaDestinoScopeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── master ─────────────────────────────────────────────────────
            modelBuilder.Entity<SsClinicaUsuario>().HasKey(x => x.ClinicaUsuarioId);
            modelBuilder.Entity<SsClinicaToken>().HasKey(x => x.TokenId);
            modelBuilder.Entity<SsClinicaAuditoria>().HasKey(x => x.AuditoriaId);

            modelBuilder.Entity<WorkerProyecto>()
                .HasOne(wp => wp.Worker)
                .WithMany()
                .HasForeignKey(wp => wp.WorkerId);

            // ga_consolidado_s10: el "S10" del nombre de la clase no lo separa la convencion
            // snake_case (los digitos no cuentan como corte), pero el mapeo se fija a mano igual
            // para que quede explicito y no dependa de ese detalle de la convencion.
            modelBuilder.Entity<GaConsolidadoS10>().ToTable("ga_consolidado_s10");
            // La tabla puente del consolidado (qué planillas cubre): mismo cuidado con el "S10",
            // tabla y FK se fijan a mano.
            modelBuilder.Entity<GaConsolidadoS10Rendicion>().ToTable("ga_consolidado_s10_rendicion");
            modelBuilder.Entity<GaConsolidadoS10Rendicion>()
                .Property(x => x.ConsolidadoS10Id).HasColumnName("consolidado_s10_id");
            // La planilla grupal preparada y su puente: sin dígitos en el nombre, la convención
            // alcanza, pero se fijan igual que las del consolidado para que se lean juntas.
            modelBuilder.Entity<GaPlanillaGrupal>().ToTable("ga_planilla_grupal");
            modelBuilder.Entity<GaPlanillaGrupalRendicion>().ToTable("ga_planilla_grupal_rendicion");
            // Mismo caso que el consolidado: se fija a mano para no depender de cómo la
            // convención snake_case parte el "S10".
            modelBuilder.Entity<GaCorreccionS10>().ToTable("ga_correccion_s10");
            modelBuilder.Entity<GaCorreccionS10>()
                .Property(x => x.ConsolidadoS10Id).HasColumnName("consolidado_s10_id");

            modelBuilder.Entity<SsomaPasoCategoria>().ToTable("ssoma_paso_categoria");
            modelBuilder.Entity<SsomaPaso>().ToTable("ssoma_paso");
            modelBuilder.Entity<SsomaPasoActividad>().ToTable("ssoma_paso_actividad");
            modelBuilder.Entity<SsomaPasoEjecucion>().ToTable("ssoma_paso_ejecucion");
            modelBuilder.Entity<SsomaPaso>()
                .HasMany(x => x.Actividades).WithOne(x => x.Paso).HasForeignKey(x => x.PasoId);
            modelBuilder.Entity<SsomaPasoActividad>()
                .HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId);
            modelBuilder.Entity<SsomaPasoActividad>()
                .HasMany(x => x.Ejecuciones).WithOne(x => x.Actividad).HasForeignKey(x => x.ActividadId);
            modelBuilder.Entity<SsomaPasoEjecucionArchivo>().ToTable("ssoma_paso_ejecucion_archivo");
            modelBuilder.Entity<SsomaPasoEjecucion>()
                .HasMany(x => x.Archivos).WithOne(x => x.Ejecucion).HasForeignKey(x => x.EjecucionId);

            // ── RAC — tablas, relaciones y defaults ──────────────────────────
            modelBuilder.Entity<SsomaRacCategoria>().ToTable("ssoma_rac_categoria");
            modelBuilder.Entity<SsomaRacInfraccion>().ToTable("ssoma_rac_infraccion");
            modelBuilder.Entity<SsomaUitAnio>().ToTable("ssoma_uit_anio");
            modelBuilder.Entity<SsomaRac>().ToTable("ssoma_rac");
            modelBuilder.Entity<SsomaRacFoto>().ToTable("ssoma_rac_foto");

            modelBuilder.Entity<SsomaRac>()
                .HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).IsRequired();
            modelBuilder.Entity<SsomaRacFoto>()
                .HasOne(x => x.Rac).WithMany(x => x.Fotos).HasForeignKey(x => x.RacId);

            modelBuilder.Entity<SsomaRac>()
                .Property(x => x.Estado).HasDefaultValue("Abierto");
            modelBuilder.Entity<SsomaRacFoto>()
                .Property(x => x.Tipo).HasDefaultValue("Hallazgo");
            modelBuilder.Entity<SsomaRacFoto>()
                .Property(x => x.Orden).HasDefaultValue(1);

            // ── Penalidad — independiente de RAC (ver PenalidadFeature) ──────
            modelBuilder.Entity<SsomaPenalidad>().ToTable("ssoma_penalidad");
            modelBuilder.Entity<SsomaPenalidadEstadoHistorial>().ToTable("ssoma_penalidad_estado_historial");
            modelBuilder.Entity<GestionPreviaEmpresa>().ToTable("ssoma_gestion_previa_empresa");

            modelBuilder.Entity<SsomaPenalidad>()
                .HasOne(x => x.Infraccion).WithMany().HasForeignKey(x => x.InfraccionId);
            modelBuilder.Entity<SsomaPenalidad>()
                .HasMany(x => x.Historial).WithOne(x => x.Penalidad).HasForeignKey(x => x.PenalidadId);
            modelBuilder.Entity<SsomaPenalidad>()
                .Property(x => x.Estado).HasDefaultValue("Registrada");

            // ── OPT — tablas y nombres explícitos ────────────────────────────
            modelBuilder.Entity<SsomaOpt>().ToTable("ssoma_opt");
            modelBuilder.Entity<SsomaOptTrabajador>().ToTable("ssoma_opt_trabajador");
            modelBuilder.Entity<SsomaPet>().ToTable("ssoma_pet");
            modelBuilder.Entity<SsomaPetVersion>().ToTable("ssoma_pet_version");
            modelBuilder.Entity<SsomaPetVersion>()
                .HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId);
            modelBuilder.Entity<SsomaPetVersion>()
                .HasIndex(x => new { x.PetId, x.NumeroVersion }).IsUnique();
            modelBuilder.Entity<SsomaPetPaso>().ToTable("ssoma_pet_paso");
            modelBuilder.Entity<SsomaPetPaso>()
                .HasOne(x => x.Pet).WithMany(p => p.Pasos).HasForeignKey(x => x.PetId);
            modelBuilder.Entity<SsomaPetPaso>()
                .HasOne(x => x.Parent).WithMany(x => x.Hijos).HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SsomaPetPasoImagen>().ToTable("ssoma_pet_paso_imagen");
            modelBuilder.Entity<SsomaPetPasoImagen>()
                .HasOne(x => x.Paso).WithMany(x => x.Imagenes).HasForeignKey(x => x.PasoId);
            modelBuilder.Entity<SsomaCatalogoItem>().ToTable("ssoma_catalogo_item");
            modelBuilder.Entity<SsomaPetItemSeleccionado>().ToTable("ssoma_pet_item_seleccionado");
            modelBuilder.Entity<SsomaPetItemSeleccionado>()
                .HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId);
            modelBuilder.Entity<SsomaPetItemSeleccionado>()
                .HasOne(x => x.CatalogoItem).WithMany().HasForeignKey(x => x.CatalogoItemId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SsomaPetSeccionTexto>().ToTable("ssoma_pet_seccion_texto");
            modelBuilder.Entity<SsomaPetSeccionTexto>()
                .HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId);
            modelBuilder.Entity<SsomaPetSeccionTexto>()
                .HasIndex(x => new { x.PetId, x.Seccion }).IsUnique();
            modelBuilder.Entity<SsomaPetFirma>().ToTable("ssoma_pet_firma");
            modelBuilder.Entity<SsomaPetFirma>()
                .HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId);
            modelBuilder.Entity<SsomaPetFirma>()
                .HasIndex(x => new { x.PetId, x.Rol }).IsUnique();
            modelBuilder.Entity<SsomaPetAnexo>().ToTable("ssoma_pet_anexo");
            modelBuilder.Entity<SsomaPetAnexo>()
                .HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId);
            modelBuilder.Entity<SsomaOptCriterioVerificacion>().ToTable("ssoma_opt_criterio_verificacion");
            modelBuilder.Entity<SsomaOptVerificacion>().ToTable("ssoma_opt_verificacion");
            modelBuilder.Entity<SsomaOptPaso>().ToTable("ssoma_opt_paso");

            modelBuilder.Entity<SsomaOpt>()
                .Property(x => x.Estado).HasDefaultValue("Completado");

            // ── Accidentes e Incidentes ───────────────────────────────────────
            modelBuilder.Entity<SsomaAccidenteIncidente>().ToTable("ss_accidente_incidente");
            modelBuilder.Entity<SsomaAccidenteDocumento>().ToTable("ss_accidente_documento")
                .Property(d => d.TamanioBytes).HasColumnName("tamanio_bytes");
            modelBuilder.Entity<SsomaEntregableTipo>().ToTable("ss_entregable_tipo");
            modelBuilder.Entity<SsomaEntregable>().ToTable("ss_entregable");
            modelBuilder.Entity<SsomaEntregableResponsable>().ToTable("ss_entregable_responsable");
            modelBuilder.Entity<SsomaEntregableArchivo>().ToTable("ss_entregable_archivo");
            modelBuilder.Entity<SsomaEntregableArchivo>()
                .HasOne<SsomaEntregable>()
                .WithMany(e => e.Archivos)
                .HasForeignKey(a => a.EntregableId)
                .HasConstraintName("fk_ss_entregable_archivo_entregable_id");
            modelBuilder.Entity<SsomaInvestigacionRm050>().ToTable("ss_investigacion_rm050");
            modelBuilder.Entity<SsomaAccionCorrectiva>().ToTable("ss_accion_correctiva");
            modelBuilder.Entity<SsomaAccionCorrectiva>()
                .HasOne<SsomaInvestigacionRm050>()
                .WithMany(i => i.AccionesCorrectivas)
                .HasForeignKey(a => a.InvestigacionId)
                .HasConstraintName("fk_ss_accion_correctiva_investigacion_id");
            // ── Inspecciones — tablas y nombres explícitos ────────────────────
            modelBuilder.Entity<SsomaInspeccionTipo>().ToTable("ssoma_inspeccion_tipo");
            modelBuilder.Entity<SsomaInspeccionChecklistItem>().ToTable("ssoma_inspeccion_checklist_item");
            modelBuilder.Entity<SsomaInspeccion>().ToTable("ssoma_inspeccion");
            modelBuilder.Entity<SsomaInspeccionRespuesta>().ToTable("ssoma_inspeccion_respuesta");
            modelBuilder.Entity<SsomaInspeccionHallazgo>().ToTable("ssoma_inspeccion_hallazgo");
            modelBuilder.Entity<SsomaInspeccionHallazgoFoto>().ToTable("ssoma_inspeccion_hallazgo_foto");
            modelBuilder.Entity<SsomaInspeccionFotoArea>().ToTable("ssoma_inspeccion_foto_area");
            modelBuilder.Entity<SsomaInspeccionParticipante>().ToTable("ssoma_inspeccion_participante");
            modelBuilder.Entity<SsomaOptFotoArea>().ToTable("ssoma_opt_foto_area");
            // ── Indicadores Proactivos ─────────────────────────────────────
            modelBuilder.Entity<SsomaProgInspeccionEmpresa>().ToTable("ssoma_prog_inspeccion_empresa");
            modelBuilder.Entity<SsomaProgInspeccionEmpresa>()
                .HasIndex(e => new { e.ProyectoId, e.EmpresaId, e.EmpresaTipo, e.Mes, e.Anio, e.InspeccionTipoId })
                .IsUnique();
            // ── Auditoría ATS ──────────────────────────────────────────────
            modelBuilder.Entity<SsomaAuditoriaAtsPregunta>().ToTable("ssoma_auditoria_ats_pregunta");
            modelBuilder.Entity<SsomaAuditoriaAts>().ToTable("ssoma_auditoria_ats");
            modelBuilder.Entity<SsomaAuditoriaAtsRespuesta>().ToTable("ssoma_auditoria_ats_respuesta")
                .HasIndex(r => new { r.AuditoriaId, r.PreguntaId }).IsUnique();
            modelBuilder.Entity<SsomaAuditoriaAtsFoto>().ToTable("ssoma_auditoria_ats_foto");

            // ── Costos (Arquitectura Comercial) ─────────────────────────────
            modelBuilder.Entity<AcCostoRegistro>()
                .HasIndex(r => new { r.ProyectoId, r.Anio, r.Mes, r.Semana, r.Partida }).IsUnique();
            modelBuilder.Entity<AcCostoProyeccion>()
                .HasIndex(p => new { p.ProyectoId, p.Anio, p.Mes, p.Partida }).IsUnique();
            modelBuilder.Entity<AcCostoMetaMensual>()
                .HasIndex(m => new { m.Anio, m.Mes }).IsUnique();
            modelBuilder.Entity<AcCostoPresupuesto>()
                .HasIndex(p => new { p.ProyectoId, p.Partida }).IsUnique();
            modelBuilder.Entity<AcCostoCierre>()
                .HasIndex(c => new { c.ProyectoId, c.Anio, c.Mes }).IsUnique();

            // ── Almacén (Logística) ──────────────────────────────────────────
            modelBuilder.Entity<AlmacenMaterial>().HasIndex(m => m.Codigo).IsUnique();
            modelBuilder.Entity<AlmacenMovimiento>().HasIndex(m => new { m.ProyectoId, m.MaterialId });
            modelBuilder.Entity<AlmacenOrdenCompra>().HasIndex(o => o.ProyectoId);
        }

        private void ConfigureSqlServer(ModelBuilder modelBuilder)
        {
        }

        private void ConfigurePostgreSQL(ModelBuilder modelBuilder)
        {
            // Mapea unaccent(text) de la extensión unaccent para búsquedas insensibles a tildes.
            modelBuilder
                .HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Unaccent), new[] { typeof(string) })!)
                .HasName("unaccent");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("app_user");
            });

            // person.sexo_id -> catálogo normalizado sexo (reemplaza la antigua columna de texto person.sexo).
            modelBuilder.Entity<Person>()
                .HasOne(p => p.Sexo)
                .WithMany()
                .HasForeignKey(p => p.SexoId);

            // FactorM3aTon snake-casea por convención a "factor_m3a_ton", pero la columna real
            // (Migrations_Manual/2026-09-22_ss_residuo_gestion.sql) es "factor_m3_a_ton".
            modelBuilder.Entity<Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models.SsResiduoTipoFactor>()
                .Property(f => f.FactorM3aTon).HasColumnName("factor_m3_a_ton");

            // Las columnas fecha/*_at de ac_revisiones y ac_revision_observaciones se crearon
            // como TIMESTAMP (sin zona horaria) en la migración manual, pero Npgsql por defecto
            // mapea DateTime a "timestamp with time zone" y exige Kind=Utc — sin este override
            // cualquier insert con Kind=Unspecified (el que llega de [FromForm]) rompe con
            // "Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with
            // time zone'". Esto hace que el tipo de columna que EF genera coincida con el real.
            //
            // Y con el tipo declarado hace falta la otra mitad: Npgsql tampoco acepta escribir un
            // DateTime con Kind=Utc en "timestamp without time zone" ("Cannot write DateTime with
            // Kind=Utc to PostgreSQL type 'timestamp without time zone'"). Los modelos inicializan
            // CreatedAt con DateTime.UtcNow y LevantarObservacion asigna DateTime.UtcNow a
            // FechaLevantamiento, así que TODA escritura del módulo de Revisiones tiraba 500 (las
            // lecturas no: por eso la pantalla cargaba pero no dejaba crear nada). El convertidor
            // normaliza el Kind al escribir sin tocar el valor del reloj, de modo que se sigue
            // guardando UTC — arreglarlo acá y no en cada asignación cubre también las que se
            // agreguen después. Se aplica solo al lado de escritura; al leer, el valor vuelve tal
            // cual (Kind=Unspecified, que es lo que ya devolvía la columna).
            //
            // ac_observaciones / ac_observacion_fotos tenían el mismo problema y NO están acá a
            // propósito: el 2026-09-04 se migraron sus columnas a timestamptz de verdad
            // (Migrations/Manual/20260904_ObservacionesTimestamptz.sql), que es el estándar del
            // proyecto, así que el mapeo por defecto de Npgsql ya es el correcto para ellas. Este
            // bloque es deuda a saldar de la misma forma en las demás tablas que lista, no el
            // patrón a seguir para una tabla nueva.
            var utcSinZona = new ValueConverter<DateTime, DateTime>(
                v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                v => v);
            var utcSinZonaNullable = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
                v => v);

            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.RevisionesFeature.Infrastructure.Models.AcRevision>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });
            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.RevisionesFeature.Infrastructure.Models.AcRevisionObservacion>(entity =>
            {
                entity.Property(e => e.Fecha).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.PlazoLevantamiento).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.FechaLevantamiento).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.RevisionesFeature.Infrastructure.Models.AcRevisionObservacionFoto>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });

            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models.AcCostoRegistro>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models.AcCostoProyeccion>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models.AcCostoMetaMensual>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models.AcCostoPresupuesto>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models.AcCostoCierre>(entity =>
            {
                entity.Property(e => e.CerradoEn).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });

            modelBuilder.Entity<AlmacenMaterial>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });
            modelBuilder.Entity<AlmacenMovimiento>(entity =>
            {
                entity.Property(e => e.Fecha).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });
            modelBuilder.Entity<AlmacenOrdenCompra>(entity =>
            {
                entity.Property(e => e.Fecha).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });

            // Mismo problema que arriba (Revisiones): las tablas nuevas de Evaluaciones
            // (2026-08-20_evaluaciones_ssoma_supervisores_jefe_prevencionistas.sql) se crearon
            // con columnas "timestamp" (sin zona) pero los modelos inicializan con
            // DateTime.UtcNow (Kind=Utc) — cualquier INSERT/UPDATE tira 500 aunque las
            // lecturas funcionen. Se aplica el mismo conversor a las 3 evaluaciones nuevas.
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvEvaluacionJefeSsoma>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvEvaluacionJefeSsomaCumplimiento>(entity =>
            {
                entity.Property(e => e.CompletadoAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvEvaluacionSupervisorContratista>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvEvaluacionPrevencionista>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvJefeSsomaPlanAccion>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvGestionSsomaPlanAccion>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZonaNullable);
            });
            modelBuilder.Entity<Abril_Backend.Features.Evaluaciones.Infrastructure.Models.EvEvaluacionStaff>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasConversion(utcSinZona);
            });

            modelBuilder.Entity<ProjectSubContractor>(entity =>
            {
                // La convención snake_case produce "step6signed_*" (no inserta guion después
                // del dígito 6). Forzamos los nombres con guion para que coincidan con las
                // columnas realmente creadas en la BD.
                entity.Property(e => e.Step6SignedCostos)
                      .HasColumnName("step6_signed_costos");
                entity.Property(e => e.Step6SignedGerenteInmobiliario)
                      .HasColumnName("step6_signed_gerente_inmobiliario");
                entity.Property(e => e.Step6SignedGerenteGeneral)
                      .HasColumnName("step6_signed_gerente_general");
            });

            // Un solo croquis activo por proyecto: índice único parcial sobre los registros con state = true.
            modelBuilder.Entity<Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models.ProjectCroquis>()
                .HasIndex(c => c.ProjectId)
                .IsUnique()
                .HasFilter("state = true");

            // Un solo requisito activo por vecino + tipo: índice único parcial sobre state = true.
            modelBuilder.Entity<Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models.VecinoRequisito>()
                .HasIndex(r => new { r.VecinoId, r.VecinoRequisitoTipoId })
                .IsUnique()
                .HasFilter("state = true");

            // Un mismo token de Decolecta no puede estar activo dos veces: índice único parcial sobre state = true.
            modelBuilder.Entity<DecolectaToken>()
                .HasIndex(t => t.Token)
                .IsUnique()
                .HasFilter("state = true");

            modelBuilder.Entity<WorkItemCategoryClause>()
                .HasOne(c => c.WorkItemCategory)
                .WithMany()
                .HasForeignKey(c => c.WorkItemCategoryId)
                .IsRequired();
            modelBuilder.Entity<WorkItemValorizationForm>()
                .HasOne(f => f.WorkItem)
                .WithMany()
                .HasForeignKey(f => f.WorkItemId)
                .IsRequired();
            modelBuilder.Entity<WorkItemCategoryAnexo3Clause>(entity =>
            {
                // Mismo caso que Anexo4: la convención snake_case produce "work_item_category_anexo3clause"
                // (sin guion antes de "clause" por el dígito 3). Forzamos los nombres con guion.
                entity.ToTable("work_item_category_anexo3_clause");
                entity.Property(e => e.WorkItemCategoryAnexo3ClauseId)
                      .HasColumnName("work_item_category_anexo3_clause_id");
                entity.HasOne(c => c.WorkItemCategory)
                      .WithMany()
                      .HasForeignKey(c => c.WorkItemCategoryId)
                      .IsRequired();
            });
            modelBuilder.Entity<WorkItemCategoryAnexo4Clause>(entity =>
            {
                // La convención snake_case produce "work_item_category_anexo4clause" (no inserta guion
                // antes de "clause" por el dígito 4). Forzamos los nombres con guion para que coincidan
                // con la tabla/columna realmente creadas en la BD.
                entity.ToTable("work_item_category_anexo4_clause");
                entity.Property(e => e.WorkItemCategoryAnexo4ClauseId)
                      .HasColumnName("work_item_category_anexo4_clause_id");
                entity.HasOne(c => c.WorkItemCategory)
                      .WithMany()
                      .HasForeignKey(c => c.WorkItemCategoryId)
                      .IsRequired();
            });
            modelBuilder.Entity<MilestoneSchedule>(entity =>
            {
                entity.Property(e => e.Order).HasColumnName("milestone_schedule_order");
            });
            modelBuilder.Entity<MilestoneScheduleHistory>(entity =>
            {
                entity.Property(e => e.IsEqualToLastVersion).HasColumnName("is_equal_to_last_version");
            });
            modelBuilder.Entity<AuditoriaCambio>(entity =>
            {
                entity.Property(e => e.DatosAnteriores).HasColumnType("jsonb");
                entity.Property(e => e.DatosNuevos).HasColumnType("jsonb");
            });
            modelBuilder.Entity<SsomaPasoAuditoria>(entity =>
            {
                entity.ToTable("ssoma_paso_auditoria");
                entity.Property(e => e.EntidadId).HasColumnName("entidad_id");
                entity.Property(e => e.PasoId).HasColumnName("paso_id");
                entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.ValorAnterior).HasColumnName("valor_anterior").HasColumnType("jsonb");
                entity.Property(e => e.ValorNuevo).HasColumnName("valor_nuevo").HasColumnType("jsonb");
            });
            modelBuilder.Entity<WorkerEvento>().ToTable("worker_eventos");
            modelBuilder.Entity<WorkerEvento>().Property(e => e.Datos).HasColumnType("jsonb");

            // ── CursoModule ──────────────────────────────────────────────────
            modelBuilder.Entity<CursoSlide>().Property(e => e.ConfiguracionJson).HasColumnType("jsonb");
            modelBuilder.Entity<CursoIntentoRespuesta>().Property(e => e.RespuestaJson).HasColumnType("jsonb");
            modelBuilder.Entity<CursoPreguntaBanco>().Property(e => e.ConfiguracionJson).HasColumnType("jsonb");

            // ── Lecciones aprendidas / Áreas (wip/lecciones-aprendidas) ─────
            // ScopeItem: evitar ambigüedad en FK self-referential con snake_case
            modelBuilder.Entity<ScopeItem>()
                .Property(s => s.ScopeItemParentId)
                .HasColumnName("scope_item_parent_id");

            // ScopeTemplateItem: evitar ambigüedad en FK self-referential con snake_case
            modelBuilder.Entity<ScopeTemplateItem>()
                .Property(s => s.ScopeTemplateItemParentId)
                .HasColumnName("scope_template_item_parent_id");

            // ── master ─────────────────────────────────────────────────────
            modelBuilder.Entity<SsClinicaAuditoria>().Property(e => e.DetalleAdicional).HasColumnType("jsonb");
            modelBuilder.Entity<ProjectActivity>(entity =>
            {
                entity.ToTable("project_activity");
                entity.Property(e => e.Order).HasColumnName("project_activity_order");
                entity.Property(e => e.ActivityDescription).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ProgressPercentage).HasDefaultValue(0);
                entity.Property(e => e.TipoCronograma).IsRequired().HasMaxLength(30).HasDefaultValue("ANTEPROYECTO");
                entity.HasOne<ProjectActivity>()
                    .WithMany()
                    .HasForeignKey(e => e.ParentId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Feriado>(entity =>
            {
                entity.ToTable("feriados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Fecha).IsRequired();
                entity.Property(e => e.Descripcion).HasMaxLength(200);
                entity.HasIndex(e => e.Fecha).IsUnique();
            });

            modelBuilder.Entity<ActivityPredecessor>(entity =>
            {
                entity.ToTable("activity_predecessor");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ActivityId, e.PredecessorId }).IsUnique();
                entity.HasIndex(e => e.ActivityId);
                entity.HasIndex(e => e.PredecessorId);
                entity.HasOne<ProjectActivity>()
                    .WithMany()
                    .HasForeignKey(e => e.ActivityId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<ProjectActivity>()
                    .WithMany()
                    .HasForeignKey(e => e.PredecessorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserCronogramaPreference>(entity =>
            {
                entity.ToTable("user_cronograma_preference");
                entity.HasKey(e => new { e.UserId, e.ProjectId });
                entity.Property(e => e.TipoCronograma).IsRequired().HasMaxLength(30);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<CronogramaTemplateItem>(entity =>
            {
                entity.ToTable("cronograma_template_item");
                entity.Property(e => e.TipoCronograma).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Codigo).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ParentCodigo).HasMaxLength(30);
                entity.Property(e => e.PredecesoraCodigo).HasMaxLength(30);
                entity.HasIndex(e => new { e.TipoCronograma, e.Codigo }).IsUnique();
            });

            // ── RAC — HasColumnName para prefijos conflictivos en snake_case ──
            // "Sp" (SharePoint ID) y "Uit" (Unidad Impositiva Tributaria) pueden
            // producir nombres incorrectos dependiendo de la versión de Humanizer.
            modelBuilder.Entity<SsomaRacFoto>(entity =>
            {
                entity.Property(e => e.SpId).HasColumnName("sp_id");
            });
            modelBuilder.Entity<SsomaRac>(entity =>
            {
                entity.Property(e => e.PdfSpId).HasColumnName("pdf_sp_id");
                entity.Property(e => e.FirmaReportanteSpId).HasColumnName("firma_reportante_sp_id");
                entity.Property(e => e.PdfUrl).HasColumnName("pdf_url");
            });
            modelBuilder.Entity<SsomaRacInfraccion>(entity =>
            {
                entity.Property(e => e.FactorUit).HasColumnName("factor_uit");
            });
            modelBuilder.Entity<SsomaPenalidad>(entity =>
            {
                entity.Property(e => e.UitReferencia).HasColumnName("uit_referencia");
                entity.Property(e => e.PdfNotificacionUrl).HasColumnName("pdf_notificacion_url");
                entity.Property(e => e.PdfResolucionUrl).HasColumnName("pdf_resolucion_url");
            });

            // ── Dossier Semanal ──────────────────────────────────────────────
            modelBuilder.Entity<SsDossierSemana>(entity =>
            {
                entity.HasIndex(e => new { e.ContributorId, e.ProyectoId, e.Anio, e.NumeroSemana })
                      .IsUnique();
                entity.HasMany(e => e.Documentos)
                      .WithOne(d => d.Dossier)
                      .HasForeignKey(d => d.DossierId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<SsDossierDocumento>(entity =>
            {
                entity.HasIndex(e => new { e.DossierId, e.TipoDoc }).IsUnique();
            });
            modelBuilder.Entity<SsDossierDocumentoArchivo>(e =>
            {
                e.ToTable("ss_dossier_documento_archivo");
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.DocumentoId).HasColumnName("documento_id");
                e.Property(x => x.NombreArchivo).HasColumnName("nombre_archivo");
                e.Property(x => x.ArchivoPath).HasColumnName("archivo_path");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.HasOne(x => x.Documento).WithMany(d => d.Archivos).HasForeignKey(x => x.DocumentoId);
            });

            // ── Amonestaciones y Suspensiones ──────────────────────────────────
            modelBuilder.Entity<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models.SsomaAmonestacion>(e =>
            {
                e.HasMany(a => a.Fotos)
                 .WithOne(f => f.Amonestacion)
                 .HasForeignKey(f => f.AmonestacionId);
            });

            // ── Presupuesto Materiales SSOMA ────────────────────────────────────
            modelBuilder.Entity<SsMaterialAlias>(e =>
            {
                e.HasIndex(x => x.TextoCrudoNorm).IsUnique();
            });
            modelBuilder.Entity<SsRatioProyecto>(e =>
            {
                e.HasIndex(x => new { x.FamiliaId, x.ProjectId }).IsUnique();
            });
            modelBuilder.Entity<SsPresupuesto>(e =>
            {
                e.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
            });
            modelBuilder.Entity<SsPresupuestoSeleccionRatio>(e =>
            {
                e.HasKey(x => new { x.PresupuestoId, x.FamiliaId, x.ProjectId });
            });
            modelBuilder.Entity<SsControlSemanaLinea>(e =>
            {
                e.Property(x => x.TotalReal)
                 .HasComputedColumnSql("cantidad_real * COALESCE(precio_unitario, 0)", stored: true);
                e.HasOne<SsControlSemana>()
                 .WithMany()
                 .HasForeignKey(x => x.ControlId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne<SsMaterialFamilia>()
                 .WithMany()
                 .HasForeignKey(x => x.FamiliaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SsKitItem>(e =>
            {
                e.HasOne(x => x.Kit)
                 .WithMany(k => k.Items)
                 .HasForeignKey(x => x.KitId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Familia)
                 .WithMany()
                 .HasForeignKey(x => x.FamiliaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Gestión GTH · Reclutamiento ─────────────────────────────────────
            // Catálogos: solo un registro "vivo" (state = true) por nombre/código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPuesto>()
                .HasIndex(p => p.Nombre).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoRequerimiento>(e =>
            {
                e.HasIndex(t => t.Nombre).IsUnique().HasFilter("state = true");
                e.HasIndex(t => t.Codigo).IsUnique().HasFilter("state = true");
            });
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoRequerimiento>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPrioridad>()
                .HasIndex(p => p.Codigo).IsUnique().HasFilter("state = true");
            // Un tipo de correo solo tiene un registro "vivo" (state = true) por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoTipo>()
                .HasIndex(t => t.Codigo).IsUnique().HasFilter("state = true");
            // Un correo no puede repetirse entre los destinatarios vigentes del mismo tipo
            // (se guarda en minúsculas). El mismo correo sí puede estar en dos tipos distintos.
            // Los destinatarios dinámicos (codigo no nulo) quedan fuera: no tienen correo y su
            // unicidad es por (tipo, codigo) — ese índice vive solo en Migrations_Manual porque
            // EF no sabe expresar el lower()/upper() del índice real.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoDestinatario>(e =>
            {
                e.HasIndex(d => new { d.GthCorreoTipoId, d.Email }).IsUnique()
                 .HasFilter("state = true AND codigo IS NULL AND email IS NOT NULL");
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCorreoTipo>()
                 .WithMany()
                 .HasForeignKey(d => d.GthCorreoTipoId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimiento>(e =>
            {
                // Código REQ-AAAA-NNNN único a nivel global.
                e.HasIndex(r => r.Codigo).IsUnique();
                e.HasIndex(r => r.Anio);
                e.HasOne(r => r.Solicitud)
                 .WithMany(s => s.Requerimientos)
                 .HasForeignKey(r => r.GthSolicitudId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne<Puesto>()
                 .WithMany()
                 .HasForeignKey(r => r.PuestoId)
                 .OnDelete(DeleteBehavior.Restrict);
                // Categoría declarada para la vacante (independiente de puesto.categoria_id).
                e.HasIndex(r => r.CategoriaId);
                e.HasOne<Categoria>()
                 .WithMany()
                 .HasForeignKey(r => r.CategoriaId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoRequerimiento>()
                 .WithMany()
                 .HasForeignKey(r => r.GthTipoRequerimientoId)
                 .OnDelete(DeleteBehavior.Restrict);
                // Tipo del documento del candidato FFT (DNI / CE). Solo lo llenan las vacantes FFT.
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoDocumento>()
                 .WithMany()
                 .HasForeignKey(r => r.GthTipoDocumentoId)
                 .OnDelete(DeleteBehavior.Restrict);
                // Trabajador reemplazado (solo en las vacantes de tipo REEMPLAZO).
                e.HasIndex(r => r.ReemplazaWorkerId);
                e.HasOne<Worker>()
                 .WithMany()
                 .HasForeignKey(r => r.ReemplazaWorkerId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoRequerimiento>()
                 .WithMany()
                 .HasForeignKey(r => r.GthEstadoRequerimientoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPrioridad>()
                 .WithMany()
                 .HasForeignKey(r => r.GthPrioridadId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Project>()
                 .WithMany()
                 .HasForeignKey(r => r.ProjectId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Shared.Models.GthResponsableProceso>()
                 .WithMany()
                 .HasForeignKey(r => r.GthResponsableProcesoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoProceso>()
                 .WithMany()
                 .HasForeignKey(r => r.GthTipoProcesoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Contributor>()
                 .WithMany()
                 .HasForeignKey(r => r.ContributorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Bitácora de fases del requerimiento: una fila por cambio de estado, escrita por
            // RequerimientoEstadoHistorialInterceptor dentro del mismo SaveChanges que lo mueve.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimientoEstadoHistorial>(e =>
            {
                // La línea de tiempo de un requerimiento se lee siempre así: sus filas en orden.
                e.HasIndex(h => new { h.GthRequerimientoId, h.CambioDateTime });
                e.HasOne(h => h.Requerimiento)
                 .WithMany()
                 .HasForeignKey(h => h.GthRequerimientoId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoRequerimiento>()
                 .WithMany()
                 .HasForeignKey(h => h.GthEstadoRequerimientoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoRequerimiento>()
                 .WithMany()
                 .HasForeignKey(h => h.EstadoAnteriorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Shared.Models.GthResponsableProceso>(e =>
            {
                // Solo un registro "vivo" (state = true) por trabajador.
                e.HasIndex(r => r.WorkerId).IsUnique().HasFilter("state = true");
                e.HasOne(r => r.Worker)
                 .WithMany()
                 .HasForeignKey(r => r.WorkerId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSolicitudPersonalVisibilidadArea>(e =>
            {
                // Llave explícita: sin una PK descubrible no se arma el modelo y se cae toda la app.
                e.HasKey(v => v.GthSolicitudPersonalVisibilidadAreaId);
                // Una sola fila viva por trabajador y área.
                e.HasIndex(v => new { v.WorkerId, v.AreaScopeId }).IsUnique().HasFilter("state = true");
                e.HasOne<Worker>()
                 .WithMany()
                 .HasForeignKey(v => v.WorkerId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>()
                 .WithMany()
                 .HasForeignKey(v => v.AreaScopeId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoProceso>()
                .HasIndex(t => t.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCanalPublicacion>()
                .HasIndex(c => c.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimientoCanal>(e =>
            {
                // Solo una publicación "viva" (state = true) por requerimiento + canal.
                e.HasIndex(rc => new { rc.GthRequerimientoId, rc.GthCanalPublicacionId }).IsUnique().HasFilter("state = true");
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimiento>()
                 .WithMany()
                 .HasForeignKey(rc => rc.GthRequerimientoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCanalPublicacion>()
                 .WithMany()
                 .HasForeignKey(rc => rc.GthCanalPublicacionId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Estado de revisión del candidato: solo un registro "vivo" (state = true) por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEstado>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>(e =>
            {
                e.HasIndex(c => c.GthRequerimientoId);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimiento>()
                 .WithMany()
                 .HasForeignKey(c => c.GthRequerimientoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEstado>()
                 .WithMany()
                 .HasForeignKey(c => c.GthCandidatoEstadoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCanalPublicacion>()
                 .WithMany()
                 .HasForeignKey(c => c.GthCanalPublicacionId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Portafolio/anexos del candidato: N archivos por candidato, sin unicidad.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoAnexo>(e =>
            {
                e.HasIndex(a => a.GthCandidatoId).HasFilter("state = true");
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>()
                 .WithMany(c => c.Anexos)
                 .HasForeignKey(a => a.GthCandidatoId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Gestión GTH · Programación de entrevistas ────────────────────────
            // Lugar de entrevista: un solo registro "vivo" (state = true) por nombre.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthLugarEntrevista>()
                .HasIndex(l => l.Nombre).IsUnique().HasFilter("state = true");

            // Catálogo de la respuesta del candidato: un solo registro "vivo" por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevistaRespuesta>()
                .HasIndex(r => r.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevista>(e =>
            {
                // Una sola entrevista vigente por candidato: reprogramar actualiza esa fila.
                e.HasIndex(x => x.GthCandidatoId).IsUnique().HasFilter("state = true");
                // El token identifica la entrevista en el enlace público del correo.
                e.HasIndex(x => x.Token).IsUnique().HasFilter("state = true AND token IS NOT NULL");
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>()
                 .WithMany()
                 .HasForeignKey(x => x.GthCandidatoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthLugarEntrevista>()
                 .WithMany()
                 .HasForeignKey(x => x.GthLugarEntrevistaId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEntrevistaRespuesta>()
                 .WithMany()
                 .HasForeignKey(x => x.GthEntrevistaRespuestaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Gestión GTH · Evaluación de la entrevista (finalistas) ──────────
            // Catálogo del resultado: un solo registro "vivo" (state = true) por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoResultado>()
                .HasIndex(r => r.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacion>(e =>
            {
                // Una sola evaluación vigente por candidato: volver a guardar actualiza esa fila.
                e.HasIndex(x => x.GthCandidatoId).IsUnique().HasFilter("state = true");
                // Con navegación: el ingreso directo FFT crea al candidato y su evaluación en el
                // mismo SaveChanges, y es EF quien copia la FK (ver GthCandidatoEvaluacion.Candidato).
                e.HasOne(x => x.Candidato)
                 .WithMany()
                 .HasForeignKey(x => x.GthCandidatoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoResultado>()
                 .WithMany()
                 .HasForeignKey(x => x.GthCandidatoResultadoId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Archivos del informe de la entrevista: catálogo de documentos (un registro vivo por
            // código) y un archivo vivo por evaluación y tipo — volver a subirlo reemplaza al anterior.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEvaluacionArchivoTipo>()
                .HasIndex(t => t.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacionArchivo>(e =>
            {
                e.HasIndex(a => new { a.GthCandidatoEvaluacionId, a.GthEvaluacionArchivoTipoId })
                 .IsUnique().HasFilter("state = true");
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidatoEvaluacion>()
                 .WithMany()
                 .HasForeignKey(a => a.GthCandidatoEvaluacionId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEvaluacionArchivoTipo>()
                 .WithMany()
                 .HasForeignKey(a => a.GthEvaluacionArchivoTipoId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Gestión GTH · Formulario del postulante + catálogos ─────────────
            // Catálogos del formulario: solo un registro "vivo" (state = true) por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormularioEstado>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoCivil>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoDocumento>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthGradoAcademico>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDisponibilidad>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthMotivoCese>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthUniversidad>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDistrito>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormulario>(e =>
            {
                // Un solo formulario "vivo" (state = true) por candidato y token único entre los vigentes.
                e.HasIndex(f => f.GthCandidatoId).IsUnique().HasFilter("state = true");
                e.HasIndex(f => f.Token).IsUnique().HasFilter("state = true");

                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>()
                 .WithMany().HasForeignKey(f => f.GthCandidatoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthPostulanteFormularioEstado>()
                 .WithMany().HasForeignKey(f => f.GthPostulanteFormularioEstadoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthEstadoCivil>()
                 .WithMany().HasForeignKey(f => f.GthEstadoCivilId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthTipoDocumento>()
                 .WithMany().HasForeignKey(f => f.GthTipoDocumentoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDistrito>()
                 .WithMany().HasForeignKey(f => f.GthDistritoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthDisponibilidad>()
                 .WithMany().HasForeignKey(f => f.GthDisponibilidadId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthUniversidad>()
                 .WithMany().HasForeignKey(f => f.GthUniversidadId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthGradoAcademico>()
                 .WithMany().HasForeignKey(f => f.GthGradoAcademicoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthMotivoCese>()
                 .WithMany().HasForeignKey(f => f.GthMotivoCeseId).OnDelete(DeleteBehavior.Restrict);
                // Ficha de la data maestra que se creó/actualizó al aprobar el formulario.
                e.HasOne<Person>()
                 .WithMany().HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Aprobación de la solicitud de personal (gerente del área + GG) ──
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgEstado>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGg>(e =>
            {
                // Una sola aprobación viva por solicitud (1:1) y token único entre las vigentes.
                e.HasIndex(a => a.GthSolicitudId).IsUnique().HasFilter("state = true");
                e.HasIndex(a => a.Token).IsUnique().HasFilter("state = true");
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthSolicitud>()
                 .WithMany().HasForeignKey(a => a.GthSolicitudId).OnDelete(DeleteBehavior.Restrict);
                // Dos FKs al MISMO catálogo de estados, una por nivel de decisión.
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgEstado>()
                 .WithMany().HasForeignKey(a => a.EstadoGerenteGeneralId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgEstado>()
                 .WithMany().HasForeignKey(a => a.EstadoGerenteAreaId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGgDetalle>(e =>
            {
                e.HasIndex(d => new { d.GthAprobacionGgId, d.GthRequerimientoId }).IsUnique().HasFilter("state = true");
                e.HasIndex(d => d.GthRequerimientoId);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthAprobacionGg>()
                 .WithMany().HasForeignKey(d => d.GthAprobacionGgId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthRequerimiento>()
                 .WithMany().HasForeignKey(d => d.GthRequerimientoId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Configuración · Bancos ──────────────────────────────────────────
            // Un solo banco vivo por código, y la razón social apunta acá. La FK se declara sin
            // navegación en Contributor a propósito: nadie necesita el objeto banco colgado de la
            // razón social, y declararla obligaría a cargarla en todas las consultas que ya existen.
            modelBuilder.Entity<Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Models.Banco>(e =>
            {
                e.HasIndex(b => b.Codigo).IsUnique().HasFilter("state = true");
            });

            modelBuilder.Entity<Contributor>()
                .HasOne<Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Models.Banco>()
                .WithMany().HasForeignKey(c => c.BancoId).OnDelete(DeleteBehavior.Restrict);

            // ── Gestión GTH · Onboarding ────────────────────────────────────────
            // Catálogos: un solo registro "vivo" (state = true) por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFase>()
                .HasIndex(f => f.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingEstado>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");

            // Checklist operativo: las actividades obligatorias de cada fase.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingActividad>(e =>
            {
                e.HasIndex(a => a.Codigo).IsUnique().HasFilter("state = true");
                e.HasIndex(a => a.GthOnboardingFaseId);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFase>()
                 .WithMany().HasForeignKey(a => a.GthOnboardingFaseId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboarding>(e =>
            {
                // Un solo onboarding "vivo" (state = true) por candidato seleccionado.
                e.HasIndex(o => o.GthCandidatoId).IsUnique().HasFilter("state = true");
                e.HasIndex(o => o.PersonId);

                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>()
                 .WithMany().HasForeignKey(o => o.GthCandidatoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Person>()
                 .WithMany().HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFase>()
                 .WithMany().HasForeignKey(o => o.GthOnboardingFaseId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingEstado>()
                 .WithMany().HasForeignKey(o => o.GthOnboardingEstadoId).OnDelete(DeleteBehavior.Restrict);
            });

            // Catálogos propios del formulario de bienvenida: un solo registro vivo por código.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormularioEstado>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingUbicacion>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthRentaQuinta>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.TallaCalzado>()
                .HasIndex(e => e.Codigo).IsUnique().HasFilter("state = true");

            // Formulario «Nuevos Talentos»: uno vivo por onboarding y token único entre los vigentes
            // (es la única credencial de su página pública).
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormulario>(e =>
            {
                e.HasIndex(f => f.GthOnboardingId).IsUnique().HasFilter("state = true");
                e.HasIndex(f => f.Token).IsUnique().HasFilter("state = true");

                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboarding>()
                 .WithMany().HasForeignKey(f => f.GthOnboardingId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingFormularioEstado>()
                 .WithMany().HasForeignKey(f => f.GthOnboardingFormularioEstadoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Puesto>()
                 .WithMany().HasForeignKey(f => f.PuestoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthOnboardingUbicacion>()
                 .WithMany().HasForeignKey(f => f.GthOnboardingUbicacionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Contributor>()
                 .WithMany().HasForeignKey(f => f.ContributorId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Sexo>()
                 .WithMany().HasForeignKey(f => f.SexoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.TallaCalzado>()
                 .WithMany().HasForeignKey(f => f.TallaCalzadoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.Talla>()
                 .WithMany().HasForeignKey(f => f.TallaId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models.GthRentaQuinta>()
                 .WithMany().HasForeignKey(f => f.GthRentaQuintaId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Gestión GTH · Carta oferta (último paso de Reclutamiento) ───────
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOferta>(e =>
            {
                // Una sola carta oferta "viva" (state = true) por candidato seleccionado.
                e.HasIndex(c => c.GthCandidatoId).IsUnique().HasFilter("state = true");
                e.HasIndex(c => c.PersonId);

                // El token del enlace público es la única credencial de la página donde el candidato
                // firma su carta oferta: no puede repetirse entre cartas vigentes.
                e.HasIndex(c => c.Token).IsUnique()
                 .HasFilter("state = true AND token IS NOT NULL");

                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCandidato>()
                 .WithMany().HasForeignKey(c => c.GthCandidatoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Person>()
                 .WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
            });

            // Las condiciones de contrato que imprime la carta: una fila por viñeta.
            modelBuilder.Entity<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOfertaCondicion>(e =>
            {
                // Se leen SIEMPRE por carta y en orden, que es como se imprimen.
                e.HasIndex(c => new { c.GthCartaOfertaId, c.Orden });

                e.HasOne<Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models.GthCartaOferta>()
                 .WithMany(c => c.Condiciones)
                 .HasForeignKey(c => c.GthCartaOfertaId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Notificaciones in-app (campanita del encabezado) ────────────────
            modelBuilder.Entity<NotificacionTipo>()
                .HasIndex(t => t.Codigo).IsUnique().HasFilter("state = true");

            modelBuilder.Entity<Notificacion>(e =>
            {
                e.HasIndex(n => n.UserId);
                e.HasOne<User>()
                 .WithMany()
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<NotificacionTipo>()
                 .WithMany()
                 .HasForeignKey(n => n.NotificacionTipoId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne<User>()
                 .WithMany()
                 .HasForeignKey(n => n.OrigenUserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PushSuscripcion>(e =>
            {
                e.HasIndex(x => x.Endpoint).IsUnique();
                e.HasIndex(x => x.UserId);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── Planeamiento BIM ─────────────────────────────────────────────
            // Catálogos globales: nombre único para poder sembrarlos con ON CONFLICT DO NOTHING.
            modelBuilder.Entity<BimMacroActividad>()
                .HasIndex(x => x.Nombre).IsUnique();
            modelBuilder.Entity<BimCausaNoCumplimiento>()
                .HasIndex(x => x.Nombre).IsUnique();
            modelBuilder.Entity<BimFase>()
                .HasIndex(x => x.Nombre).IsUnique();

            modelBuilder.Entity<BimActividad>(e =>
            {
                // Mismo nombre puede repetirse entre tipos distintos (ej. "Vaciado de concreto"
                // aparece como VERTICAL y como HORIZONTAL) dentro de una misma macro_actividad.
                e.HasIndex(x => new { x.MacroActividadId, x.Tipo, x.Nombre }).IsUnique();
                e.HasOne(x => x.MacroActividad)
                 .WithMany(m => m.Actividades)
                 .HasForeignKey(x => x.MacroActividadId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BimProyectoTorre>(e =>
            {
                e.HasIndex(x => x.ProjectId);
                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BimTorreNivel>(e =>
            {
                e.HasIndex(x => x.TorreId);
                e.HasOne(x => x.Torre)
                 .WithMany(t => t.Niveles)
                 .HasForeignKey(x => x.TorreId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Huérfana (ver comentario en BimZonaSector.cs): sin navegación inversa desde
            // BimProyectoTorre, esta tabla ya no forma parte del árbol torre/nivel.
            modelBuilder.Entity<BimZonaSector>(e =>
            {
                e.HasIndex(x => x.ZonaId);
                e.HasOne(x => x.Zona)
                 .WithMany()
                 .HasForeignKey(x => x.ZonaId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BimProyectoFase>(e =>
            {
                e.HasIndex(x => x.ProjectId);
                e.HasIndex(x => x.FaseId);
                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Fase)
                 .WithMany()
                 .HasForeignKey(x => x.FaseId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BimRegistroDiario>(e =>
            {
                e.ToTable("bim_registro_diario");
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.ProjectId).HasColumnName("project_id");
                e.Property(x => x.TorreId).HasColumnName("torre_id");
                e.Property(x => x.NivelId).HasColumnName("nivel_id");
                e.Property(x => x.SectorId).HasColumnName("sector_id");
                e.Property(x => x.ActividadId).HasColumnName("actividad_id");
                e.Property(x => x.Fecha).HasColumnName("fecha");
                e.Property(x => x.PorcentajeAvance).HasColumnName("porcentaje_avance");
                e.Property(x => x.CausaId).HasColumnName("causa_id");
                e.Property(x => x.CausaDetalle).HasColumnName("causa_detalle");
                e.Property(x => x.CreatedUserId).HasColumnName("created_user_id");
                e.Property(x => x.CreatedDateTime).HasColumnName("created_date_time");
                e.Property(x => x.UpdatedUserId).HasColumnName("updated_user_id");
                e.Property(x => x.UpdatedDateTime).HasColumnName("updated_date_time");

                e.Ignore(x => x.ZonaId);
                e.Ignore(x => x.Zona);

                // Evita duplicados y define que una corrección dentro de la ventana de
                // edición sea UPDATE sobre esta fila, no un INSERT nuevo.
                // Nombre real en producción: ix_bim_registro_diario_unico (creado a mano,
                // más corto que el que EF generaría por convención — 63+ chars se trunca).
                e.HasIndex(x => new { x.ProjectId, x.TorreId, x.NivelId, x.SectorId, x.ActividadId, x.Fecha })
                 .IsUnique()
                 .HasDatabaseName("ix_bim_registro_diario_unico");
                e.HasIndex(x => x.TorreId);
                e.HasIndex(x => x.NivelId);
                e.HasIndex(x => x.SectorId);
                e.HasIndex(x => x.ActividadId);
                e.HasIndex(x => x.CausaId);
                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Torre)
                 .WithMany()
                 .HasForeignKey(x => x.TorreId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Nivel)
                 .WithMany()
                 .HasForeignKey(x => x.NivelId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Actividad)
                 .WithMany()
                 .HasForeignKey(x => x.ActividadId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Causa)
                 .WithMany()
                 .HasForeignKey(x => x.CausaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BimEvidenciaFoto>(e =>
            {
                e.HasIndex(x => new { x.ProjectId, x.Fecha });
                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BimRestriccion>(e =>
            {
                e.ToTable("bim_bloqueo");
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.ProjectId).HasColumnName("project_id");
                e.Property(x => x.Descripcion).HasColumnName("descripcion");
                e.Property(x => x.Estado).HasColumnName("estado");
                e.Property(x => x.FechaCreacion).HasColumnName("fecha_creacion");
                e.Property(x => x.FechaActualizacion).HasColumnName("fecha_actualizacion");
                e.Property(x => x.FechaCierre).HasColumnName("fecha_cierre");
                e.Property(x => x.FechaLevantamientoPrevista).HasColumnName("fecha_levantamiento_prevista");
                e.Property(x => x.CreatedUserId).HasColumnName("created_user_id");
                e.Property(x => x.TorreId).HasColumnName("torre_id");
                e.Property(x => x.NivelId).HasColumnName("nivel_id");
                e.Property(x => x.Sector).HasColumnName("sector");
                e.Property(x => x.ActividadId).HasColumnName("actividad_id");

                e.HasIndex(x => x.ProjectId);
                e.HasIndex(x => x.TorreId);
                e.HasIndex(x => x.NivelId);
                e.HasIndex(x => x.ActividadId);

                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Torre)
                 .WithMany()
                 .HasForeignKey(x => x.TorreId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Nivel)
                 .WithMany()
                 .HasForeignKey(x => x.NivelId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Actividad)
                 .WithMany()
                 .HasForeignKey(x => x.ActividadId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<BimMetaSemanal>(e =>
            {
                e.HasIndex(x => new { x.ProjectId, x.MacroActividadId, x.FechaInicioSemana })
                 .IsUnique()
                 .HasDatabaseName("ix_bim_meta_semanal_unico");
                e.HasIndex(x => x.MacroActividadId);
                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.MacroActividad)
                 .WithMany()
                 .HasForeignKey(x => x.MacroActividadId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}
