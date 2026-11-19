using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Formatação de tamanhos (usada nos cartões de disco e nas métricas).</summary>
public class DriveCardTests
{
    [Fact]
    public void Zero_e_bytes_baixo_nao_dividem()
    {
        Assert.Equal("0 B", DriveCard.Fmt(0));
        Assert.Equal("1023 B", DriveCard.Fmt(1023));
    }

    [Fact]
    public void Multiplos_de_1024_sobem_de_unidade()
    {
        Assert.Equal("1 KB", DriveCard.Fmt(1024));
        Assert.Equal("1 MB", DriveCard.Fmt(1024L * 1024));
        Assert.Equal("5 GB", DriveCard.Fmt(5L * 1024 * 1024 * 1024));
        Assert.Equal("2 TB", DriveCard.Fmt(2L * 1024 * 1024 * 1024 * 1024));
    }

    [Fact]
    public void Fracoes_usam_a_cultura_atual()
    {
        // 1536 bytes = 1,5 KB (pt-BR usa vírgula; o teste acompanha a cultura da máquina)
        Assert.Equal((1.5).ToString("0.#") + " KB", DriveCard.Fmt(1536));
    }
}
