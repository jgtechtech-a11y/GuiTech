using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Contas dos ajustes de clock/energia (Overdrive8), sem tocar na GPU.</summary>
public class Od8TuningTests
{
    [Theory]
    [InlineData(1820, 800, 1820, 1820)]   // no topo da faixa
    [InlineData(900, 800, 1820, 900)]     // dentro da faixa
    [InlineData(400, 800, 1820, 800)]     // abaixo do mínimo
    [InlineData(5000, 800, 1820, 1820)]   // acima do máximo
    [InlineData(16, -50, 20, 16)]         // limite de energia negativo/positivo
    [InlineData(-80, -50, 20, -50)]
    public void Fit_prende_o_valor_na_faixa(int valor, int min, int max, int esperado)
        => Assert.Equal(esperado, Od8Tuning.Fit(valor, min, max));

    [Fact]
    public void Fit_com_faixa_invertida_usa_o_minimo()
        => Assert.Equal(500, Od8Tuning.Fit(900, 500, 400));

    [Theory]
    [InlineData(1820, "MHz", false, "1820 MHz")]
    [InlineData(16, "%", true, "+16 %")]
    [InlineData(0, "%", true, "0 %")]
    [InlineData(-50, "%", true, "-50 %")]
    [InlineData(-50, "%", false, "-50 %")]
    public void Describe_formata_com_sinal_quando_pedido(int valor, string unidade, bool comSinal, string esperado)
        => Assert.Equal(esperado, Od8Tuning.Describe(valor, unidade, comSinal));
}
