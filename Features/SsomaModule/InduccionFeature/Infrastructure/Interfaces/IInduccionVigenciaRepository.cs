using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;

public record VigRoster(int WorkerId, string Nombre, string? Dni, string? Categoria);
public record VigDocRecibido(int DocumentoId, int Version);
/// <summary>Una firma de inducción/re-inducción/actualización en una sesión CERRADA del proyecto.</summary>
public record VigRegistro(int WorkerId, int AsistenciaId, int SesionId, string? SesionCodigo, string Tipo, DateOnly Fecha, bool Aprobado, List<VigDocRecibido> Docs);
public record VigVersion(int Version, bool Material, DateTime PublicadoEn, bool Vigente);
public record VigDocumento(int DocumentoId, string Codigo, string Nombre, bool Obligatorio, List<VigVersion> Versiones);

public class DatosVigencia
{
    public List<VigRoster> Roster { get; set; } = [];
    public List<VigRegistro> Registros { get; set; } = [];
    public List<VigDocumento> Catalogo { get; set; } = [];
    /// <summary>Solo las exenciones activas hoy (no revocadas, no vencidas).</summary>
    public List<SsIndExencion> Exenciones { get; set; } = [];
}

public interface IInduccionVigenciaRepository
{
    Task<bool> GateActivo(int proyectoId);
    Task SetGate(int proyectoId, bool activo, int? userId);

    /// <summary>Todo lo necesario para calcular el estado de vigencia. <paramref name="workerIds"/> acota la consulta a
    /// unos trabajadores (el bloqueo del ATS consulta de a uno); null = todo el proyecto. <paramref name="conRoster"/>
    /// trae además la lista de trabajadores activos del proyecto.</summary>
    Task<DatosVigencia> CargarDatos(int proyectoId, IReadOnlyCollection<int>? workerIds, bool conRoster);

    Task<List<SsIndExencion>> ListarExenciones(int proyectoId);
    Task<SsIndExencion> CrearExencion(IndExencionCrearDto dto, int userId);
    Task RevocarExencion(int id, int userId);

    Task<List<int>> ProyectosConInducciones();
    Task<string?> GetProyectoNombre(int proyectoId);
    /// <summary>true si la alerta es nueva (no se había registrado ese proyecto/tipo/periodo).</summary>
    Task<bool> RegistrarAlerta(int proyectoId, string tipo, string periodo, string? detalle);

    Task<IndExpedienteDto> GetExpediente(int workerId, int vigenciaMeses);
}
