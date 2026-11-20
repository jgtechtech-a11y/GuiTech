namespace GuiTech;

/// <summary>
/// Contas dos ajustes de clock e energia da GPU (Overdrive8). Ficam separadas da chamada ao
/// driver para poderem ser testadas sem hardware.
/// </summary>
public static class Od8Tuning
{
    /// <summary>
    /// Prende o valor na faixa que o driver informa. Se a faixa vier invertida (driver
    /// estranho), usa o mínimo em vez de estourar.
    /// </summary>
    public static int Fit(int value, int min, int max)
        => max < min ? min : Math.Clamp(value, min, max);

    /// <summary>Texto do valor para a tela: "1820 MHz", "+16 %", "0 %", "-50 %".</summary>
    public static string Describe(int value, string unit, bool signed = false)
        => (signed && value > 0 ? "+" : "") + value + (unit.Length > 0 ? " " + unit : "");
}
