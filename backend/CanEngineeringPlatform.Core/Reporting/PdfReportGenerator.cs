using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace CanEngineeringPlatform.Core.Reporting;

/// <summary>
/// Professional vector PDF report generator for automotive engineering and diagnostics.
/// Uses PdfSharpCore to render high-DPI vector documents with tables, charts, KPI metrics, and headers.
/// </summary>
public class PdfReportGenerator
{
    public byte[] GeneratePdf(ReportDefinition report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));

        var document = new PdfDocument();
        document.Info.Title = report.Metadata.Title;
        document.Info.Author = report.Metadata.TechnicianName;
        document.Info.Subject = $"Automotive Report - VIN: {report.Metadata.VehicleVin}";
        document.Info.Creator = "CAN Engineering Platform";

        using (var pageContext = new ReportRenderContext(document, report))
        {
            pageContext.StartNewPage();

            // Render sections in order
            var sortedSections = report.Sections.OrderBy(s => s.Order).ToList();
            foreach (var section in sortedSections)
            {
                RenderSection(pageContext, section);
            }
        }

        // Second pass: Draw running headers and footers with accurate total page count
        int totalPages = document.PageCount;
        for (int i = 0; i < totalPages; i++)
        {
            var page = document.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page);
            DrawPageHeaderAndFooter(gfx, report, i + 1, totalPages);
        }

        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }

    private void RenderSection(ReportRenderContext ctx, ReportSection section)
    {
        // Check if section requires a heading
        if (!string.IsNullOrWhiteSpace(section.Title))
        {
            ctx.EnsureSpace(30);
            DrawSectionHeader(ctx.Gfx, ref ctx.CurrentY, section.Title);
        }

        switch (section.Type)
        {
            case ReportSectionType.VehicleSummary:
            case ReportSectionType.KeyValueList:
                if (section.KeyValues != null)
                {
                    DrawKeyValueGrid(ctx, section.KeyValues);
                }
                break;

            case ReportSectionType.KeyMetrics:
                if (section.Metrics != null && section.Metrics.Count > 0)
                {
                    DrawKpiGrid(ctx, section.Metrics);
                }
                break;

            case ReportSectionType.Table:
                if (section.Table != null && section.Table.Headers.Count > 0)
                {
                    DrawTable(ctx, section.Table);
                }
                break;

            case ReportSectionType.Chart:
                if (section.Chart != null && section.Chart.Series.Count > 0)
                {
                    DrawVectorChart(ctx, section.Chart);
                }
                break;

            case ReportSectionType.VisualMap:
                if (section.Heatmap != null)
                {
                    DrawHeatmap(ctx, section.Heatmap);
                }
                break;

            case ReportSectionType.AlertNotice:
                if (!string.IsNullOrWhiteSpace(section.AlertMessage))
                {
                    DrawAlertBox(ctx, section.Title, section.AlertMessage, section.AlertSeverity ?? "Info");
                }
                break;

            case ReportSectionType.Notes:
                if (!string.IsNullOrWhiteSpace(section.NotesContent))
                {
                    DrawNotesBox(ctx, section.NotesContent);
                }
                break;
        }

        ctx.CurrentY += 12; // Spacing after section
    }

    private static void DrawSectionHeader(XGraphics gfx, ref double y, string title)
    {
        gfx.DrawString(title.ToUpperInvariant(), ReportTemplate.FontSectionHeading, new XSolidBrush(ReportTemplate.PrimaryBlue), ReportTemplate.MarginLeft, y);
        y += 4;
        gfx.DrawLine(new XPen(ReportTemplate.PrimaryBlue, 1.2), ReportTemplate.MarginLeft, y, ReportTemplate.MarginLeft + ReportTemplate.ContentWidth, y);
        y += 10;
    }

    private static void DrawKeyValueGrid(ReportRenderContext ctx, Dictionary<string, string> kvs)
    {
        int cols = 2;
        double colWidth = ReportTemplate.ContentWidth / cols;
        double rowHeight = 18;
        var entries = kvs.ToList();

        for (int i = 0; i < entries.Count; i += cols)
        {
            ctx.EnsureSpace(rowHeight + 4);
            double y = ctx.CurrentY;

            for (int c = 0; c < cols && (i + c) < entries.Count; c++)
            {
                var kv = entries[i + c];
                double x = ReportTemplate.MarginLeft + (c * colWidth);

                // Label
                ctx.Gfx.DrawString(kv.Key + ":", ReportTemplate.FontBodyBold, new XSolidBrush(ReportTemplate.TextSecondary), x, y + 12);
                // Value
                double labelWidth = ctx.Gfx.MeasureString(kv.Key + ":", ReportTemplate.FontBodyBold).Width;
                ctx.Gfx.DrawString(kv.Value, ReportTemplate.FontBody, new XSolidBrush(ReportTemplate.TextPrimary), x + labelWidth + 6, y + 12);
            }

            ctx.CurrentY += rowHeight;
        }
    }

    private static void DrawKpiGrid(ReportRenderContext ctx, List<ReportMetricItem> metrics)
    {
        int count = metrics.Count;
        int cols = Math.Min(4, Math.Max(2, count));
        double cardWidth = (ReportTemplate.ContentWidth - ((cols - 1) * 8)) / cols;
        double cardHeight = 44;

        ctx.EnsureSpace(cardHeight + 6);
        double startX = ReportTemplate.MarginLeft;
        double y = ctx.CurrentY;

        for (int i = 0; i < count; i++)
        {
            int colIdx = i % cols;
            if (i > 0 && colIdx == 0)
            {
                y += cardHeight + 8;
                ctx.EnsureSpace(cardHeight + 6);
            }

            double x = startX + (colIdx * (cardWidth + 8));
            var m = metrics[i];

            // Card background
            ctx.Gfx.DrawRoundedRectangle(new XPen(ReportTemplate.BorderLight, 0.8), new XSolidBrush(ReportTemplate.BackgroundCard), x, y, cardWidth, cardHeight, 4, 4);

            // Left status accent bar
            XColor accentColor = m.Status.ToLowerInvariant() switch
            {
                "danger" => ReportTemplate.StatusDanger,
                "warning" => ReportTemplate.StatusWarning,
                "success" => ReportTemplate.StatusSuccess,
                _ => ReportTemplate.PrimaryBlue
            };
            ctx.Gfx.DrawRectangle(new XSolidBrush(accentColor), x, y, 3.5, cardHeight);

            // Label
            ctx.Gfx.DrawString(m.Label.ToUpperInvariant(), ReportTemplate.FontKpiLabel, new XSolidBrush(ReportTemplate.TextSecondary), x + 8, y + 13);

            // Value + Unit
            string valStr = m.Value + (!string.IsNullOrWhiteSpace(m.Unit) ? " " + m.Unit : "");
            ctx.Gfx.DrawString(valStr, ReportTemplate.FontKpiValue, new XSolidBrush(ReportTemplate.TextPrimary), x + 8, y + 32);
        }

        int totalRows = (int)Math.Ceiling((double)count / cols);
        ctx.CurrentY = y + cardHeight + 4;
    }

    private static void DrawTable(ReportRenderContext ctx, ReportTableData table)
    {
        int colCount = table.Headers.Count;
        if (colCount == 0) return;

        double[] colWidths = new double[colCount];
        if (table.ColumnWidthRatios != null && table.ColumnWidthRatios.Count == colCount)
        {
            double sum = table.ColumnWidthRatios.Sum();
            for (int i = 0; i < colCount; i++)
            {
                colWidths[i] = (table.ColumnWidthRatios[i] / sum) * ReportTemplate.ContentWidth;
            }
        }
        else
        {
            double uniform = ReportTemplate.ContentWidth / colCount;
            for (int i = 0; i < colCount; i++) colWidths[i] = uniform;
        }

        double rowHeight = 18;
        double headerHeight = 20;

        // Draw Table Header
        ctx.EnsureSpace(headerHeight + rowHeight);
        DrawTableHeaderRow(ctx.Gfx, ctx.CurrentY, table.Headers, colWidths);
        ctx.CurrentY += headerHeight;

        // Draw Rows
        bool alternate = false;
        foreach (var row in table.Rows)
        {
            ctx.EnsureSpace(rowHeight + 4);

            // If we just wrapped to a new page, redraw table header
            if (ctx.IsNewPage)
            {
                DrawTableHeaderRow(ctx.Gfx, ctx.CurrentY, table.Headers, colWidths);
                ctx.CurrentY += headerHeight;
                ctx.IsNewPage = false;
            }

            double y = ctx.CurrentY;
            if (alternate)
            {
                ctx.Gfx.DrawRectangle(new XSolidBrush(ReportTemplate.BackgroundLight), ReportTemplate.MarginLeft, y, ReportTemplate.ContentWidth, rowHeight);
            }

            double x = ReportTemplate.MarginLeft;
            for (int c = 0; c < colCount; c++)
            {
                string cellText = c < row.Count ? row[c] : "";
                ctx.Gfx.DrawString(cellText, ReportTemplate.FontBody, new XSolidBrush(ReportTemplate.TextPrimary), x + 4, y + 13);
                x += colWidths[c];
            }

            // Bottom row border
            ctx.Gfx.DrawLine(new XPen(ReportTemplate.BorderLight, 0.5), ReportTemplate.MarginLeft, y + rowHeight, ReportTemplate.MarginLeft + ReportTemplate.ContentWidth, y + rowHeight);

            ctx.CurrentY += rowHeight;
            alternate = !alternate;
        }
    }

    private static void DrawTableHeaderRow(XGraphics gfx, double y, List<string> headers, double[] colWidths)
    {
        double headerHeight = 20;
        gfx.DrawRectangle(new XSolidBrush(ReportTemplate.PrimaryDark), ReportTemplate.MarginLeft, y, ReportTemplate.ContentWidth, headerHeight);

        double x = ReportTemplate.MarginLeft;
        for (int i = 0; i < headers.Count; i++)
        {
            gfx.DrawString(headers[i].ToUpperInvariant(), ReportTemplate.FontBodyBold, XBrushes.White, x + 4, y + 14);
            x += colWidths[i];
        }
    }

    private static void DrawVectorChart(ReportRenderContext ctx, ReportChartData chart)
    {
        double chartWidth = ReportTemplate.ContentWidth;
        double chartHeight = 160;

        ctx.EnsureSpace(chartHeight + 20);
        double startX = ReportTemplate.MarginLeft;
        double startY = ctx.CurrentY;

        // Chart container background
        ctx.Gfx.DrawRoundedRectangle(new XPen(ReportTemplate.BorderStrong, 0.8), new XSolidBrush(ReportTemplate.BackgroundLight), startX, startY, chartWidth, chartHeight, 4, 4);

        // Chart plot area with padding
        double plotX = startX + 45;
        double plotY = startY + 25;
        double plotW = chartWidth - 65;
        double plotH = chartHeight - 50;

        // Determine bounds
        double minX = chart.MinX ?? chart.Series.SelectMany(s => s.Points).Select(p => p.X).DefaultIfEmpty(0).Min();
        double maxX = chart.MaxX ?? chart.Series.SelectMany(s => s.Points).Select(p => p.X).DefaultIfEmpty(100).Max();
        double minY = chart.MinY ?? 0;
        double maxY = chart.MaxY ?? chart.Series.SelectMany(s => s.Points).Select(p => p.Y).DefaultIfEmpty(100).Max();

        if (Math.Abs(maxX - minX) < 0.001) maxX = minX + 1.0;
        if (Math.Abs(maxY - minY) < 0.001) maxY = minY + 1.0;

        // Title
        if (!string.IsNullOrWhiteSpace(chart.Title))
        {
            ctx.Gfx.DrawString(chart.Title, ReportTemplate.FontBodyBold, new XSolidBrush(ReportTemplate.TextPrimary), startX + 10, startY + 16);
        }

        // Horizontal grid lines and Y-axis ticks
        int yTicks = 4;
        for (int i = 0; i <= yTicks; i++)
        {
            double frac = (double)i / yTicks;
            double tickY = plotY + plotH - (frac * plotH);
            double valY = minY + frac * (maxY - minY);

            ctx.Gfx.DrawLine(new XPen(ReportTemplate.BorderLight, 0.5), plotX, tickY, plotX + plotW, tickY);
            string yLabel = Math.Round(valY, 0).ToString();
            ctx.Gfx.DrawString(yLabel, ReportTemplate.FontCaption, new XSolidBrush(ReportTemplate.TextSecondary), plotX - 25, tickY + 3);
        }

        // Draw Series Curves
        foreach (var s in chart.Series)
        {
            if (s.Points.Count < 2) continue;

            var penColor = ReportTemplate.ParseHexColor(s.ColorHex, ReportTemplate.PrimaryBlue);
            var pen = new XPen(penColor, s.LineWidth);
            if (s.IsDashed)
            {
                pen.DashStyle = XDashStyle.Dash;
            }

            for (int p = 1; p < s.Points.Count; p++)
            {
                var pt0 = s.Points[p - 1];
                var pt1 = s.Points[p];

                double x0 = plotX + ((pt0.X - minX) / (maxX - minX)) * plotW;
                double y0 = plotY + plotH - ((pt0.Y - minY) / (maxY - minY)) * plotH;
                double x1 = plotX + ((pt1.X - minX) / (maxX - minX)) * plotW;
                double y1 = plotY + plotH - ((pt1.Y - minY) / (maxY - minY)) * plotH;

                ctx.Gfx.DrawLine(pen, x0, y0, x1, y1);
            }
        }

        // Axis labels
        if (!string.IsNullOrWhiteSpace(chart.XAxisLabel))
        {
            ctx.Gfx.DrawString(chart.XAxisLabel, ReportTemplate.FontCaption, new XSolidBrush(ReportTemplate.TextSecondary), plotX + (plotW / 2) - 20, startY + chartHeight - 6);
        }

        // Legend in top-right
        double legendX = startX + chartWidth - 160;
        double legendY = startY + 12;
        foreach (var s in chart.Series)
        {
            var penColor = ReportTemplate.ParseHexColor(s.ColorHex, ReportTemplate.PrimaryBlue);
            ctx.Gfx.DrawLine(new XPen(penColor, 2.0), legendX, legendY, legendX + 14, legendY);
            ctx.Gfx.DrawString(s.Name, ReportTemplate.FontCaption, new XSolidBrush(ReportTemplate.TextPrimary), legendX + 18, legendY + 3);
            legendX += ctx.Gfx.MeasureString(s.Name, ReportTemplate.FontCaption).Width + 26;
        }

        ctx.CurrentY += chartHeight;
    }

    private static void DrawHeatmap(ReportRenderContext ctx, ReportHeatmapData map)
    {
        int cols = map.XBreakpoints.Length;
        int rows = map.YBreakpoints.Length;
        if (cols == 0 || rows == 0) return;

        double mapH = 20 + (rows * 16);
        ctx.EnsureSpace(mapH + 20);

        double startX = ReportTemplate.MarginLeft;
        double y = ctx.CurrentY;

        ctx.Gfx.DrawString($"{map.Title} ({map.ZUnit})", ReportTemplate.FontBodyBold, new XSolidBrush(ReportTemplate.TextPrimary), startX, y);
        y += 14;

        double labelW = 45;
        double cellW = (ReportTemplate.ContentWidth - labelW) / cols;
        double cellH = 15;

        // X Breakpoints Header
        for (int c = 0; c < cols; c++)
        {
            double cx = startX + labelW + (c * cellW);
            ctx.Gfx.DrawRectangle(new XSolidBrush(ReportTemplate.PrimaryDark), cx, y, cellW, cellH);
            ctx.Gfx.DrawString(map.XBreakpoints[c].ToString("0"), ReportTemplate.FontCaption, XBrushes.White, cx + 2, y + 10);
        }
        y += cellH;

        // Find min and max for normalization
        double minZ = double.MaxValue, maxZ = double.MinValue;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols && c < map.ZValues[r].Length; c++)
            {
                double v = map.ZValues[r][c];
                if (v < minZ) minZ = v;
                if (v > maxZ) maxZ = v;
            }
        }
        if (minZ >= maxZ) { minZ = 0; maxZ = 1; }

        for (int r = 0; r < rows; r++)
        {
            // Y breakpoint label
            ctx.Gfx.DrawRectangle(new XSolidBrush(ReportTemplate.BackgroundCard), startX, y, labelW, cellH);
            ctx.Gfx.DrawString(map.YBreakpoints[r].ToString("0"), ReportTemplate.FontCaption, new XSolidBrush(ReportTemplate.TextPrimary), startX + 2, y + 10);

            for (int c = 0; c < cols && c < map.ZValues[r].Length; c++)
            {
                double val = map.ZValues[r][c];
                double norm = (val - minZ) / (maxZ - minZ);

                // Gradient from light sky blue to deep amber/rose
                int red = (int)(14 + norm * (225 - 14));
                int green = (int)(165 - norm * 60);
                int blue = (int)(233 - norm * 150);
                var cellColor = XColor.FromArgb(Math.Clamp(red, 0, 255), Math.Clamp(green, 0, 255), Math.Clamp(blue, 0, 255));

                double cx = startX + labelW + (c * cellW);
                ctx.Gfx.DrawRectangle(new XPen(XColors.White, 0.4), new XSolidBrush(cellColor), cx, y, cellW, cellH);

                var textColor = norm > 0.6 ? XColors.White : XColors.Black;
                ctx.Gfx.DrawString(val.ToString("0.0"), ReportTemplate.FontCaption, new XSolidBrush(textColor), cx + 2, y + 10);
            }

            y += cellH;
        }

        ctx.CurrentY = y + 4;
    }

    private static void DrawAlertBox(ReportRenderContext ctx, string title, string message, string severity)
    {
        double boxH = 46;
        ctx.EnsureSpace(boxH + 10);

        XColor accentColor = severity.ToLowerInvariant() switch
        {
            "danger" => ReportTemplate.StatusDanger,
            "warning" => ReportTemplate.StatusWarning,
            "success" => ReportTemplate.StatusSuccess,
            _ => ReportTemplate.PrimaryBlue
        };

        XColor bgColor = severity.ToLowerInvariant() switch
        {
            "danger" => XColor.FromArgb(254, 242, 242),   // Light rose
            "warning" => XColor.FromArgb(254, 243, 199),  // Light amber
            "success" => XColor.FromArgb(236, 253, 245),  // Light emerald
            _ => XColor.FromArgb(240, 249, 255)           // Light sky
        };

        double y = ctx.CurrentY;
        ctx.Gfx.DrawRoundedRectangle(new XPen(accentColor, 1.0), new XSolidBrush(bgColor), ReportTemplate.MarginLeft, y, ReportTemplate.ContentWidth, boxH, 4, 4);

        ctx.Gfx.DrawString(title.ToUpperInvariant(), ReportTemplate.FontBodyBold, new XSolidBrush(accentColor), ReportTemplate.MarginLeft + 12, y + 16);
        ctx.Gfx.DrawString(message, ReportTemplate.FontBody, new XSolidBrush(ReportTemplate.TextPrimary), ReportTemplate.MarginLeft + 12, y + 32);

        ctx.CurrentY += boxH + 6;
    }

    private static void DrawNotesBox(ReportRenderContext ctx, string notes)
    {
        double boxH = 50;
        ctx.EnsureSpace(boxH + 10);

        double y = ctx.CurrentY;
        ctx.Gfx.DrawRoundedRectangle(new XPen(ReportTemplate.BorderStrong, 0.8), new XSolidBrush(ReportTemplate.BackgroundLight), ReportTemplate.MarginLeft, y, ReportTemplate.ContentWidth, boxH, 4, 4);

        ctx.Gfx.DrawString("TECHNICIAN / CALIBRATOR NOTES & EVIDENCE:", ReportTemplate.FontKpiLabel, new XSolidBrush(ReportTemplate.TextSecondary), ReportTemplate.MarginLeft + 10, y + 14);
        ctx.Gfx.DrawString(notes, ReportTemplate.FontBody, new XSolidBrush(ReportTemplate.TextPrimary), ReportTemplate.MarginLeft + 10, y + 30);

        ctx.CurrentY += boxH + 6;
    }

    private static void DrawPageHeaderAndFooter(XGraphics gfx, ReportDefinition report, int pageNumber, int totalPages)
    {
        // 1. Running Top Header
        // Deep slate banner at top
        gfx.DrawRectangle(new XSolidBrush(ReportTemplate.PrimaryDark), 0, 0, ReportTemplate.PageWidth, 28);
        gfx.DrawString("CAN ENGINEERING PLATFORM", ReportTemplate.FontKpiLabel, XBrushes.White, ReportTemplate.MarginLeft, 18);
        gfx.DrawString(report.Metadata.Title.ToUpperInvariant(), ReportTemplate.FontKpiLabel, new XSolidBrush(ReportTemplate.AccentCyan), ReportTemplate.MarginLeft + 160, 18);
        gfx.DrawString($"VIN: {report.Metadata.VehicleVin}", ReportTemplate.FontKpiLabel, XBrushes.LightGray, ReportTemplate.PageWidth - ReportTemplate.MarginRight - 120, 18);

        // 2. Running Bottom Footer
        double footerY = ReportTemplate.PageHeight - 24;
        gfx.DrawLine(new XPen(ReportTemplate.BorderLight, 0.8), ReportTemplate.MarginLeft, footerY - 6, ReportTemplate.PageWidth - ReportTemplate.MarginRight, footerY - 6);

        string genDate = report.Metadata.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm UTC");
        gfx.DrawString($"Generated: {genDate} | Operator: {report.Metadata.TechnicianName} | STRICT READ-ONLY AUDIT", ReportTemplate.FontCaption, new XSolidBrush(ReportTemplate.TextMuted), ReportTemplate.MarginLeft, footerY + 6);
        gfx.DrawString($"Page {pageNumber} of {totalPages}", ReportTemplate.FontCaption, new XSolidBrush(ReportTemplate.TextPrimary), ReportTemplate.PageWidth - ReportTemplate.MarginRight - 55, footerY + 6);
    }
}

