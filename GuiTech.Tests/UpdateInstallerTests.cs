using System.IO;
using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>
/// Regras puras da atualização em um clique: nome do Setup publicado, endereço na release,
/// leitura do SHA256SUMS.txt e detecção de instalação (Setup) x versão portátil.
/// </summary>
public class UpdateInstallerTests
{
    [Theory]
    [InlineData("0.25.16", "v0.25.16")]
    [InlineData("v0.25.16", "v0.25.16")]
    [InlineData("V0.25.16", "v0.25.16")]
    [InlineData(" 0.25.16 ", "v0.25.16")]
    public void Tag_sempre_comeca_com_v(string entrada, string esperado)
    {
        Assert.Equal(esperado, UpdateInstaller.Tag(entrada));
    }

    [Theory]
    [InlineData("v0.25.16", "GuiTech-Setup-0.25.16.exe")]
    [InlineData("0.25.16", "GuiTech-Setup-0.25.16.exe")]
    [InlineData("V1.0.0", "GuiTech-Setup-1.0.0.exe")]
    public void Nome_do_asset_segue_o_padrao_do_ci(string versao, string esperado)
    {
        Assert.Equal(esperado, UpdateInstaller.AssetName(versao));
    }

    [Fact]
    public void Url_aponta_para_o_download_da_tag()
    {
        string url = UpdateInstaller.AssetUrl("0.25.16", "GuiTech-Setup-0.25.16.exe");
        Assert.Equal(
            $"https://github.com/{UpdateChecker.Repo}/releases/download/v0.25.16/GuiTech-Setup-0.25.16.exe",
            url);
    }

    [Fact]
    public void Hash_no_formato_sha256sum_e_encontrado()
    {
        const string hash = "4DC0A060CD01FCCBE6AF19DE45F86102E5EB0D355FA2F971DE20A8AA2C5CE88C";
        string sums = $"{hash}  GuiTech.exe\n{hash}  GuiTech-Setup-0.25.16.exe\n";
        Assert.Equal(hash, UpdateInstaller.FindHash(sums, "GuiTech-Setup-0.25.16.exe"));
    }

    [Fact]
    public void Hash_no_formato_binario_estrela_e_encontrado()
    {
        const string hash = "FEFE1F71C8EFF5677FB828AAA4E192FD854007D58C2C2ED63D391362ED60956F";
        string sums = $"{hash} *GuiTech-Setup-0.25.16.exe\n";
        Assert.Equal(hash, UpdateInstaller.FindHash(sums, "GuiTech-Setup-0.25.16.exe"));
    }

    [Fact]
    public void Nome_do_arquivo_nao_diferencia_maiusculas()
    {
        const string hash = "8FF696811001DA1653CECEEBA5DCAFF7BC3EEC23A84D22F7A396D022D72BCFD4";
        string sums = $"{hash}  guitech-setup-0.25.16.EXE\n";
        Assert.Equal(hash, UpdateInstaller.FindHash(sums, "GuiTech-Setup-0.25.16.exe"));
    }

    [Fact]
    public void Arquivo_ausente_na_lista_devolve_nulo()
    {
        string sums = "AAAA  GuiTech.exe\nBBBB  GuiTech.zip\n";
        Assert.Null(UpdateInstaller.FindHash(sums, "GuiTech-Setup-0.25.16.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo sem hash")]
    public void Lista_vazia_ou_invalida_devolve_nulo(string? sums)
    {
        Assert.Null(UpdateInstaller.FindHash(sums, "GuiTech-Setup-0.25.16.exe"));
    }

    [Fact]
    public void Pasta_com_desinstalador_e_tratada_como_instalada()
    {
        string pasta = Path.Combine(Path.GetTempPath(), "GuiTechTeste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            Assert.False(UpdateInstaller.IsInstalled(pasta));
            File.WriteAllText(Path.Combine(pasta, "unins000.exe"), "");
            Assert.True(UpdateInstaller.IsInstalled(pasta));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void Limpeza_apaga_instaladores_antigos_e_preserva_o_novo()
    {
        string pasta = Path.Combine(Path.GetTempPath(), "GuiTechUpdate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            File.WriteAllText(Path.Combine(pasta, "GuiTech-Setup-0.25.15.exe"), "");
            File.WriteAllText(Path.Combine(pasta, "GuiTech-Setup-0.25.16.exe"), "");
            File.WriteAllText(Path.Combine(pasta, "anotacoes.txt"), "");

            UpdateInstaller.CleanupOldSetups(pasta, "GuiTech-Setup-0.25.16.exe");

            string?[] restantes = Directory.GetFiles(pasta).Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(new string?[] { "GuiTech-Setup-0.25.16.exe", "anotacoes.txt" }, restantes);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void Limpeza_em_pasta_inexistente_nao_lanca()
    {
        UpdateInstaller.CleanupOldSetups(Path.Combine(Path.GetTempPath(), "nao-existe-" + Guid.NewGuid().ToString("N")), "x.exe");
    }

    /// <summary>
    /// Teste de ponta a ponta contra a release publicada (não roda no CI): baixa o Setup de
    /// verdade e confere o SHA-256 contra o SHA256SUMS.txt. Para rodar:
    ///   $env:GUITECH_TEST_VERSION="0.25.17"; dotnet test --filter Download_real
    /// Sem a variável, o teste não faz nada (o CI não depende da rede nem da release).
    /// </summary>
    [Fact]
    public async Task Download_real_confere_o_sha256_publicado()
    {
        string? versao = Environment.GetEnvironmentVariable("GUITECH_TEST_VERSION");
        if (string.IsNullOrWhiteSpace(versao)) return;

        string arquivo = await UpdateInstaller.DownloadSetupAsync(versao);

        Assert.True(File.Exists(arquivo));
        Assert.Equal(UpdateInstaller.AssetName(versao), Path.GetFileName(arquivo));
        Assert.True(new FileInfo(arquivo).Length > 40_000_000, "o arquivo baixado parece incompleto");
    }
}
