using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Services;

/// <summary>
/// Genera el PDF del ATS firmado con formato SSO-FO-018.m "ATS SUPERVISIÓN": cabecera, peligros
/// identificados con su medida de control, y el bloque de evidencia (selfie, firma, geolocalización,
/// fecha/hora de servidor) con un QR que apunta a la URL de verificación — cualquiera con el link
/// puede comparar el hash del documento contra lo que quedó guardado en el sistema.
/// </summary>
public static class AtsPdfService
{
    // ── Encabezado corporativo estándar (mismo patrón que RAC/Inspecciones/Convalidación) ──
    private const string Codigo = "SSO-FO-018";
    private const string Titulo = "ANÁLISIS DE TRABAJO SEGURO (ATS)";

    public static byte[] Generar(
        AtsResponseDto ats, byte[]? selfieBytes, byte[]? firmaBytes, string verificacionUrl,
        byte[]? firmaAutorizaBytes = null, byte[]? firmaSsomaBytes = null, byte[]? logoBytes = null,
        byte[]? firmaCapatazBytes = null, string? petarCodigos = null)
    {
        byte[]? qrBytes = null;
        using (var generator = new QRCodeGenerator())
        using (var data = generator.CreateQrCode(verificacionUrl, QRCodeGenerator.ECCLevel.M))
        {
            var png = new PngByteQRCode(data);
            qrBytes = png.GetGraphic(8);
        }

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Element(c => ComposeHeader(c, logoBytes, ats.PlantillaNombre, ats.Codigo, petarCodigos));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Trabajador: {ats.WorkerNombre}").Bold();
                            c.Item().Text($"Puesto: {ats.PuestoNombre ?? "—"}");
                            c.Item().Text($"Proyecto: {ats.ProyectoNombre}");
                            c.Item().Text($"Actividad: {ats.Actividad}");
                            if (!string.IsNullOrWhiteSpace(ats.TorreNombre))
                                c.Item().Text($"Torre: {ats.TorreNombre} — Piso(s): {ats.Pisos}");
                            if (!string.IsNullOrWhiteSpace(ats.Lugar))
                                c.Item().Text($"Lugar: {ats.Lugar}");
                            c.Item().Text($"Fecha: {ats.Fecha:dd/MM/yyyy}");
                        });
                    });

                    ComposeDetalle(col, ats);

                    col.Item().PaddingTop(10).Text("Evidencia de trazabilidad").Bold();
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text($"Firmado digitalmente: {FechaHoraPeru(ats.HoraServidorFirma)}").FontSize(8);
                            if (ats.OrigenOffline && ats.HoraDispositivo.HasValue)
                                c.Item().Text($"Capturado sin conexión el {FechaHoraPeru(ats.HoraDispositivo)} (hora del dispositivo)").FontSize(8).Bold();
                            c.Item().Text($"Geolocalización: {FormatGeo(ats.Lat, ats.Lng, ats.PrecisionMetros)}").FontSize(8);
                            c.Item().Text($"Estado: {ats.Estado}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(ats.PdfHash))
                                c.Item().Text($"Hash del documento: {ats.PdfHash[..Math.Min(16, ats.PdfHash.Length)]}…").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });

                        if (selfieBytes is { Length: > 0 })
                            row.ConstantItem(90).Height(90).Image(selfieBytes).FitArea();

                        if (qrBytes is { Length: > 0 })
                            row.ConstantItem(70).Height(70).Image(qrBytes).FitArea();
                    });

                    col.Item().PaddingTop(14).Text("Firmas").Bold();
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => FirmaBloque(c, "Ejecutante", ats.WorkerNombre, ats.PuestoNombre, ats.HoraServidorFirma, firmaBytes));
                        if (ats.RequiereCapataz)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Capataz / Maestro de Obra", ats.CapatazNombre, ats.CapatazCargo, ats.CapatazHoraServidor, firmaCapatazBytes));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Autoriza (Residente / Ing. Producción)", ats.AutorizaNombre, ats.AutorizaCargo, ats.AutorizaHoraServidor, firmaAutorizaBytes));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Visto Bueno SSOMA", ats.SsomaNombre, ats.SsomaCargo, ats.SsomaHoraServidor, firmaSsomaBytes));
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Verificar autenticidad: ").FontSize(7);
                    t.Span(verificacionUrl).FontSize(7).FontColor(Colors.Blue.Darken2);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Pasos, EPP, herramientas e IPERC — idéntico en el ATS individual y en el grupal.</summary>
    private static void ComposeDetalle(ColumnDescriptor col, AtsResponseDto ats)
    {
        if (ats.Pasos.Any(p => p.Aplica))
                    {
                        col.Item().PaddingTop(6).Text("Actividades y pasos de la tarea").Bold();
                        foreach (var grupo in ats.Pasos.Where(p => p.Aplica).GroupBy(p => p.CategoriaNombre))
                        {
                            col.Item().Text(grupo.Key).SemiBold().FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                            foreach (var p in grupo)
                                col.Item().Text($"☑ {p.Texto}").FontSize(8.5f);
                        }
                    }

                    if (ats.Epps.Count > 0)
                        col.Item().PaddingTop(6).Text("EPP: " + string.Join(", ", ats.Epps)).FontSize(8.5f);
                    if (ats.Herramientas.Count > 0)
                        col.Item().Text("Herramientas/equipos: " + string.Join(", ", ats.Herramientas)).FontSize(8.5f);

                    col.Item().PaddingTop(6).Text("Peligros, riesgos y controles (IPERC)").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.ConstantColumn(45);
                            c.RelativeColumn(3);
                            c.ConstantColumn(45);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Peligro").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Riesgo").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("R. Base").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Controles").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("R. Resid.").Bold();
                        });

                        foreach (var d in ats.Riesgos)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(d.PeligroNombre).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(d.RiesgoNombre).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(NivelLabel(d.RiesgoBase)).FontSize(8).FontColor(NivelColor(d.RiesgoBase)).Bold();
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(d.Controles).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(NivelLabel(d.RiesgoResidual)).FontSize(8).FontColor(NivelColor(d.RiesgoResidual)).Bold();
                        }
                    });
    }

    public record GrupalFila(string Nombre, string? Puesto, DateTime? Hora, byte[]? Selfie, byte[]? Firma, decimal? Lat, decimal? Lng, bool Offline = false, DateTime? HoraDispositivo = null);
    public record FirmaCadena(string? Nombre, string? Cargo, DateTime? Hora, byte[]? Firma);

    /// <summary>ATS GRUPAL: un solo documento por cuadrilla con la firma (y selfie/hora/geo) de CADA trabajador
    /// y una única firma de Capataz/Maestro (solo cuadrillas de obreros), Autoriza (Residente/Producción) y
    /// Visto Bueno SSOMA. Mismo formato SSO-FO-018 que el ATS individual.</summary>
    public static byte[] GenerarGrupal(
        AtsResponseDto contenido, string numeroDocumento, List<GrupalFila> filas, bool requiereCapataz,
        FirmaCadena capataz, FirmaCadena autoriza, FirmaCadena ssoma, byte[]? logoBytes, string? petarCodigos = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Element(c => ComposeHeader(c, logoBytes, contenido.PlantillaNombre, numeroDocumento, petarCodigos));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(10).Column(c =>
                    {
                        c.Item().Text($"Proyecto: {contenido.ProyectoNombre}").Bold();
                        c.Item().Text($"Actividad: {contenido.Actividad}");
                        if (!string.IsNullOrWhiteSpace(contenido.TorreNombre))
                            c.Item().Text($"Torre: {contenido.TorreNombre} — Piso(s): {contenido.Pisos}");
                        if (!string.IsNullOrWhiteSpace(contenido.Lugar))
                            c.Item().Text($"Lugar: {contenido.Lugar}");
                        c.Item().Text($"Fecha: {contenido.Fecha:dd/MM/yyyy}");
                        c.Item().Text($"Trabajadores de la cuadrilla: {filas.Count}");
                    });

                    ComposeDetalle(col, contenido);

                    col.Item().PaddingTop(10).Text($"Trabajadores que firman ({filas.Count(f => f.Firma is { Length: > 0 })} de {filas.Count})").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.ConstantColumn(40);
                            c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("N°").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Trabajador").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Firmado (hora / GPS)").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Selfie").Bold().FontSize(8);
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Firma").Bold().FontSize(8);
                        });

                        var n = 0;
                        foreach (var f in filas)
                        {
                            n++;
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignMiddle().Text(n.ToString()).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignMiddle().Column(c =>
                            {
                                c.Item().Text(f.Nombre).FontSize(8).Bold();
                                if (!string.IsNullOrWhiteSpace(f.Puesto))
                                    c.Item().Text(f.Puesto).FontSize(7).FontColor(Colors.Grey.Darken1);
                            });
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignMiddle().Column(c =>
                            {
                                if (f.Offline && f.HoraDispositivo.HasValue)
                                {
                                    c.Item().Text($"Capturado sin conexión: {FechaHoraPeru(f.HoraDispositivo)}").FontSize(7.5f).Bold();
                                    c.Item().Text($"Sincronizado: {FechaHoraPeru(f.Hora)}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                    c.Item().Text(FechaHoraPeru(f.Hora)).FontSize(7.5f);
                                c.Item().Text(FormatGeo(f.Lat, f.Lng, null)).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                            });
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Height(36).Element(inner =>
                            {
                                if (f.Selfie is { Length: > 0 }) inner.Image(f.Selfie).FitArea();
                            });
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Height(36).Element(inner =>
                            {
                                if (f.Firma is { Length: > 0 })
                                    inner.Image(f.Firma).FitArea();
                                else
                                    inner.AlignMiddle().AlignCenter().Text("PENDIENTE").FontSize(8).Bold().FontColor(Colors.Red.Darken2);
                            });
                        }
                    });

                    col.Item().PaddingTop(14).Text("Firmas de validación (una por cuadrilla)").Bold();
                    col.Item().ShowEntire().Row(row =>
                    {
                        if (requiereCapataz)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Capataz / Maestro de Obra", capataz.Nombre, capataz.Cargo, capataz.Hora, capataz.Firma));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Autoriza (Residente / Ing. Producción)", autoriza.Nombre, autoriza.Cargo, autoriza.Hora, autoriza.Firma));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Visto Bueno SSOMA", ssoma.Nombre, ssoma.Cargo, ssoma.Hora, ssoma.Firma));
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("ATS grupal — cada trabajador firmó con su selfie y geolocalización · Página ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" de ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Un bloque de firma: imagen (o "PENDIENTE" en rojo si aún no la tiene), rol, nombre,
    /// cargo y hora. Usado para las 3 firmas del ATS (ejecutante, autoriza, visto bueno SSOMA).</summary>
    private static void FirmaBloque(QuestPDF.Infrastructure.IContainer container, string rol, string? nombre, string? cargo, DateTime? hora, byte[]? firma)
    {
        container.Column(c =>
        {
            c.Item().Height(40).AlignCenter().Element(inner =>
            {
                if (firma is { Length: > 0 })
                    inner.Image(firma).FitArea();
                else
                    inner.AlignMiddle().AlignCenter().Text("PENDIENTE").FontSize(9).Bold().FontColor(Colors.Red.Darken2);
            });
            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(2).Text(rol).FontSize(7).Bold();
            c.Item().Text(nombre ?? "—").FontSize(7);
            if (!string.IsNullOrWhiteSpace(cargo))
                c.Item().Text(cargo).FontSize(6.5f).FontColor(Colors.Grey.Darken1).Italic();
            if (hora.HasValue)
                c.Item().Text(FechaHoraPeru(hora)).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    /// <summary>Logo | título centrado | Código/Versión/Fecha + Elab./Rev./Apro. — mismo patrón
    /// que ConvalidacionPdfService/RacPdfService, el estándar corporativo de formatos SSOMA.</summary>
    private static void ComposeHeader(IContainer container, byte[]? logoBytes, string? plantillaNombre, string? numeroDocumento, string? petarCodigos = null)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Row(row =>
        {
            row.ConstantItem(90).AlignMiddle().AlignCenter().Padding(4).Element(logoEl =>
            {
                if (logoBytes != null)
                    logoEl.AlignMiddle().AlignCenter().Image(logoBytes).FitArea();
                else
                    logoEl.AlignMiddle().AlignCenter().Text("ABRIL").Bold().FontSize(8).AlignCenter();
            });

            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

            row.RelativeItem().AlignMiddle().AlignCenter().Column(tCol =>
            {
                tCol.Item().AlignCenter().Text(Titulo).Bold().FontSize(11).AlignCenter();
                if (!string.IsNullOrWhiteSpace(plantillaNombre))
                    tCol.Item().AlignCenter().Text(plantillaNombre).FontSize(8).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(numeroDocumento))
                    tCol.Item().AlignCenter().Text($"N.° {numeroDocumento}").Bold().FontSize(9);
                if (!string.IsNullOrWhiteSpace(petarCodigos))
                    tCol.Item().AlignCenter().Text($"PETAR vinculado: {petarCodigos}").Bold().FontSize(8);
            });

            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

            row.ConstantItem(120).Column(metaCol =>
            {
                void MetaRow(string label, string valor, bool last = false)
                {
                    metaCol.Item()
                        .BorderBottom(last ? 0f : 0.5f)
                        .Padding(2).Row(r =>
                    {
                        r.AutoItem().Text(label).Bold().FontSize(7);
                        r.ConstantItem(2);
                        r.RelativeItem().Text(valor).FontSize(7);
                    });
                }
                MetaRow("Código:", Codigo);
                MetaRow("Versión:", "01");
                MetaRow("Fecha:", DateTime.Today.ToString("dd/MM/yyyy"));

                metaCol.Item().BorderTop(0.5f).Row(subRow =>
                {
                    foreach (var (lbl, val, last) in new[]
                    {
                        ("Elab.:", "SSOMA",  false),
                        ("Rev.:",  "JSSOMA", false),
                        ("Apro.:", "GP",     true ),
                    })
                    {
                        var cell = subRow.RelativeItem().Padding(2);
                        if (!last) cell = cell.BorderRight(0.5f);
                        cell.Text(t =>
                        {
                            t.Span(lbl + " ").Bold().FontSize(5.5f);
                            t.Span(val).FontSize(5.5f);
                        });
                    }
                });
            });
        });
    }

    private static string NivelLabel(string nivel) => nivel switch { "A" => "ALTO", "M" => "MEDIO", "B" => "BAJO", _ => nivel };

    private static string NivelColor(string nivel) => nivel switch
    {
        "A" => Colors.Red.Darken2,
        "M" => Colors.Orange.Darken2,
        "B" => Colors.Green.Darken2,
        _ => Colors.Black,
    };

    private static string FormatGeo(decimal? lat, decimal? lng, decimal? precision)
    {
        if (lat is null || lng is null) return "Sin datos de GPS (permiso denegado o no disponible)";
        var precisionTxt = precision.HasValue ? $" (±{precision:0}m)" : string.Empty;
        return $"{lat:0.000000}, {lng:0.000000}{precisionTxt}";
    }

    /// <summary>Perú no tiene horario de verano: se imprime siempre en -05:00 (mismo criterio que SignaturePdfStamper).</summary>
    private static string FechaHoraPeru(DateTime? horaServidorUtc)
    {
        if (horaServidorUtc is null) return "—";
        var utc = DateTime.SpecifyKind(horaServidorUtc.Value, DateTimeKind.Utc);
        var peru = new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(-5));
        return peru.ToString("dd/MM/yyyy HH:mm");
    }
}
