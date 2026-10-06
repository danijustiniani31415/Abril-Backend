using System.Security.Cryptography;
using System.Text.Json;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Services;

/// <summary>
/// Respaldo de una sesión cerrada en una cuenta de almacenamiento DISTINTA a la de producción
/// (<c>Induccion:Respaldo:ConnectionString</c>, contenedor <c>Induccion:Respaldo:Container</c>, por defecto
/// «induccion-respaldo»). Copia el acta, las firmas, las selfies y las versiones exactas de los documentos
/// entregados, más un <c>manifiesto.json</c> con el SHA-256 de cada archivo y los datos del sello de tiempo.
/// Nunca sobrescribe: si un archivo ya existe en el respaldo se deja como está (compatible con una política de
/// inmutabilidad WORM del contenedor). El contenedor se crea PRIVADO, a diferencia del de producción.
/// </summary>
public class InduccionRespaldoService : IInduccionRespaldoService
{
    private const int MaxIntentos = 10;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly IInduccionRepository _repository;
    private readonly IInduccionPostCierreRepository _postCierre;
    private readonly IConfiguration _config;
    private readonly ILogger<InduccionRespaldoService> _log;

    public InduccionRespaldoService(
        IInduccionRepository repository,
        IInduccionPostCierreRepository postCierre,
        IConfiguration config,
        ILogger<InduccionRespaldoService> log)
    {
        _repository = repository;
        _postCierre = postCierre;
        _config = config;
        _log = log;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_config["Induccion:Respaldo:ConnectionString"]);

    private sealed record Archivo(string Ruta, string Origen, string Categoria);
    private sealed record ArchivoCopiado(string Ruta, string Origen, string Categoria, string Sha256, long Bytes);

