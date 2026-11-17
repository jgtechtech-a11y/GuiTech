using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuiTech;

/// <summary>
/// Verifica se existe versão mais nova do Gui_Tech publicada nas Releases do GitHub.
/// A fonte é o repositório abaixo: quando o repositório próprio do fork estiver criado,
/// troque a constante e o verificador começa a funcionar sozinho (até lá, fica mudo).
/// Consulta no máximo uma vez por dia — o resultado fica cacheado em %AppData%\GuiTech\update.json.
/// </summary>
public static class UpdateChecker
{
    // >>> Repositório das versões (formato "usuario/repo"). Placeholder até o fork ficar pronto.
    public const string Repo = "jgtechtech-a11y/GuiTech";

    public sealed record Info(bool HasUpdate, string Version, string Url, string Notes);

    private sealed record Cache
    {
        [JsonPropertyName("checked")] public DateTime Checked { get; set; }
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    }

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "GuiTech-UpdateChecker" } }
    };

    /// <summary>Null = sem informação (repo não configurado, offline ou sem versão nova).</summary>
    public static async Task<Info?> CheckAsync()
    {
        try
        {
#if DEBUG
            // Teste manual: crie %AppData%\GuiTech\update-test.json com
            // {"version":"9.9.9","url":"https://github.com/...","notes":"texto"} e abra o app em Debug
            // para ver o banner sem precisar de uma release de verdade.
            var testPath = Path.Combine(AppSettings.Folder, "update-test.json");
            if (File.Exists(testPath))
            {
                var t = JsonDocument.Parse(File.ReadAllText(testPath)).RootElement;
                return ToInfo(
                    t.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "9.9.9",
                    t.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    t.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "");
            }
#endif

            if (Repo.Contains("SEU-USUARIO") || Repo.Split('/') is { Length: < 2 }) return null;

            var cachePath = Path.Combine(AppSettings.Folder, "update.json");
            if (File.Exists(cachePath))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<Cache>(File.ReadAllText(cachePath));
                    if (cached != null && DateTime.Now - cached.Checked < TimeSpan.FromHours(24))
                        return ToInfo(cached.Version, cached.Url, cached.Notes);
                }
                catch { }
            }

            var body = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            using var doc = JsonDocument.Parse(body);
            string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            string url = doc.RootElement.GetProperty("html_url").GetString() ?? "";
            string notes = doc.RootElement.GetProperty("body").GetString() ?? "";
            notes = notes.Length > 300 ? notes[..300] + "…" : notes;

            File.WriteAllText(cachePath, JsonSerializer.Serialize(new Cache
            {
                Checked = DateTime.Now, Version = tag, Url = url, Notes = notes
            }));

            return ToInfo(tag, url, notes);
        }
        catch
        {
            return null; // sem internet, repo vazio ou API indisponível: silêncio
        }
    }

    private static Info? ToInfo(string tag, string url, string notes)
    {
        if (!TryParseVersion(tag, out var latest) || latest <= Current) return new Info(false, tag, url, notes);
        return new Info(true, tag, url, notes);
    }

    /// <summary>Versão atual do executável.</summary>
    public static Version Current => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Aceita "v0.25.11", "0.25.11", "0.25.11.1"…</summary>
    internal static bool TryParseVersion(string text, out Version version)
    {
        string clean = (text ?? "").Trim().TrimStart('v', 'V');
        // sufixos tipo "-beta1" são descartados
        int dash = clean.IndexOf('-');
        if (dash >= 0) clean = clean[..dash];
        if (!Version.TryParse(clean, out var parsed))
        {
            version = new Version();
            return false;
        }
        version = parsed;
        return true;
    }
}
