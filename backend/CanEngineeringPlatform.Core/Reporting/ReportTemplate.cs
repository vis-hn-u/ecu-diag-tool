using System;
using PdfSharpCore.Drawing;

namespace CanEngineeringPlatform.Core.Reporting;

/// <summary>
/// Professional automotive styling configuration and design tokens for PDF reports.
/// </summary>
public static class ReportTemplate
{
    // Page Dimensions (A4: 595.28 x 841.89 points)
    public const double PageWidth = 595.28;
    public const double PageHeight = 841.89;
    public const double MarginLeft = 36.0;
    public const double MarginRight = 36.0;
    public const double MarginTop = 40.0;
    public const double MarginBottom = 40.0;
    public const double ContentWidth = PageWidth - MarginLeft - MarginRight; // 523.28 pt

    // Color Palette
    public static readonly XColor PrimaryDark = XColor.FromArgb(15, 23, 42);      // #0f172a (Deep Slate)
    public static readonly XColor PrimaryBlue = XColor.FromArgb(2, 132, 199);     // #0284c7 (Automotive Blue)
    public static readonly XColor AccentCyan = XColor.FromArgb(14, 165, 233);     // #0ea5e9
    public static readonly XColor TextPrimary = XColor.FromArgb(30, 41, 59);      // #1e293b
    public static readonly XColor TextSecondary = XColor.FromArgb(100, 116, 139); // #64748b
    public static readonly XColor TextMuted = XColor.FromArgb(148, 163, 184);     // #94a3b8
    
    public static readonly XColor BackgroundLight = XColor.FromArgb(248, 250, 252);// #f8fafc
    public static readonly XColor BackgroundCard = XColor.FromArgb(241, 245, 249); // #f1f5f9
    public static readonly XColor BorderLight = XColor.FromArgb(226, 232, 240);    // #e2e8f0
    public static readonly XColor BorderStrong = XColor.FromArgb(203, 213, 225);   // #cbd5e1

    // Status Colors
    public static readonly XColor StatusDanger = XColor.FromArgb(225, 29, 72);     // #e11d48 (Rose)
    public static readonly XColor StatusWarning = XColor.FromArgb(217, 119, 6);    // #d97706 (Amber)
    public static readonly XColor StatusSuccess = XColor.FromArgb(16, 185, 129);   // #10b981 (Emerald)
    public static readonly XColor StatusInfo = XColor.FromArgb(14, 165, 233);      // #0ea5e9 (Sky)

    // Standard Fonts
    public static readonly XFont FontTitle = new("Arial", 16, XFontStyle.Bold);
    public static readonly XFont FontSubtitle = new("Arial", 10, XFontStyle.Regular);
    public static readonly XFont FontSectionHeading = new("Arial", 11, XFontStyle.Bold);
    public static readonly XFont FontBody = new("Arial", 9, XFontStyle.Regular);
    public static readonly XFont FontBodyBold = new("Arial", 9, XFontStyle.Bold);
    public static readonly XFont FontCaption = new("Arial", 7.5, XFontStyle.Regular);
    public static readonly XFont FontMono = new("Courier New", 8, XFontStyle.Regular);
    public static readonly XFont FontMonoBold = new("Courier New", 8, XFontStyle.Bold);
    public static readonly XFont FontKpiValue = new("Arial", 14, XFontStyle.Bold);
    public static readonly XFont FontKpiLabel = new("Arial", 7.5, XFontStyle.Bold);

    public static XColor ParseHexColor(string? hex, XColor defaultColor)
    {
        if (string.IsNullOrWhiteSpace(hex)) return defaultColor;
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6 &&
            int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
            int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
            int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
        {
            return XColor.FromArgb(r, g, b);
        }
        return defaultColor;
    }
}
