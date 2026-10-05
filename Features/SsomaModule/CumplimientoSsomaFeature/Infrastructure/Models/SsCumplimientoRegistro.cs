using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models
{
    // Registro de cumplimiento de una actividad, en un proyecto, para un periodo concreto
    // (el día, el lunes de la semana, o el día 1 del mes, según la frecuencia de la actividad),
    // POR USUARIO: cada prevencionista/coordinador del proyecto lleva su propio checklist.
    [Table("ss_cumplimiento_registro")]
    [Index(nameof(ActividadId), nameof(ProyectoId), nameof(Periodo), nameof(UsuarioId), IsUnique = true)]
    public class SsCumplimientoRegistro
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("actividad_id")]
        public int ActividadId { get; set; }

        [Column("proyecto_id")]
        public int ProyectoId { get; set; }

        // Dueño del registro (user_id del que lleva este checklist). Null solo en filas
        // históricas que no tenían a nadie asignado; estas no se muestran a ningún usuario.
        [Column("usuario_id")]
        public int? UsuarioId { get; set; }

        // Fecha que representa el periodo: el día (diaria), el lunes (semanal) o el día 1 (mensual)
        [Column("periodo")]
        public DateOnly Periodo { get; set; }

        [Column("cumplido")]
        public bool Cumplido { get; set; } = false;

        // "pendiente" | "cumplido" | "no_aplica". "Cumplido" (bool) se mantiene en paralelo
        // por compatibilidad (true solo cuando Estado == "cumplido") — no leer Cumplido en
        // código nuevo, usar Estado.
        [Column("estado")]
        public string Estado { get; set; } = "pendiente";

        [Column("motivo_no_aplica")]
        public string? MotivoNoAplica { get; set; }

        [Column("fecha_cumplimiento")]
        public DateTimeOffset? FechaCumplimiento { get; set; }

        [Column("cumplido_por_id")]
        public int? CumplidoPorId { get; set; }

        [Column("observacion")]
        public string? Observacion { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey(nameof(ActividadId))]
        public SsCumplimientoActividad? Actividad { get; set; }

        [ForeignKey(nameof(ProyectoId))]
        public Project? Proyecto { get; set; }
    }
}
