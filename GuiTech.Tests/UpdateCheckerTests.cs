using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Parsing de tags de release e comparação com a versão do executável.</summary>
public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v0.25.11", "0.25.11")]
    [InlineData("0.25.11", "0.25.11")]
    [InlineData("V0.25.11", "0.25.11")]
    [InlineData("0.25.11-beta1", "0.25.11")]
    [InlineData("v1.0.0", "1.0.0")]
    public void Tags_validas_sao_parseadas(string tag, string esperado)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var v));
        Assert.Equal(new Version(esperado), v);
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("")]
    [InlineData("abc")]
    public void Tags_invalidas_falham_sem_excecao(string tag)
    {
        Assert.False(UpdateChecker.TryParseVersion(tag, out var v));
        Assert.Equal(new Version(0, 0), v);
    }

    [Fact]
    public void Versao_atual_do_executavel_esta_definida()
    {
        Assert.True(UpdateChecker.Current > new Version(0, 0, 0));
    }
}
