using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Services;

/// <summary>
/// Acta de inducción: reemplaza SSO-FO-118/119 (entrega y difusión de documentos), SSO-FO-031 (temario y
/// declaración "Derecho a Saber"), SSO-FO-091 (resultado de la evaluación) y GTH-FO-17 (registro de asistentes).
/// Misma estructura que AtsPdfService: cabecera corporativa, evidencia por trabajador y un QR que apunta a la
/// verificación pública.
/// </summary>
public class InduccionActaPdfService : IInduccionActaPdfService
{
    private const string Titulo = "ACTA DE INDUCCIÓN DE SEGURIDAD Y SALUD EN EL TRABAJO";
    private const string Subtitulo = "Derecho a Saber · Entrega de documentos · Evaluación";
    private const string TituloReInduccion = "ACTA DE RE-INDUCCIÓN DE SEGURIDAD Y SALUD EN EL TRABAJO";
    private const string TituloActualizacion = "ACTA DE ACTUALIZACIÓN DE DOCUMENTOS DE SEGURIDAD Y SALUD";
    private const string SubtituloActualizacion = "Recepción de documentos modificados · Re-aceptación";

    public byte[] Generar(ActaInduccionDatos d)
    {
        var s = d.Sesion;
        byte[]? qrBytes;
        using (var generator = new QRCodeGenerator())
        using (var data = generator.CreateQrCode(d.VerificacionUrl, QRCodeGenerator.ECCLevel.M))
            qrBytes = new PngByteQRCode(data).GetGraphic(8);

        var firmados = s.Asistencias.Where(a => a.Estado == "Firmado").OrderBy(a => a.NombreCompleto).ToList();
        var esActualizacion = s.Tipo == "Actualizacion";
        var titulo = esActualizacion ? TituloActualizacion : s.Tipo == "ReInduccion" ? TituloReInduccion : Titulo;
        var subtitulo = esActualizacion ? SubtituloActualizacion : Subtitulo;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Element(c => ComposeHeader(c, d.Logo, s.Codigo, titulo, subtitulo));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Proyecto: {s.ProyectoNombre}").Bold();
                            c.Item().Text($"Fecha de inducción: {s.Fecha:dd/MM/yyyy}");
                            c.Item().Text($"Duración: {s.Horas:0.##} horas");
                            c.Item().Text($"Instructor: {s.InstructorNombre} — {s.InstructorCargo ?? "—"}");
                        });
                        row.ConstantItem(70).Height(70).Image(qrBytes).FitArea();
                    });

                    // Temario (SSO-FO-031) — una Actualización de documentos no lleva temario.
                    if (s.Temas.Count > 0)
                    {
                        col.Item().PaddingTop(4).Text("Temas tratados").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.ConstantColumn(18); c.RelativeColumn(); c.ConstantColumn(18); c.RelativeColumn(); });
                            var mitad = (s.Temas.Count + 1) / 2;
                            for (var i = 0; i < mitad; i++)
                            {
                                Celda(table, s.Temas[i].Orden.ToString(), true);
                                Celda(table, s.Temas[i].Descripcion);
                                if (i + mitad < s.Temas.Count)
                                {
                                    Celda(table, s.Temas[i + mitad].Orden.ToString(), true);
                                    Celda(table, s.Temas[i + mitad].Descripcion);
                                }
                                else
                                {
                                    Celda(table, string.Empty, true);
                                    Celda(table, string.Empty);
                                }
                            }
                        });
                    }

                    // Documentos entregados, con versión y hash (SSO-FO-118/119)
                    col.Item().PaddingTop(4).Text("Documentos entregados y difundidos").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(); c.ConstantColumn(38); c.ConstantColumn(95); });
                        table.Header(h =>
                        {
                            Encabezado(h, "Código"); Encabezado(h, "Documento"); Encabezado(h, "Versión"); Encabezado(h, "Hash (SHA-256)");
                        });
                        foreach (var doc in s.Documentos)
                        {
                            Celda(table, doc.Codigo);
                            Celda(table, doc.Nombre);
                            Celda(table, $"v{doc.Version}");
                            Celda(table, doc.ArchivoHash[..Math.Min(16, doc.ArchivoHash.Length)] + "…");
                        }
                    });

                    if (!esActualizacion)
                        col.Item().Text($"Evaluación: versión {s.EvaluacionVersion} · nota mínima aprobatoria {s.NotaMinima:0.##} · intentos ilimitados.")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);

                    // Declaración y compromiso
                    col.Item().PaddingTop(4).Text("Declaración y compromiso").Bold();
                    col.Item().Text(d.DeclaracionTexto).FontSize(7.5f).Justify();
                    col.Item().Text($"Texto de declaración: {d.DeclaracionVersion}. Cada trabajador la aceptó al firmar.").FontSize(7).FontColor(Colors.Grey.Darken1);

                    // Registro de asistentes con evidencia (GTH-FO-17)
                    col.Item().PaddingTop(6).Text($"Trabajadores inducidos ({firmados.Count})").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(18);   // N°
                            c.RelativeColumn(3);    // Apellidos y nombres
                            c.ConstantColumn(52);   // DNI
                            c.RelativeColumn(2);    // Categoría / empresa
                            c.ConstantColumn(30);   // Nota
                            c.ConstantColumn(34);   // Selfie
                            c.ConstantColumn(62);   // Firma
                            c.ConstantColumn(62);   // Fecha y hora
                        });
                        table.Header(h =>
                        {
                            Encabezado(h, "N°"); Encabezado(h, "Apellidos y nombres"); Encabezado(h, "DNI");
                            Encabezado(h, "Categoría / Empresa"); Encabezado(h, "Nota"); Encabezado(h, "Selfie");
                            Encabezado(h, "Firma"); Encabezado(h, "Firmado");
                        });

                        var n = 1;
                        foreach (var a in firmados)
                        {
                            Celda(table, (n++).ToString(), true);
                            Celda(table, a.NombreCompleto ?? "—");
                            Celda(table, a.DocumentoIdentidad ?? "—");
                            Celda(table, $"{a.Categoria ?? "—"}{(string.IsNullOrWhiteSpace(a.ContratistaNombre) ? "" : " / " + a.ContratistaNombre)}");
                            Celda(table, a.Nota?.ToString("0.##") ?? "—");
                            Imagen(table, d.SelfiesTrabajadores.GetValueOrDefault(a.Id), 30);
                            Imagen(table, d.FirmasTrabajadores.GetValueOrDefault(a.Id), 30);
                            Celda(table, FechaHoraPeru(a.HoraServidorFirma));
                        }
                    });

                    col.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Element(c => FirmaBloque(c, "Instructor", s.InstructorNombre, s.InstructorCargo, s.CerradaEn, d.FirmaInstructor));
                        row.RelativeItem(2).AlignBottom().Column(c =>
                        {
                            c.Item().Text($"Código de verificación: {(s.ActaPdfHash is { Length: > 0 } h ? h[..Math.Min(16, h.Length)] + "…" : "—")}").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text("Estado: inmutable tras el cierre. Auditoría con hash encadenado.").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Verificar autenticidad: ").FontSize(7);
                    t.Span(d.VerificacionUrl).FontSize(7).FontColor(Colors.Blue.Darken2);
                    t.Span("   ·   Página ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" de ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    private static void Encabezado(TableCellDescriptor h, string texto)
        => h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(texto).Bold().FontSize(7.5f);

    private static void Celda(TableDescriptor table, string texto, bool centrado = false)
    {
        var c = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
        if (centrado) c.AlignCenter().Text(texto).FontSize(7.5f);
        else c.Text(texto).FontSize(7.5f);
    }

    private static void Imagen(TableDescriptor table, byte[]? bytes, float alto)
    {
        var c = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Height(alto + 4);
        if (bytes is { Length: > 0 }) c.Image(bytes).FitArea();
        else c.AlignMiddle().AlignCenter().Text("—").FontSize(7);
    }

    private static void FirmaBloque(IContainer container, string rol, string? nombre, string? cargo, DateTime? hora, byte[]? firma)
    {
        container.Column(c =>
        {
            c.Item().Height(40).AlignCenter().Element(inner =>
            {
                if (firma is { Length: > 0 }) inner.Image(firma).FitArea();
                else inner.AlignMiddle().AlignCenter().Text("PENDIENTE").FontSize(9).Bold().FontColor(Colors.Red.Darken2);
            });
            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(2).Text(rol).FontSize(7).Bold();
            c.Item().Text(nombre ?? "—").FontSize(7);
            if (!string.IsNullOrWhiteSpace(cargo)) c.Item().Text(cargo).FontSize(6.5f).FontColor(Colors.Grey.Darken1).Italic();
            if (hora.HasValue) c.Item().Text(FechaHoraPeru(hora)).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    /// <summary>Logo | título | código de sesión — mismo patrón de cabecera que el ATS y el resto de formatos SSOMA.</summary>
    private static void ComposeHeader(IContainer container, byte[]? logo, string? codigo, string titulo, string subtitulo)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Row(row =>
        {
            row.ConstantItem(90).AlignMiddle().AlignCenter().Padding(4).Element(el =>
            {
                if (logo != null) el.AlignMiddle().AlignCenter().Image(logo).FitArea();
                else el.AlignMiddle().AlignCenter().Text("ABRIL").Bold().FontSize(8);
            });
            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);
            row.RelativeItem().AlignMiddle().AlignCenter().Column(t =>
            {
                t.Item().AlignCenter().Text(titulo).Bold().FontSize(10.5f).AlignCenter();
                t.Item().AlignCenter().Text(subtitulo).FontSize(8).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(codigo)) t.Item().AlignCenter().Text($"N.° {codigo}").Bold().FontSize(9);
            });
        });
    }

    /// <summary>Perú no tiene horario de verano: se imprime siempre en -05:00.</summary>
    private static string FechaHoraPeru(DateTime? utc)
    {
        if (utc is null) return "—";
        var u = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        return new DateTimeOffset(u).ToOffset(TimeSpan.FromHours(-5)).ToString("dd/MM/yyyy HH:mm");
    }
}
