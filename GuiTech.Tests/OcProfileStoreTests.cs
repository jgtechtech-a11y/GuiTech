using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Regras dos perfis de overclock salvos (nome, substituição e resumo), sem abrir a janela.</summary>
public class OcProfileStoreTests
{
    private static OcProfile P(string name, int core, int mem, int power)
        => new() { Name = name, Core = core, Mem = mem, Power = power };

    [Fact]
    public void Find_ignora_caixa_e_espacos_nas_pontas()
    {
        var list = new List<OcProfile> { P("Jogo", 1820, 1828, 16) };

        Assert.NotNull(OcProfileStore.Find(list, "  jogo "));
        Assert.Equal("Jogo", OcProfileStore.Find(list, "JOGO")!.Name);
        Assert.Null(OcProfileStore.Find(list, "Silencioso"));
        Assert.Null(OcProfileStore.Find(list, "   "));
        Assert.Null(OcProfileStore.Find(list, null));
    }

    [Fact]
    public void Save_acrescenta_perfil_novo()
    {
        var list = new List<OcProfile>();

        OcProfileStore.Save(list, P("Diário", 1780, 1750, 0));

        Assert.Single(list);
        Assert.Equal("Diário", list[0].Name);
    }

    [Fact]
    public void Save_substitui_mesmo_nome_na_mesma_posicao()
    {
        var list = new List<OcProfile> { P("A", 1700, 1750, 0), P("Jogo", 1800, 1800, 5) };

        OcProfileStore.Save(list, P("jogo", 1820, 1828, 16));

        Assert.Equal(2, list.Count);
        Assert.Equal("A", list[0].Name);
        Assert.Equal(1820, list[1].Core);
        Assert.Equal("jogo", list[1].Name);          // mantém a grafia de quem salvou por último
        Assert.Equal(16, list[1].Power);
    }

    [Fact]
    public void UniqueName_comeca_pela_base_e_desvia_do_repetido()
    {
        var list = new List<OcProfile>();

        Assert.Equal("Perfil", OcProfileStore.UniqueName(list, "Perfil"));
        Assert.Equal("Perfil", OcProfileStore.UniqueName(list, "   "));

        list.Add(P("Perfil", 1780, 1750, 0));
        Assert.Equal("Perfil 2", OcProfileStore.UniqueName(list, "Perfil"));

        list.Add(P("Perfil 2", 1780, 1750, 0));
        Assert.Equal("Perfil 3", OcProfileStore.UniqueName(list, "Perfil"));
    }

    [Fact]
    public void Describe_mostra_os_tres_valores_com_sinal_na_energia()
    {
        Assert.Equal("GPU 1820 MHz · VRAM 1828 MHz · energia +16 %",
            OcProfileStore.Describe(P("Jogo", 1820, 1828, 16)));
        Assert.Equal("GPU 1780 MHz · VRAM 1750 MHz · energia 0 %",
            OcProfileStore.Describe(P("Padrão", 1780, 1750, 0)));
        Assert.Equal("GPU 1700 MHz · VRAM 1750 MHz · energia -50 %",
            OcProfileStore.Describe(P("Frio", 1700, 1750, -50)));
    }

    [Theory]
    [InlineData(1820, 1828, 16, true)]
    [InlineData(1819, 1828, 16, false)]
    [InlineData(1820, 1827, 16, false)]
    [InlineData(1820, 1828, 15, false)]
    public void Matches_compara_os_tres_valores(int core, int mem, int power, bool esperado)
        => Assert.Equal(esperado, OcProfileStore.Matches(P("Jogo", 1820, 1828, 16), core, mem, power));
}
