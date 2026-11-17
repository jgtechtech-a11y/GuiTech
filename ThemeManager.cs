using System.Windows;
using System.Windows.Media;

namespace GuiTech;

/// <summary>
/// Troca entre modo escuro e claro substituindo os pincéis da paleta (Theme.xaml). O XAML os referencia com DynamicResource,
/// então a troca vale na hora, sem recarregar janelas. Overlay e aviso de dispositivo ficam sempre escuros (aparecem sobre jogos).
/// </summary>
public static class ThemeManager
{
    public static bool IsLight { get; private set; }

    // Neutros com leve tom de teal (em vez de roxo) + acento teal dessaturado (#4FB3A9).
    // No tema claro o acento escurece para manter contraste sobre branco.
    private static readonly (object Key, string Dark, string Light)[] Palette =
    {
        ("Bg", "#070B0A", "#FAFBFB"),
        ("Panel", "#0B100E", "#F1F5F4"),
        ("Card", "#0E1311", "#FFFFFF"),
        ("Hover", "#161B19", "#E7EEEC"),
        ("Active", "#1F2422", "#DCEBE8"),
        ("Line", "#1C211F", "#D7E3E0"),
        ("LineStrong", "#2D3331", "#93A8A4"),
        ("Fg", "#E0E5E3", "#101715"),
        ("Muted", "#A0A6A3", "#44544F"),
        ("Faint", "#7E8683", "#65756F"),
        ("Ink", "#070B0A", "#FFFFFF"),
        ("ThumbBrush", "#242A28", "#D7E3E0"),
        ("ThumbHover", "#545A58", "#A2B8B3"),
        ("ThumbDrag", "#6EC8BF", "#2E8B80"),
        ("Accent", "#4FB3A9", "#128577"),
        ("AccentHover", "#6EC8BF", "#0B5F57"),
        ("AccentSoft", "#16332F", "#DDF0ED"),
        ("AccentInk", "#06231E", "#FFFFFF"),
        ("Good", "#4AA16D", "#15803D"),
        ("Warn", "#CA9C48", "#B45309"),
        (SystemColors.ControlBrushKey, "#070B0A", "#FAFBFB"),
        (SystemColors.HighlightBrushKey, "#16332F", "#DDF0ED"),
        (SystemColors.InactiveSelectionHighlightBrushKey, "#16332F", "#DDF0ED"),
    };

    public static void Apply(bool light)
    {
        IsLight = light;
        var res = Application.Current.Resources;
        foreach (var (key, dark, lightHex) in Palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(light ? lightHex : dark);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            res[key] = brush; // o XAML usa DynamicResource: trocar o pincel atualiza todos os controles na hora
        }
    }
}
