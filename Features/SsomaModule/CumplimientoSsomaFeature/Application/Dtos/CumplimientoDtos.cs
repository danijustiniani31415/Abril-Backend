namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos
{
    // ─── Catálogo de actividades ─────────────────────────────────────────────────

    public class CumplimientoActividadDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string? Categoria { get; set; }
        public string RolResponsable { get; set; } = null!;
        public string Frecuencia { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class CumplimientoActividadUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string? Categoria { get; set; }
        public string RolResponsable { get; set; } = "ambos";
        public string Frecuencia { get; set; } = "diaria";
        public int Orden { get; set; } = 0;
    }

    // ─── Cumplimiento por proyecto/periodo ───────────────────────────────────────

    // Estado de una actividad en el periodo vigente para un proyecto (el registro
    // se crea recién cuando alguien la marca — antes de eso, "pendiente" es solo
    // la ausencia de fila, no algo que haya que sembrar por cron).
    public class CumplimientoItemDto
    {
        public int ActividadId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string? Categoria { get; set; }
        public string RolResponsable { get; set; } = null!;
        public string Frecuencia { get; set; } = null!;
        public DateOnly Periodo { get; set; }
        // "pendiente" | "cumplido" | "no_aplica"
        public string Estado { get; set; } = "pendiente";
        public string? MotivoNoAplica { get; set; }
        public DateTimeOffset? FechaCumplimiento { get; set; }
        public string? CumplidoPor { get; set; }
        public string? Observacion { get; set; }
    }

    public class CumplimientoResumenDto
    {
        public int ProyectoId { get; set; }
        public List<CumplimientoItemDto> Actividades { get; set; } = new();
    }

    // Resumen "mío": ya resuelve rol y proyecto actual del usuario logueado, para que
    // el celular no tenga que pedirle nada — solo llega y ve su checklist del día.
    public class CumplimientoMiResumenDto
    {
        public int ProyectoId { get; set; } = 0;
        public string ProyectoNombre { get; set; } = "";
        public string? Rol { get; set; }
        public List<CumplimientoItemDto> Actividades { get; set; } = new();
    }

    // Usuario cuyo checklist se puede consultar en un proyecto (prevencionista o coordinador).
    public class CumplimientoUsuarioDto
    {
        public int UserId { get; set; }
        public string Nombre { get; set; } = "";
        // "coordinador_ssoma" | "prevencionista"
        public string Rol { get; set; } = "prevencionista";
    }

    public class CumplimientoMarcarDto
    {
        // "cumplido" | "no_aplica" | "pendiente" (pendiente = desmarcar)
        public string Estado { get; set; } = "cumplido";
        public string? MotivoNoAplica { get; set; }
        public string? Observacion { get; set; }
    }

    // ─── Histórico / indicadores ─────────────────────────────────────────────────

    // Foto de un periodo ya cerrado (o el vigente): cuántas actividades tenía ese día/
    // semana/mes, cuántas se cumplieron, cuántas no aplicaban y cuántas quedaron sin marcar.
    public class CumplimientoHistoricoDiaDto
    {
        public DateOnly Periodo { get; set; }
        // Número de semana ISO-8601 (1-53) del lunes que representa el periodo — solo
        // tiene sentido cuando Frecuencia == "semanal"; se pide mostrar "Semana 48" en
        // vez de la fecha exacta del lunes.
        public int? NumeroSemana { get; set; }
        public int Total { get; set; }
        public int Cumplidas { get; set; }
        public int NoAplica { get; set; }
        public int Pendientes { get; set; }
        public double PorcentajeCumplimiento { get; set; }
    }

    public class CumplimientoHistoricoDto
    {
        public int ProyectoId { get; set; }
        public string Frecuencia { get; set; } = "diaria";
        public List<CumplimientoHistoricoDiaDto> Dias { get; set; } = new();
    }
}
