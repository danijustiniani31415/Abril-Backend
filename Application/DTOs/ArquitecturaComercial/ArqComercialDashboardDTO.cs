namespace Abril_Backend.Application.DTOs.ArquitecturaComercial
{
    public class ArqComercialKpiDTO
    {
        public int TotalActividades { get; set; }
        public int Culminadas { get; set; }
        public int EnProceso { get; set; }
        public int Vencidas { get; set; }
        public int Pendientes { get; set; }
        public double EficienciaMedia { get; set; }
        public double ProgresoGlobal { get; set; }
    }

    public class ArqComercialAlertDTO
    {
        public int VencidasSinCerrar { get; set; }
        public int VencenEstaSemana { get; set; }
        public int ArrancanEstaSemana { get; set; }
        public int HitosProximos14Dias { get; set; }
    }

    public class ArqComercialChartItemDTO
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class ProyeccionAvanceDTO
    {
        public List<string> Labels { get; set; } = new();
        public List<double> Programado { get; set; } = new();
        public List<double> Real { get; set; } = new();
        public List<double> Proyeccion { get; set; } = new();
    }

    public class EficienciaSemanalDTO
    {
        public string Semana { get; set; } = string.Empty;
        public double Valor { get; set; }
    }

    public class SupervisorProgresoDTO
    {
        public int UserId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public double Progreso { get; set; }
        public int Total { get; set; }
        public int Completadas { get; set; }
        /// <summary>Actividades vencidas de semanas anteriores (no de la semana en control) que
        /// el supervisor sigue arrastrando sin cerrar. Informativo — no afecta el IES.</summary>
        public int DeudaAnterior { get; set; }
        /// <summary>true si no tenía nada que vencer ni arrancar esta semana — se muestra aparte,
        /// no participa del IES ni del promedio del equipo.</summary>
        public bool SinCompromisos { get; set; }
        /// <summary>Componente SPI del IES (35% del puntaje): ritmo real vs. planificado de lo que debía cerrar.</summary>
        public double CompSpi { get; set; }
        /// <summary>Componente tasa de cierre del IES (35%): % de lo que debía cerrar esta semana y cerró.</summary>
        public double CompCierre { get; set; }
        /// <summary>Componente puntualidad de inicio del IES (20%): % de lo que debía arrancar y arrancó a tiempo.</summary>
        public double CompInicio { get; set; }
        /// <summary>Motivos concretos por los que el IES no llegó a 100% (para mostrar en el desglose del ranking).</summary>
        public List<string> Motivos { get; set; } = new();
    }

    /// <summary>Histórico completo de un supervisor (todo el tiempo, no solo la semana en control) —
    /// para el modal "Eficiencia a lo largo del tiempo" del ranking.</summary>
    public class SupervisorHistoricoDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public int TotalActividades { get; set; }
        public int Culminadas { get; set; }
        public int EnProceso { get; set; }
        public int Vencidas { get; set; }
        public int Pendientes { get; set; }
        /// <summary>Culminadas ÷ total, histórico completo (el indicador "de siempre", antes de
        /// restringir el ranking a la semana en control).</summary>
        public double EficienciaHistorica { get; set; }
        /// <summary>SPI promedio histórico de sus actividades con SPI válido.</summary>
        public double SpiPromedio { get; set; }
        /// <summary>Tasa de cierre semanal (culminadas ÷ vencían esa semana) de las últimas 8 semanas —
        /// para ver si viene mejorando o empeorando.</summary>
        public List<EficienciaSemanalDTO> TendenciaSemanal { get; set; } = new();
        /// <summary>Carga (ponderada) semana a semana de las últimas 8 semanas — para distinguir
        /// una sobrecarga puntual de un patrón repetido.</summary>
        public List<CargaSemanalDTO> TendenciaCarga { get; set; } = new();
        /// <summary>Carga ponderada día a día (lun–vie) de la semana pasada y la actual — para ver
        /// en qué días puntuales tocó sobrecarga.</summary>
        public List<CargaDiariaDTO> CargaDiaria { get; set; } = new();
    }

    /// <summary>Carga ponderada de un día hábil puntual (reconstruida con las fechas de inicio/fin
    /// de las actividades), con la media del equipo ese mismo día para clasificarla.</summary>
    public class CargaDiariaDTO
    {
        /// <summary>yyyy-MM-dd.</summary>
        public string Fecha { get; set; } = string.Empty;
        /// <summary>Lun, Mar, Mié, Jue, Vie.</summary>
        public string Dia { get; set; } = string.Empty;
        public bool EsSemanaActual { get; set; }
        public int Hitos { get; set; }
        public int Entregables { get; set; }
        public int Consultas { get; set; }
        public int Total { get; set; }
        public double TotalPonderado { get; set; }
        public double PromedioEquipo { get; set; }
        /// <summary>SOBRECARGADO | NORMAL | DISPONIBLE.</summary>
        public string Tag { get; set; } = string.Empty;
    }

    /// <summary>Carga ponderada de una semana puntual, con el promedio del equipo esa misma
    /// semana para poder clasificarla (igual criterio que "Distribución de Carga" en vivo).</summary>
    public class CargaSemanalDTO
    {
        public string Semana { get; set; } = string.Empty;
        public int Total { get; set; }
        public double TotalPonderado { get; set; }
        public double PromedioEquipo { get; set; }
        /// <summary>SOBRECARGADO | NORMAL | DISPONIBLE.</summary>
        public string Tag { get; set; } = string.Empty;
    }

    public class HitoCriticoDTO
    {
        public int    Id            { get; set; }
        public string Nombre        { get; set; } = string.Empty;
        public string Proyecto      { get; set; } = string.Empty;
        public string FechaLimite   { get; set; } = string.Empty;
        public int    DiasRestantes { get; set; }
        public string Estado        { get; set; } = string.Empty;
        public int    Semana        { get; set; }
    }

    public class SemanaDashboardDTO
    {
        public int    Numero { get; set; }
        public string Label  { get; set; } = string.Empty;
        public string Inicio { get; set; } = string.Empty;
        public string Fin    { get; set; } = string.Empty;
    }

    public class CategoriaDashboardItemDTO
    {
        public int    Id         { get; set; }
        public string Nombre     { get; set; } = string.Empty;
        public int    Total      { get; set; }
        public int    Culminadas { get; set; }
        public int    EnProceso  { get; set; }
        public int    Vencidas   { get; set; }
        public int    Pendientes { get; set; }
        public double Progreso   { get; set; }
    }

    public class ArqComercialDashboardDTO
    {
        public ArqComercialKpiDTO Kpis { get; set; } = new();
        public ArqComercialAlertDTO Alertas { get; set; } = new();
        public ProyeccionAvanceDTO ProyeccionAvance { get; set; } = new();
        public List<ArqComercialChartItemDTO> RankingEficiencia { get; set; } = new();
        public List<ArqComercialChartItemDTO> DistribucionEstado { get; set; } = new();
        public List<EficienciaSemanalDTO> TendenciaEficiencia { get; set; } = new();
        public List<SupervisorProgresoDTO> Supervisores { get; set; } = new();
        public List<HitoCriticoDTO> HitosCriticos { get; set; } = new();
        public TareasPorArquitectoDTO[]       TareasPorArquitectoDetalle  { get; set; } = [];
        public List<ProximoPorProyectoDTO>          ProximosPorProyecto  { get; set; } = new();
        public List<EficienciaConsultaSemanalDTO>   EficienciaConsultas  { get; set; } = new();
        public List<GanttMiniItemDTO>                GanttHitos          { get; set; } = new();
        public List<GanttMiniItemDTO>                GanttEntregables    { get; set; } = new();
        public CategoriaItemDTO[]             Categorias                  { get; set; } = [];
        public List<CategoriaDashboardItemDTO> DistribucionPorCategoria   { get; set; } = [];
        public List<ArqComercialChartItemDTO> DistribucionTipos           { get; set; } = [];
        public SemanaDashboardDTO             SemanaActual                { get; set; } = new();
        public string                          RangoUltimasSemanas        { get; set; } = string.Empty;
    }

    public class ArqComercialFilterOptionDTO
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class ArqComercialProjectOptionDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class ArqComercialFiltersDTO
    {
        public List<ArqComercialFilterOptionDTO> Semanas { get; set; } = new();
        public List<ArqComercialFilterOptionDTO> Meses { get; set; } = new();
        public List<ArqComercialProjectOptionDTO> Proyectos { get; set; } = new();
    }
}
