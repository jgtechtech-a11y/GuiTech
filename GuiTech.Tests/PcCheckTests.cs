using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Selos da Verificação do PC: mapeamento nível → texto visível no app.</summary>
public class PcCheckTests
{
    [Theory]
    [InlineData(PcCheckLevel.Fixed, "CORRIGIDO")]
    [InlineData(PcCheckLevel.Ok, "OK")]
    [InlineData(PcCheckLevel.Suggestion, "PODE OTIMIZAR")]
    [InlineData(PcCheckLevel.Warning, "ATENÇÃO")]
    public void Cada_nivel_tem_o_seu_selo(PcCheckLevel level, string esperado)
    {
        var item = new PcCheckItem { Title = "Teste", Level = level };
        Assert.Equal(esperado, item.Badge);
    }

    [Fact]
    public void Contagens_do_relatorio_batem_com_os_itens()
    {
        var report = new PcCheckReport();
        report.Items.Add(new PcCheckItem { Title = "A", Level = PcCheckLevel.Fixed });
        report.Items.Add(new PcCheckItem { Title = "B", Level = PcCheckLevel.Fixed });
        report.Items.Add(new PcCheckItem { Title = "C", Level = PcCheckLevel.Warning });
        report.Items.Add(new PcCheckItem { Title = "D", Level = PcCheckLevel.Suggestion });
        report.Items.Add(new PcCheckItem { Title = "E", Level = PcCheckLevel.Ok });

        Assert.Equal(2, report.FixedCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(1, report.SuggestionCount);
        Assert.Equal(5, report.Items.Count);
    }
}
