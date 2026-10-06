using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;

/// <summary>Lo que ocurre DESPUÉS de cerrar una sesión: sello de tiempo externo y respaldo de archivos.
/// Va aparte de IInduccionRepository porque corre en segundo plano y no toca la sesión en sí.</summary>
public interface IInduccionPostCierreRepository
{
    // ── Sello de tiempo ─────────────────────────────────────────────────────
    Task<SsIndSello?> GetSello(int sesionId);

    /// <summary>Crea (una sola vez) el registro con el digest a sellar: SHA-256( acta_pdf_sha256 | último hash de auditoría ).
    /// Devuelve null si la sesión todavía no tiene acta. Los reintentos reusan el MISMO digest.</summary>
    Task<SsIndSello?> PrepararSello(int sesionId, string proveedor);

    Task RegistrarIntentoSello(int selloId, bool ok, byte[]? token, DateTime? selladoEn, string? serial, string? emisor, string? error, int maxIntentos);

    /// <summary>Vuelve a poner en Pendiente un sello que se rindió (Error) para reintentarlo a mano.</summary>
    Task ReiniciarSello(int sesionId);

    Task<List<int>> SesionesPendientesSello(int max);

    // ── Respaldo ────────────────────────────────────────────────────────────
    Task<SsIndRespaldo?> GetRespaldo(int sesionId);
    Task<SsIndRespaldo> GetOCrearRespaldo(int sesionId);
    Task RegistrarResultadoRespaldo(int respaldoId, bool ok, int archivos, long bytes, string? prefijo, string? error, int maxIntentos);
    Task ReiniciarRespaldo(int sesionId);
    Task<List<int>> SesionesPendientesRespaldo(int max);
}