    public async Task<bool> RespaldarAsync(int sesionId, CancellationToken ct = default)
    {
        if (!Configurado) return false;

        var s = await _repository.GetSesion(sesionId);
        if (s is null || s.CerradaEn is null) return false;

        var fila = await _postCierre.GetOCrearRespaldo(sesionId);
        if (fila.Estado == "Respaldado") return true;
        if (fila.Estado == "Error") return false;

        var prefijo = $"{s.Fecha:yyyy}/{s.ProyectoId}/{s.Codigo ?? $"sesion-{s.Id}"}";
        try
        {
            var contenedor = new BlobServiceClient(_config["Induccion:Respaldo:ConnectionString"])
                .GetBlobContainerClient(_config["Induccion:Respaldo:Container"] is { Length: > 0 } c ? c : "induccion-respaldo");
            await contenedor.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

            // ── Qué se copia ──
            var archivos = new List<Archivo>();
            void Agregar(string? url, string ruta, string categoria)
            {
                if (!string.IsNullOrWhiteSpace(url)) archivos.Add(new Archivo(ruta, url, categoria));
            }

            Agregar(s.ActaPdfUrl, $"acta/{s.Codigo ?? s.Id.ToString()}.pdf", "acta");
            Agregar(s.InstructorFirmaUrl, "firmas/instructor.png", "firma");
            foreach (var d in s.Documentos.Where(d => d.DocumentoVersion != null))
            {
                var v = d.DocumentoVersion!;
                Agregar(v.ArchivoUrl, $"documentos/{v.Documento?.Codigo ?? v.DocumentoId.ToString()}_v{v.Version}{Extension(v.ArchivoUrl)}", "documento");
            }
            foreach (var a in s.Asistencias.Where(a => a.Estado == "Firmado"))
            {
                Agregar(a.FirmaUrl, $"firmas/{a.Id}_{a.DocumentoIdentidad}.png", "firma");
                Agregar(a.SelfieUrl, $"selfies/{a.Id}_{a.DocumentoIdentidad}.jpg", "selfie");
            }

            // ── Copia con hash ──
            var copiados = new List<ArchivoCopiado>();
            long total = 0;
            foreach (var f in archivos)
            {
                var bytes = await Http.GetByteArrayAsync(f.Origen, ct);
                var sha = Convert.ToHexString(SHA256.HashData(bytes));
                await SubirSinSobrescribir(contenedor, $"{prefijo}/{f.Ruta}", bytes, ct);
                copiados.Add(new ArchivoCopiado(f.Ruta, f.Origen, f.Categoria, sha, bytes.LongLength));
                total += bytes.LongLength;
            }

            // ── Manifiesto: lo que permite auditar el respaldo sin este sistema ──
            var sello = await _postCierre.GetSello(sesionId);
            var manifiesto = new
            {
                generadoEn = DateTime.UtcNow,
                sesion = new
                {
                    s.Id, s.Codigo, s.ProyectoId, proyecto = s.Proyecto?.ProjectDescription, fecha = s.Fecha.ToString("yyyy-MM-dd"),
                    s.Horas, s.InstructorNombre, s.CerradaEn, s.ActaPdfHash, s.ActaPdfSha256, s.InstructorFirmaHash, evaluacionVersion = s.EvaluacionVersion?.Version,
                },
                documentos = s.Documentos.Where(d => d.DocumentoVersion != null).Select(d => new
                {
                    codigo = d.DocumentoVersion!.Documento?.Codigo, d.DocumentoVersion.Version, sha256 = d.DocumentoVersion.ArchivoHash,
                }),
                trabajadores = s.Asistencias.Where(a => a.Estado == "Firmado").Select(a => new
                {
                    a.Id, a.WorkerId, a.NombreCompleto, a.DocumentoIdentidad, a.Nota, a.Aprobado, a.Intentos, a.HoraServidorFirma,
                    a.Lat, a.Lng, a.SelfieHash, a.FirmaHash, a.HashVerificacion, validada = a.ValidadoEn,
                }),
                archivos = copiados,
                selloDeTiempo = sello == null ? null : new
                {
                    sello.Estado, sello.Digest, sello.Componentes, sello.Proveedor, sello.SelladoEn, sello.SerialTsa, sello.EmisorTsa,
                    tokenBase64 = sello.Token == null ? null : Convert.ToBase64String(sello.Token),
                },
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(manifiesto, new JsonSerializerOptions { WriteIndented = true });
            await SubirSinSobrescribir(contenedor, $"{prefijo}/manifiesto.json", json, ct);

            await _postCierre.RegistrarResultadoRespaldo(fila.Id, true, copiados.Count + 1, total + json.LongLength, prefijo, null, MaxIntentos);
            _log.LogInformation("[Inducción] Sesión {Id} respaldada ({N} archivos) en {Prefijo}", sesionId, copiados.Count + 1, prefijo);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Inducción] No se pudo respaldar la sesión {Id}", sesionId);
            await _postCierre.RegistrarResultadoRespaldo(fila.Id, false, 0, 0, null, ex.Message, MaxIntentos);
            return false;
        }
    }

    /// <summary>Sube solo si el blob NO existe: un respaldo jamás pisa lo ya guardado.</summary>
    private static async Task SubirSinSobrescribir(BlobContainerClient contenedor, string nombre, byte[] bytes, CancellationToken ct)
    {
        var blob = contenedor.GetBlobClient(nombre);
        try
        {
            using var ms = new MemoryStream(bytes);
            await blob.UploadAsync(ms, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                HttpHeaders = new BlobHttpHeaders { ContentType = TipoContenido(nombre) },
            }, ct);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // Ya estaba respaldado (reintento tras una falla a medias): se deja como está.
        }
    }

    private static string Extension(string url)
    {
        var limpio = url.Split('?')[0];
        var ext = Path.GetExtension(limpio);
        return string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant();
    }

    private static string TipoContenido(string nombre) => Path.GetExtension(nombre).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".json" => "application/json",
        _ => "application/octet-stream",
    };
}
