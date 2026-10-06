using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Services;

/// <summary>Un equipo dentro del PDF: su imagen guía (con dimensiones, para ubicar los círculos), sus respuestas y las fotos de evidencia.</summary>
public record InspPdfEquipo(
    string Titulo,
    byte[]? Imagen,
    int ImagenAncho,
    int ImagenAlto,
    List<InspRespuestaDto> Respuestas,
    Dictionary<int, byte[]> Evidencias);

public record InspPdfFirma(string? Nombre, string? Cargo, DateTime? Hora, byte[]? Firma);

/// <summary>Un día del registro semanal: lo que se inspeccionó (null = ese día no hay check list) y las firmas de ese día.</summary>
public record InspPdfDia(DateOnly Fecha, string? Resultado, string? Operador, byte[]? FirmaOperador, byte[]? FirmaResidente, byte[]? FirmaSsoma);

/// <summary>PDF del check list de inspección de equipos (SSO-FO-049 arnés, SSO-FO-056 minicargador…). Mismo encabezado corporativo que
/// AtsPdfService/PetarPdfService: logo | título | Código-Versión-Fecha + Elaboración/Revisión/Aprobación. Un documento por persona/inspección,
/// con una sección por equipo (imagen guía con los números ubicados + criterios) y la cadena de firmas.</summary>
public static class InspeccionEquipoPdfService
{
    private const float AnchoImagen = 190f;
    private const float RadioPin = 7f;
    private const string SvgPin = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'><circle cx='10' cy='10' r='9' fill='white' stroke='black' stroke-width='1.8'/></svg>";
    private const string SvgPinMal = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'><circle cx='10' cy='10' r='9' fill='#fee2e2' stroke='#b91c1c' stroke-width='1.8'/></svg>";

