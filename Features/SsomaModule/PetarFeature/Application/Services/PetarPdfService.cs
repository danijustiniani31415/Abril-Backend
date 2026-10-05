using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Services;

public record PetarGrupalFila(string Nombre, DateTime? Hora, decimal? Lat, decimal? Lng, byte[]? Selfie, byte[]? Firma);

public record PetarGrupalFirma(string? Nombre, string? Cargo, DateTime? Hora, byte[]? Firma);

public record PetarGrupalDatos(
    string? TipoCodigo, string? TipoNombre, string Numero, string? AtsCodigo, string? ProyectoNombre,
    string Descripcion, string? Lugar, DateOnly Fecha, TimeOnly? HoraInicio, TimeOnly? HoraFin,
    List<(string Texto, string Respuesta)> Checklist);

/// <summary>Genera el PDF del PETAR — checklist de verificación + las 3 firmas (Ejecutante,
/// Supervisor, SSOMA) + QR de verificación, mismo patrón que AtsPdfService.</summary>
public static class PetarPdfService
{
    /// <summary>PETAR GRUPAL: un solo documento por cuadrilla — mismo formato/cabecera que el individual, con la
    /// firma (selfie/hora/GPS) de CADA trabajador y una única firma de Supervisor y de Visto Bueno SSOMA.</summary>
    public static byte[] GenerarGrupal(PetarGrupalDatos d, List<PetarGrupalFila> filas, PetarGrupalFirma? capataz, PetarGrupalFirma supervisor, PetarGrupalFirma ssoma, byte[]? logoBytes, string verificacionUrl, string hashCorto)
    {
        byte[] qrBytes;
        using (var generator = new QRCodeGenerator())
        using (var data = generator.CreateQrCode(verificacionUrl, QRCodeGenerator.ECCLevel.M))
            qrBytes = new PngByteQRCode(data).GetGraphic(8);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Element(c => ComposeHeader(c, logoBytes, d.TipoCodigo, d.TipoNombre, d.Numero, d.AtsCodigo));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(10).Column(c =>
                    {
                        c.Item().Text($"Proyecto: {d.ProyectoNombre}").Bold();
                        c.Item().Text($"Trabajo a realizar: {d.Descripcion}");
                        if (!string.IsNullOrWhiteSpace(d.Lugar))
                            c.Item().Text($"Lugar: {d.Lugar}");
                        c.Item().Text($"Fecha: {d.Fecha:dd/MM/yyyy}" + (d.HoraInicio != null ? $"   Horario: {d.HoraInicio} - {d.HoraFin}" : ""));
                        c.Item().Text($"Trabajadores de la cuadrilla: {filas.Count}");
                    });

                    col.Item().PaddingTop(6).Text("Checklist de verificación previa").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(5); c.ConstantColumn(50); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Ítem").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Resp.").Bold();
                        });
                        foreach (var (texto, respuesta) in d.Checklist)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(texto).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter()
                                .Text(respuesta).FontSize(8).Bold()
                                .FontColor(respuesta == "NO" ? Colors.Red.Darken2 : Colors.Green.Darken2);
                        }
                    });

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
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignMiddle().Text(f.Nombre).FontSize(8).Bold();
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignMiddle().Column(c =>
                            {
                                c.Item().Text(FechaHoraPeru(f.Hora)).FontSize(7.5f);
                                if (f.Lat.HasValue && f.Lng.HasValue)
                                    c.Item().Text($"{f.Lat:0.00000}, {f.Lng:0.00000}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
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
                        if (capataz != null)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Capataz / Maestro de Obra", capataz.Nombre, capataz.Cargo, capataz.Hora, capataz.Firma));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Supervisor (Residente / Producción)", supervisor.Nombre, supervisor.Cargo, supervisor.Hora, supervisor.Firma));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Visto Bueno SSOMA", ssoma.Nombre, ssoma.Cargo, ssoma.Hora, ssoma.Firma));
                    });

                    col.Item().PaddingTop(10).ShowEntire().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Documento: {d.Numero}" + (string.IsNullOrWhiteSpace(d.AtsCodigo) ? "" : $"  ·  ATS vinculado: {d.AtsCodigo}")).FontSize(8).Bold();
                            c.Item().Text($"Código de verificación: {hashCorto}").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(70).Height(70).Image(qrBytes).FitArea();
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

    public static byte[] Generar(
        PetarResponseDto p, byte[]? selfieBytes, byte[]? firmaBytes, string verificacionUrl,
        byte[]? firmaSupervisorBytes = null, byte[]? firmaSsomaBytes = null, byte[]? logoBytes = null,
        byte[]? firmaCapatazBytes = null)
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

                page.Header().Element(c => ComposeHeader(c, logoBytes, p.TipoCodigo, p.TipoNombre, p.Codigo, p.AtsCodigo));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(10).Column(c =>
                    {
                        c.Item().Text($"Ejecutante: {p.WorkerNombre}").Bold();
                        c.Item().Text($"Proyecto: {p.ProyectoNombre}");
                        c.Item().Text($"Trabajo a realizar: {p.DescripcionTrabajo}");
                        if (!string.IsNullOrWhiteSpace(p.Lugar))
                            c.Item().Text($"Lugar: {p.Lugar}");
                        c.Item().Text($"Fecha: {p.Fecha:dd/MM/yyyy}" + (p.HoraInicio != null ? $"   Horario: {p.HoraInicio} - {p.HoraFin}" : ""));
                        c.Item().Text($"ATS de origen: #{p.AtsId}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    if (p.IzajeGrua is { } g)
                    {
                        col.Item().PaddingTop(6).Text("Datos técnicos del izaje").Bold();
                        col.Item().Column(c =>
                        {
                            var tipoGrua = g.TipoGrua == "TorreGrua" ? "Torre grúa" : g.TipoGrua == "GruaMovil" ? "Grúa móvil" : g.TipoGrua;
                            c.Item().Text($"Tipo de grúa: {tipoGrua ?? "—"}   Fabricante/marca: {g.FabricanteOMarca ?? "—"}").FontSize(8);
                            c.Item().Text($"Modelo/placa: {g.ModeloOPlaca ?? "—"}   Serie/tarjeta de circulación: {g.SerieOTarjetaCirculacion ?? "—"}").FontSize(8);
                            c.Item().Text($"Longitud de pluma/brazo: {g.LongitudPlumaBrazoM?.ToString() ?? "—"} m   Ángulo de pluma: {g.AnguloPluma?.ToString() ?? "—"}°").FontSize(8);
                            c.Item().Text($"Radio máximo de giro: {g.RadioMaximoGiroM?.ToString() ?? "—"} m   Elevación: {g.ElevacionM?.ToString() ?? "—"} m   Dirección/grado de giro: {g.DireccionGradoGiro ?? "—"}").FontSize(8);
                            c.Item().Text($"Capacidad certificada de fábrica: {g.CapacidadCertificadaTon?.ToString() ?? "—"} ton   Peso de la carga: {g.PesoCargaTotalTon?.ToString() ?? "—"} ton   % de capacidad usado: {g.PorcentajeCapacidad?.ToString() ?? "—"}%").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(g.TamanoEstrobo))
                                c.Item().Text($"Tamaño del estrobo: {g.TamanoEstrobo}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(g.Observaciones))
                                c.Item().Text($"Observaciones: {g.Observaciones}").FontSize(8);
                        });
                    }

                    col.Item().PaddingTop(6).Text("Checklist de verificación previa").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(5); c.ConstantColumn(50); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Ítem").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Resp.").Bold();
                        });
                        foreach (var r in p.Respuestas)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.Texto).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter()
                                .Text(r.Respuesta).FontSize(8).Bold()
                                .FontColor(r.Respuesta == "NO" ? Colors.Red.Darken2 : Colors.Green.Darken2);
                        }
                    });

                    if (p.Estado == "Cerrado")
                    {
                        col.Item().PaddingTop(6).Text("Cierre del trabajo").Bold();
                        col.Item().Text($"Cerrado: {p.CierreHoraServidor:dd/MM/yyyy HH:mm}").FontSize(8);
                        if (!string.IsNullOrWhiteSpace(p.CierreObservaciones))
                            col.Item().Text($"Observaciones: {p.CierreObservaciones}").FontSize(8);
                    }

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Estado: {p.Estado}").FontSize(8).Bold();
                            if (!string.IsNullOrWhiteSpace(p.PdfHash))
                                c.Item().Text($"Hash del documento: {p.PdfHash[..Math.Min(16, p.PdfHash.Length)]}…").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                        if (selfieBytes is { Length: > 0 })
                            row.ConstantItem(80).Height(80).Image(selfieBytes).FitArea();
                        if (qrBytes is { Length: > 0 })
                            row.ConstantItem(70).Height(70).Image(qrBytes).FitArea();
                    });

                    col.Item().PaddingTop(14).Text("Firmas").Bold();
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => FirmaBloque(c, "Ejecutante", p.WorkerNombre, null, p.HoraServidorFirma, firmaBytes));
                        if (p.RequiereCapataz)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Capataz / Maestro de Obra", p.CapatazNombre, p.CapatazCargo, p.CapatazHoraServidor, firmaCapatazBytes));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Supervisor (Residente / Producción)", p.SupervisorNombre, p.SupervisorCargo, p.SupervisorHoraServidor, firmaSupervisorBytes));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Visto Bueno SSOMA", p.SsomaNombre, p.SsomaCargo, p.SsomaHoraServidor, firmaSsomaBytes));
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

    /// <summary>Logo | título centrado | Código/Versión/Fecha + Elab./Rev./Apro. — mismo patrón
    /// estándar corporativo que AtsPdfService/ConvalidacionPdfService/RacPdfService.</summary>
    private static void ComposeHeader(IContainer container, byte[]? logoBytes, string? codigo, string? tipoNombre, string? numeroDocumento, string? atsCodigo)
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
                tCol.Item().AlignCenter().Text("PERMISO ESCRITO DE TRABAJO DE ALTO RIESGO (PETAR)").Bold().FontSize(11).AlignCenter();
                if (!string.IsNullOrWhiteSpace(tipoNombre))
                    tCol.Item().AlignCenter().Text(tipoNombre).FontSize(8).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(numeroDocumento))
                    tCol.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(atsCodigo) ? $"N.° {numeroDocumento}" : $"N.° {numeroDocumento}  ·  ATS vinculado: {atsCodigo}").Bold().FontSize(9);
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
                MetaRow("Código:", string.IsNullOrWhiteSpace(codigo) ? "SSO-FO-039" : codigo);
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

    private static void FirmaBloque(IContainer container, string rol, string? nombre, string? cargo, DateTime? hora, byte[]? firma)
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

    private static string FechaHoraPeru(DateTime? horaServidorUtc)
    {
        if (horaServidorUtc is null) return "—";
        var utc = DateTime.SpecifyKind(horaServidorUtc.Value, DateTimeKind.Utc);
        var peru = new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(-5));
        return peru.ToString("dd/MM/yyyy HH:mm");
    }
}