/// <summary>
/// Helper tracking active PDF page, graphics context, and current vertical Y coordinate.
/// </summary>
public class ReportRenderContext : IDisposable
{
    private readonly PdfDocument _doc;
    public ReportDefinition Report { get; }
    public PdfPage CurrentPage { get; private set; } = null!;
    public XGraphics Gfx { get; private set; } = null!;
    public double CurrentY;
    public bool IsNewPage { get; set; }

    public ReportRenderContext(PdfDocument doc, ReportDefinition report)
    {
        _doc = doc;
        Report = report;
    }

    public void StartNewPage()
    {
        Gfx?.Dispose();
        CurrentPage = _doc.AddPage();
        CurrentPage.Width = ReportTemplate.PageWidth;
        CurrentPage.Height = ReportTemplate.PageHeight;
        Gfx = XGraphics.FromPdfPage(CurrentPage);
        CurrentY = ReportTemplate.MarginTop + 14; // Allow room under header
        IsNewPage = true;
    }

    public void EnsureSpace(double requiredHeight)
    {
        if (CurrentY + requiredHeight > ReportTemplate.PageHeight - ReportTemplate.MarginBottom)
        {
            StartNewPage();
        }
    }

    public void Dispose()
    {
        Gfx?.Dispose();
    }
}