    public static byte[] Generar(
        InspInspeccionDetalleDto d,
        List<InspPdfEquipo> equipos,
        List<(string Nombre, bool Marcado)> equiposDelFormato,
        byte[]? selfie,
        InspPdfFirma operador,
        InspPdfFirma? capataz,
        InspPdfFirma? residente,
        InspPdfFirma? ssoma,
        byte[]? logoBytes,
        string verificacionUrl,
        string hashCorto)
    {
        byte[] qrBytes;
        using (var generator = new QRCodeGenerator())
        using (var qr = generator.CreateQrCode(verificacionUrl, QRCodeGenerator.ECCLevel.M))
            qrBytes = new PngByteQRCode(qr).GetGraphic(8);

        var tipo = d.Tipo!;
        var siNo = tipo.ModoRespuesta == "SI_NO_NA";
        var conCorreccion = tipo.RegistraCorreccion;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Element(c => ComposeHeader(c, logoBytes, tipo));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    // ── Datos generales (mismos campos de la cabecera del formato físico) ──
                    col.Item().PaddingTop(8).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t => { t.Span("Fecha: ").Bold(); t.Span(d.Fecha.ToString("dd/MM/yyyy")); });
                            r.RelativeItem(2).Text(t => { t.Span("Proyecto: ").Bold(); t.Span(d.ProyectoNombre ?? "—"); });
                            if (tipo.RequiereTurno)
                                r.RelativeItem().Text(t => { t.Span("Turno: ").Bold(); t.Span(d.Turno == "noche" ? "Noche" : "Día"); });
                        });
                        c.Item().Text(t => { t.Span("Inspeccionado por: ").Bold(); t.Span(d.OperadorNombre); });
                        if (d.Planificada.HasValue || d.EmpresaTipo != null)
                        {
                            c.Item().Row(r =>
                            {
                                if (d.Planificada.HasValue)
                                    r.RelativeItem().Row(x => { x.AutoItem().Text("Planificada: ").Bold(); x.AutoItem().Element(e => Casilla(e, d.Planificada == true)); x.ConstantItem(14); x.AutoItem().Text("No planificada: ").Bold(); x.AutoItem().Element(e => Casilla(e, d.Planificada == false)); });
                                if (d.EmpresaTipo != null)
                                    r.RelativeItem().Row(x => { x.AutoItem().Text("Casa: ").Bold(); x.AutoItem().Element(e => Casilla(e, d.EmpresaTipo == "casa")); x.ConstantItem(14); x.AutoItem().Text("Contratista: ").Bold(); x.AutoItem().Element(e => Casilla(e, d.EmpresaTipo == "contratista")); });
                            });
                            if (!string.IsNullOrWhiteSpace(d.EmpresaNombre))
                                c.Item().Text(t => { t.Span("Empresa: ").Bold(); t.Span(d.EmpresaNombre!); });
                        }
                        if (equiposDelFormato.Count > 0)
                        {
                            c.Item().PaddingTop(2).Text("Equipo de protección a usar el día de hoy:").Bold();
                            c.Item().Row(r =>
                            {
                                foreach (var (nombre, marcado) in equiposDelFormato)
                                {
                                    r.AutoItem().PaddingRight(10).Row(x => { x.AutoItem().Text(nombre + " ").FontSize(8); x.AutoItem().Element(e => Casilla(e, marcado)); });
                                }
                            });
                        }
                        if (!string.IsNullOrWhiteSpace(tipo.Instrucciones))
                            c.Item().PaddingTop(2).Text(tipo.Instrucciones).FontSize(7).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"Resultado: {Resultado(d.Resultado)}   ·   N.° {d.Codigo}").FontSize(8).Bold()
                            .FontColor(d.Resultado == "conforme" ? Colors.Green.Darken2 : d.Resultado == "con_observaciones" ? Colors.Orange.Darken2 : Colors.Red.Darken2);
                    });

                    // ── Una sección por equipo ──
                    foreach (var eq in equipos)
                    {
                        var seccion = col.Item();
                        if (eq.Imagen != null) seccion = seccion.ShowEntire();
                        seccion.Column(c =>
                        {
                            c.Spacing(4);
                            c.Item().Background(Colors.Grey.Lighten3).Padding(4).Text(eq.Titulo).Bold().FontSize(9);
                            c.Item().Row(r =>
                            {
                                if (eq.Imagen != null)
                                {
                                    r.ConstantItem(AnchoImagen + 8).PaddingRight(8).Element(e => GuiaImagen(e, eq));
                                }
                                r.RelativeItem().Element(e => TablaCriterios(e, eq, siNo, conCorreccion));
                            });

                            var conFoto = eq.Respuestas.Where(x => eq.Evidencias.ContainsKey(x.Id)).ToList();
                            if (conFoto.Count > 0)
                            {
                                c.Item().PaddingTop(2).Text("Evidencias fotográficas").Bold().FontSize(8);
                                c.Item().Row(r =>
                                {
                                    foreach (var x in conFoto.Take(5))
                                    {
                                        r.ConstantItem(88).PaddingRight(4).Column(f =>
                                        {
                                            f.Item().Height(64).Image(eq.Evidencias[x.Id]).FitArea();
                                            f.Item().Text($"Ítem {x.Numero}").FontSize(7).FontColor(Colors.Grey.Darken1);
                                        });
                                    }
                                });
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.Observaciones))
                        col.Item().Text(t => { t.Span("Observaciones generales: ").Bold(); t.Span(d.Observaciones!); });

                    // ── Firmas de la cadena ──
                    col.Item().PaddingTop(10).Text("Firmas").Bold();
                    col.Item().ShowEntire().Row(row =>
                    {
                        row.RelativeItem().Element(c => FirmaBloque(c, "Operador / inspeccionado por", operador, selfie));
                        if (capataz != null)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Capataz / Maestro de obra", capataz, null));
                        if (residente != null)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Ingeniero de producción o residente", residente, null));
                        if (ssoma != null)
                            row.RelativeItem().Element(c => FirmaBloque(c, "Responsable SSOMA o prevencionista de riesgos", ssoma, null));
                    });

                    col.Item().PaddingTop(8).ShowEntire().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Documento: {d.Codigo}").FontSize(8).Bold();
                            c.Item().Text($"Código de verificación: {hashCorto}").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text("Escanea el QR para verificar la autenticidad: " + verificacionUrl).FontSize(6.5f).FontColor(Colors.Blue.Darken2);
                        });
                        row.ConstantItem(64).Height(64).Image(qrBytes).FitArea();
                    });

                    if (d.Estado == "Anulado")
                        col.Item().Text($"ANULADO — {d.AnuladoMotivo}").Bold().FontColor(Colors.Red.Darken2);
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text($"{tipo.CodigoFormato} · Versión {tipo.Version ?? "—"} · {d.Codigo}").FontSize(7).FontColor(Colors.Grey.Darken1);
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Página ").FontSize(7); t.CurrentPageNumber().FontSize(7);
                        t.Span(" de ").FontSize(7); t.TotalPages().FontSize(7);
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>"Registro de seguimiento e inspección diaria": una página con los 6 días (Lunes–Sábado) de UN equipo, cada uno con su
    /// resultado y las firmas del Operador, del Ingeniero Residente y del Prevencionista — la tabla que el formato físico trae al pie.</summary>
    public static byte[] GenerarRegistroSemanal(InspTipoDto tipo, InspEquipoDto equipo, DateOnly lunes, int semana, List<InspPdfDia> dias, byte[]? logoBytes)
    {
        var nombres = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Element(c => ComposeHeader(c, logoBytes, tipo, $"REGISTRO DE SEGUIMIENTO E INSPECCIÓN DIARIA — {tipo.Nombre.ToUpperInvariant()}"));

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().PaddingTop(8).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().Row(r =>
                        {
                            r.RelativeItem(2).Text(t => { t.Span("Equipo: ").Bold(); t.Span($"{equipo.VarianteNombre ?? equipo.TipoNombre} {equipo.Codigo}"); });
                            r.RelativeItem().Text(t => { t.Span("N° semana calendario: ").Bold(); t.Span(semana.ToString()); });
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem(2).Text(t => { t.Span("Proyecto: ").Bold(); t.Span(equipo.ProyectoNombre ?? "—"); });
                            r.RelativeItem().Text(t => { t.Span("Semana: ").Bold(); t.Span($"{lunes:dd/MM/yyyy} al {lunes.AddDays(5):dd/MM/yyyy}"); });
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(92); for (var i = 0; i < 6; i++) c.RelativeColumn(); });

                        IContainer Cab(IContainer x) => x.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignCenter().AlignMiddle();
                        IContainer Celda(IContainer x, float alto) => x.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Height(alto).AlignCenter().AlignMiddle();

                        table.Cell().Element(Cab).Text("Día").Bold().FontSize(8);
                        foreach (var n in nombres) table.Cell().Element(Cab).Text(n).Bold().FontSize(8);

                        table.Cell().Element(x => Celda(x, 24)).Text("Fecha").Bold().FontSize(8);
                        foreach (var d in dias) table.Cell().Element(x => Celda(x, 24)).Text(d.Fecha.ToString("dd/MM")).FontSize(8);

                        table.Cell().Element(x => Celda(x, 30)).Text("Resultado").Bold().FontSize(8);
                        foreach (var d in dias)
                            table.Cell().Element(x => Celda(x, 30)).Text(d.Resultado is null ? "—" : Resultado(d.Resultado).Split(" — ")[0]).FontSize(6.5f).Bold()
                                .FontColor(d.Resultado is null ? Colors.Grey.Medium : d.Resultado == "conforme" ? Colors.Green.Darken2 : d.Resultado == "con_observaciones" ? Colors.Orange.Darken2 : Colors.Red.Darken2);

                        table.Cell().Element(x => Celda(x, 28)).Text("Operador").Bold().FontSize(8);
                        foreach (var d in dias) table.Cell().Element(x => Celda(x, 28)).Text(d.Operador ?? "").FontSize(6.5f);

                        void FilaFirmas(string titulo, Func<InspPdfDia, byte[]?> firma)
                        {
                            table.Cell().Element(x => Celda(x, 46)).Text(titulo).Bold().FontSize(8);
                            foreach (var d in dias)
                                table.Cell().Element(x => Celda(x, 46)).Element(inner =>
                                {
                                    var f = firma(d);
                                    if (f is { Length: > 0 }) inner.Image(f).FitArea();
                                    else inner.Text(" ");
                                });
                        }
                        FilaFirmas("Firma operador", d => d.FirmaOperador);
                        FilaFirmas("Firma Ing. residente", d => d.FirmaResidente);
                        FilaFirmas("Firma prevencionista", d => d.FirmaSsoma);
                    });

                    col.Item().Text("Un día sin datos significa que no hubo check list de este equipo ese día.").FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().Text($"{tipo.CodigoFormato} · Registro semanal · {equipo.Codigo} · generado {DateTime.UtcNow.AddHours(-5):dd/MM/yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    // ── Imagen con los números ubicados encima ───────────────────────────

    private static void GuiaImagen(IContainer container, InspPdfEquipo eq)
    {
        var alto = eq.ImagenAncho > 0 ? AnchoImagen * eq.ImagenAlto / eq.ImagenAncho : AnchoImagen;
        container.AlignTop().Width(AnchoImagen).Height(alto).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Layers(layers =>
        {
            layers.PrimaryLayer().Image(eq.Imagen!).FitArea();
            var flechas = SvgFlechas(eq, AnchoImagen, alto);
            if (flechas != null) layers.Layer().Svg(flechas);
            foreach (var r in eq.Respuestas)
            {
                foreach (var m in r.Marcadores)
                {
                    var x = (float)m.X / 100f * AnchoImagen - RadioPin;
                    var y = (float)m.Y / 100f * alto - RadioPin;
                    var mal = r.Respuesta == "no_cumple";
                    layers.Layer().TranslateX(x).TranslateY(y).Width(RadioPin * 2).Height(RadioPin * 2).Layers(pin =>
                    {
                        pin.PrimaryLayer().Svg(mal ? SvgPinMal : SvgPin);
                        pin.Layer().AlignCenter().AlignMiddle().Text(r.Numero.ToString()).FontSize(7).Bold();
                    });
                }
            }
        });
    }

    /// <summary>Flechas de los marcadores que la llevan: del círculo a la punta, con su cabeza. El SVG usa las mismas medidas (puntos) que el contenedor
    /// de la imagen, así que no se deforma. Rojo en los ítems que no cumplen.</summary>
    private static string? SvgFlechas(InspPdfEquipo eq, float ancho, float alto)
    {
        var sb = new System.Text.StringBuilder();
        var hay = false;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var r in eq.Respuestas)
        {
            var color = r.Respuesta == "no_cumple" ? "#b91c1c" : "#14100b";
            foreach (var m in r.Marcadores.Where(x => x.Px.HasValue && x.Py.HasValue))
            {
                var x1 = (double)m.X / 100 * ancho; var y1 = (double)m.Y / 100 * alto;
                var x2 = (double)m.Px!.Value / 100 * ancho; var y2 = (double)m.Py!.Value / 100 * alto;
                var dx = x2 - x1; var dy = y2 - y1;
                var largo = Math.Sqrt(dx * dx + dy * dy);
                if (largo < 1) continue;
                var ux = dx / largo; var uy = dy / largo;
                // La línea nace en el borde del círculo y termina en la base de la cabeza.
                var ini = Math.Min(RadioPin, largo);
                var cabeza = Math.Min(5.0, Math.Max(0, largo - ini));
                var sx = x1 + ux * ini; var sy = y1 + uy * ini;
                var bx = x2 - ux * cabeza; var by = y2 - uy * cabeza;
                var nx = -uy; var ny = ux; // perpendicular
                sb.Append(string.Format(inv, "<line x1='{0:0.##}' y1='{1:0.##}' x2='{2:0.##}' y2='{3:0.##}' stroke='{4}' stroke-width='1.1'/>", sx, sy, bx, by, color));
                sb.Append(string.Format(inv, "<polygon points='{0:0.##},{1:0.##} {2:0.##},{3:0.##} {4:0.##},{5:0.##}' fill='{6}'/>",
                    x2, y2, bx + nx * 2.2, by + ny * 2.2, bx - nx * 2.2, by - ny * 2.2, color));
                hay = true;
            }
        }
        return hay ? string.Format(inv, "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {0:0.##} {1:0.##}' width='{0:0.##}' height='{1:0.##}'>{2}</svg>", ancho, alto, sb) : null;
    }

    // ── Tabla de criterios ───────────────────────────────────────────────

    private static void TablaCriterios(IContainer container, InspPdfEquipo eq, bool siNo, bool conCorreccion)
    {
        var conNr = eq.Respuestas.Any(r => !string.IsNullOrWhiteSpace(r.NivelRiesgo));
        var opciones = siNo ? new[] { "SI", "NO", "NA" } : new[] { "C", "NC", "NA" };

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(20);
                if (conNr) c.ConstantColumn(28);
                c.RelativeColumn(4);
                c.ConstantColumn(22); c.ConstantColumn(22); c.ConstantColumn(22);
                if (conCorreccion) { c.RelativeColumn(2); c.ConstantColumn(20); c.ConstantColumn(20); }
            });

            IContainer Cab(IContainer x) => x.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignCenter().AlignMiddle();
            IContainer Celda(IContainer x) => x.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignMiddle();

            table.Header(h =>
            {
                h.Cell().Element(Cab).Text("N°").Bold().FontSize(7);
                if (conNr) h.Cell().Element(Cab).Text("NR").Bold().FontSize(7);
                h.Cell().Element(Cab).Text("CRITERIOS OPERACIONALES").Bold().FontSize(7);
                foreach (var o in opciones) h.Cell().Element(Cab).Text(o).Bold().FontSize(7);
                if (conCorreccion)
                {
                    h.Cell().Element(Cab).Text("OBSERVACIÓN").Bold().FontSize(7);
                    h.Cell().Element(Cab).Text("SE CORRIGIÓ SI").Bold().FontSize(5.5f);
                    h.Cell().Element(Cab).Text("NO").Bold().FontSize(7);
                }
            });

            foreach (var r in eq.Respuestas)
            {
                var mal = r.Respuesta == "no_cumple";
                table.Cell().Element(Celda).AlignCenter().Text(r.Numero.ToString()).FontSize(8).Bold();
                if (conNr)
                    table.Cell().Element(Celda).AlignCenter().Text(r.NivelRiesgo ?? "").FontSize(6.5f).Bold()
                        .FontColor(r.NivelRiesgo == "STOP" ? Colors.Red.Darken2 : Colors.Black);
                table.Cell().Element(Celda).Column(c =>
                {
                    c.Item().Text(r.Descripcion).FontSize(8);
                    if (!conCorreccion && !string.IsNullOrWhiteSpace(r.Observacion))
                        c.Item().Text($"Obs.: {r.Observacion}").FontSize(7).Italic().FontColor(Colors.Red.Darken2);
                });

                // Lo que marcó la persona tal cual el formato (en preguntas negativas el SI es lo malo: se re-traduce).
                var marcada = r.Respuesta == "na" ? 2
                    : siNo ? (r.Respuesta == "cumple" ? (r.Invertido ? 1 : 0) : (r.Invertido ? 0 : 1))
                           : (r.Respuesta == "cumple" ? 0 : 1);
                for (var i = 0; i < 3; i++)
                {
                    var cell = table.Cell().Element(Celda).AlignCenter();
                    if (i == marcada)
                        cell.Text("X").Bold().FontSize(9).FontColor(mal ? Colors.Red.Darken2 : Colors.Black);
                    else
                        cell.Text(" ");
                }

                if (conCorreccion)
                {
                    table.Cell().Element(Celda).Text(r.Observacion ?? "").FontSize(7);
                    table.Cell().Element(Celda).AlignCenter().Text(r.SeCorrigio == true ? "X" : " ").Bold().FontSize(9);
                    table.Cell().Element(Celda).AlignCenter().Text(r.SeCorrigio == false ? "X" : " ").Bold().FontSize(9);
                }
            }
        });
    }

    private static void Casilla(IContainer container, bool marcada)
    {
        container.Width(9).Height(9).Border(0.7f).AlignCenter().AlignMiddle().Text(marcada ? "X" : " ").FontSize(7).Bold();
    }

    private static string Resultado(string r) => r switch
    {
        "conforme" => "CONFORME",
        "con_observaciones" => "CON OBSERVACIONES",
        _ => "NO CONFORME CRÍTICO — EQUIPO FUERA DE SERVICIO",
    };

    // ── Encabezado corporativo (mismo patrón que ATS/PETAR) ──────────────

    private static void ComposeHeader(IContainer container, byte[]? logoBytes, InspTipoDto tipo, string? titulo = null)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Row(row =>
        {
            row.ConstantItem(90).AlignMiddle().AlignCenter().Padding(4).Element(logoEl =>
            {
                if (logoBytes != null) logoEl.AlignMiddle().AlignCenter().Image(logoBytes).FitArea();
                else logoEl.AlignMiddle().AlignCenter().Text("ABRIL").Bold().FontSize(8).AlignCenter();
            });

            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

            row.RelativeItem().AlignMiddle().AlignCenter().Padding(4).Text(titulo ?? ("CHECK LIST DE " + tipo.Nombre.ToUpperInvariant())).Bold().FontSize(11).AlignCenter();

            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

            row.ConstantItem(120).Column(metaCol =>
            {
                void MetaRow(string label, string valor)
                {
                    metaCol.Item().BorderBottom(0.5f).Padding(2).Row(r =>
                    {
                        r.AutoItem().Text(label).Bold().FontSize(7);
                        r.ConstantItem(2);
                        r.RelativeItem().Text(valor).FontSize(7);
                    });
                }
                MetaRow("Código:", tipo.CodigoFormato);
                MetaRow("Versión:", tipo.Version ?? "—");
                MetaRow("Fecha:", tipo.FechaVersion?.ToString("dd/MM/yyyy") ?? "—");

                metaCol.Item().Row(subRow =>
                {
                    foreach (var (lbl, val, last) in new[]
                    {
                        ("Elab.:", tipo.Elaboracion ?? "SSOMA", false),
                        ("Rev.:", tipo.Revision ?? "JSSOMA", false),
                        ("Apro.:", tipo.Aprobacion ?? "GP", true),
                    })
                    {
                        var cell = subRow.RelativeItem().Padding(2);
                        if (!last) cell = cell.BorderRight(0.5f);
                        cell.Text(t => { t.Span(lbl + " ").Bold().FontSize(5.5f); t.Span(val).FontSize(5.5f); });
                    }
                });
            });
        });
    }

    private static void FirmaBloque(IContainer container, string rol, InspPdfFirma f, byte[]? selfie)
    {
        container.PaddingRight(6).Column(c =>
        {
            c.Item().Height(44).AlignCenter().Row(r =>
            {
                if (selfie is { Length: > 0 }) r.ConstantItem(40).Padding(2).Image(selfie).FitArea();
                r.RelativeItem().Element(inner =>
                {
                    if (f.Firma is { Length: > 0 }) inner.Image(f.Firma).FitArea();
                    else inner.AlignMiddle().AlignCenter().Text("PENDIENTE").FontSize(8).Bold().FontColor(Colors.Red.Darken2);
                });
            });
            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(2).Text(rol).FontSize(6.5f).Bold();
            c.Item().Text(f.Nombre ?? "—").FontSize(7);
            if (!string.IsNullOrWhiteSpace(f.Cargo)) c.Item().Text(f.Cargo).FontSize(6.5f).FontColor(Colors.Grey.Darken1).Italic();
            if (f.Hora.HasValue) c.Item().Text(FechaHoraPeru(f.Hora)).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string FechaHoraPeru(DateTime? horaServidorUtc)
    {
        if (horaServidorUtc is null) return "—";
        var utc = DateTime.SpecifyKind(horaServidorUtc.Value, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(-5)).ToString("dd/MM/yyyy HH:mm");
    }
}
