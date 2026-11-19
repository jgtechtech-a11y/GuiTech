using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GuiTech;

/// <summary>
/// Atualização em um clique: baixa o Setup publicado na release, confere o SHA-256 contra o
/// SHA256SUMS.txt da mesma release e devolve o caminho do arquivo para executar.
/// Toda a decisão (nome do arquivo, leitura da lista de hashes) fica em funções puras,
/// para poder ser testada sem rede.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>Arquivo de conferência publicado junto com a release.</summary>
    public const string SumsAsset = "SHA256SUMS.txt";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
        DefaultRequestHeaders = { { "User-Agent", "GuiTech-UpdateChecker" } },
    };

    /// <summary>"0.25.16" ou "v0.25.16" → "v0.25.16".</summary>
    public static string Tag(string version) =>
        "v" + (version ?? "").Trim().TrimStart('v', 'V');

    /// <summary>"v0.25.16" → "GuiTech-Setup-0.25.16.exe" (o nome que o CI publica).</summary>
    public static string AssetName(string version) =>
        "GuiTech-Setup-" + (version ?? "").Trim().TrimStart('v', 'V') + ".exe";

    /// <summary>
    /// Procura o hash de um arquivo no texto do SHA256SUMS.txt. Aceita as linhas no formato
    /// "HASH  arquivo" (sha256sum) e "HASH *arquivo" (sha256sum -b).
    /// </summary>
    public static string? FindHash(string? sums, string fileName)
    {
        foreach (string raw in (sums ?? "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length < 66) continue;   // 64 do hash + separador + algo
            int sep = line.IndexOfAny(new[] { ' ', '\t' });
            if (sep <= 0) continue;
            string hash = line[..sep];
            string name = line[sep..].Trim().TrimStart('*');
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) return hash.Trim().ToUpperInvariant();
        }
        return null;
    }

    /// <summary>URL do arquivo dentro da release.</summary>
    public static string AssetUrl(string version, string asset) =>
        $"https://github.com/{UpdateChecker.Repo}/releases/download/{Tag(version)}/{asset}";

    /// <summary>
    /// True quando o programa está rodando de uma instalação feita pelo Setup (a pasta tem o
    /// desinstalador do Inno). Versão portátil/arquivo único devolve false: baixar o Setup
    /// instalaria uma cópia nova em Program Files, então lá o botão abre a release no navegador.
    /// </summary>
    public static bool IsInstalled(string? folder = null) =>
        File.Exists(Path.Combine(folder ?? AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>
    /// Baixa o Setup da versão indicada, confere o SHA-256 e devolve o caminho local.
    /// Lança <see cref="InvalidOperationException"/> quando a release não tem o instalador
    /// (versões antigas) e <see cref="InvalidDataException"/> quando o arquivo não confere.
    /// </summary>
    public static async Task<string> DownloadSetupAsync(
        string version, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string asset = AssetName(version);

        string sums;
        try
        {
            sums = await Http.GetStringAsync(AssetUrl(version, SumsAsset), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                "Não consegui baixar a lista de conferência (SHA256SUMS.txt) desta versão.", ex);
        }

        string expected = FindHash(sums, asset)
            ?? throw new InvalidOperationException(
                $"A release {Tag(version)} não publicou o instalador {asset}.");

        string folder = Path.Combine(Path.GetTempPath(), "GuiTech-update");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, asset);

        using (var resp = await Http.GetAsync(AssetUrl(version, asset), HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"O download falhou (HTTP {(int)resp.StatusCode}).");

            long total = resp.Content.Headers.ContentLength ?? 0;
            await using var input = await resp.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(target);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
            }
        }

        string actual = await HashFileAsync(target, ct);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(target); } catch { /* fica para a limpeza do %TEMP% */ }
            throw new InvalidDataException(
                "O arquivo baixado não confere com o SHA-256 publicado. Tente de novo ou use o navegador.");
        }

        progress?.Report(1);
        return target;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, ct));
    }
}
