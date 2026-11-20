using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GuiTech;

public sealed record BenchRun(DateTime Date, double? Multi, double? Single, bool Sustained, string Cpu);

/// <summary>Resultado dos benchmarks de jogo (Test = "gpu", "sim" ou "mem"). Score é usado para a barra do histórico.</summary>
public sealed record BenchRecord(DateTime Date, string Test, double Score, string Summary);

/// <summary>Programa extra que abre antes do jogo (ex.: ferramenta de comando/monitoramento) e pode fechar junto com ele.</summary>
public sealed class HelperProgram
{
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public bool CloseWithGame { get; set; } = true;
    public string Display => System.IO.Path.GetFileName(Path) + (string.IsNullOrWhiteSpace(Args) ? "" : " " + Args);
}

/// <summary>Perfil de um jogo na aba Gui_Tech JOGOS: desempenho, argumentos de gráficos e programas auxiliares.</summary>
public sealed class GameProfile
{
    public string Priority { get; set; } = "normal";   // normal | above | high
    /// <summary>Máscara de afinidade de CPU (bit i = núcleo lógico i). 0 = todos os núcleos.</summary>
    public long AffinityMask { get; set; }
    /// <summary>Prioridade das threads do jogo: normal | above | highest (relativa ao processo).</summary>
    public string ThreadPriority { get; set; } = "normal";
    public bool HighPerfPlan { get; set; }
    public bool GpuHigh { get; set; }
    public bool DisableFso { get; set; }
    public string? ExePath { get; set; }
    public string WinMode { get; set; } = "default";   // default | full | borderless | window
    public string Res { get; set; } = "default";       // default | 1280x720 | 1920x1080 | 2560x1440 | 3840x2160
    public int Hz { get; set; }                        // 0 = não definir
    public string Style { get; set; } = "source";      // source | unity | unreal
    public string Extra { get; set; } = "";
    public List<HelperProgram> Helpers { get; set; } = new();
}

/// <summary>Jogo adicionado à mão (qualquer .exe).</summary>
public sealed class CustomGame
{
    public string Name { get; set; } = "";
    public string Exe { get; set; } = "";
    public string Args { get; set; } = "";
}

/// <summary>Perfil de iluminação salvo (efeito, cor, brilho e velocidade).</summary>
public sealed class LightProfile
{
    public string Name { get; set; } = "";
    /// <summary>static | breath | rainbow | cpu | gpu | load | off</summary>
    public string Fx { get; set; } = "static";
    public byte R { get; set; } = 255;
    public byte G { get; set; } = 255;
    public byte B { get; set; } = 255;
    /// <summary>Brilho 0–100.</summary>
    public int Bright { get; set; } = 100;
    /// <summary>Velocidade 1–10 (respiração e arco-íris).</summary>
    public int Speed { get; set; } = 5;
    /// <summary>Controle exclusivo (iCUE).</summary>
    public bool Exclusive { get; set; } = true;
}

/// <summary>
/// Perfil de overclock da GPU salvo na aba Gui_Tech OC: teto de clock, clock da memória e
/// limite de energia (%). Serve para voltar a uma configuração conhecida sem mexer nos sliders.
/// </summary>
public sealed class OcProfile
{
    public string Name { get; set; } = "";
    public int Core { get; set; }
    public int Mem { get; set; }
    public int Power { get; set; }
}

/// <summary>Configurações em %AppData%\GuiTech. A chave da API é cifrada com DPAPI (só este usuário do Windows a lê).</summary>
public sealed class AppSettings
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GuiTech");
    private static string FilePath => Path.Combine(Folder, "settings.json");

    public string? HowManyFpsKeyProtected { get; set; }

    // Gui_Tech BENCH: últimos resultados (mais recente primeiro)
    public List<BenchRun> BenchRuns { get; set; } = new();
    public List<BenchRecord> BenchRecords { get; set; } = new();

    // Gui_Tech OTIMIZA: valores originais dos ajustes do Windows antes da primeira alteração ("<ausente>" = não existia)
    public Dictionary<string, string?> TweakBackup { get; set; } = new();
    /// <summary>ComDB original (reservas de números COM) antes da primeira "liberação".</summary>
    public byte[]? ComDbBackup { get; set; }
    /// <summary>Programas de inicialização removidos, para "Restaurar removidos".</summary>
    public List<StartupBackup> StartupRemoved { get; set; } = new();

    // Gui_Tech JOGOS
    public Dictionary<string, GameProfile> GameProfiles { get; set; } = new();
    public List<CustomGame> CustomGames { get; set; } = new();
    /// <summary>Perfis de iluminação salvos (Gui_Tech INTEGRAÇÃO).</summary>
    public List<LightProfile> LightProfiles { get; set; } = new();

    // Aparência
    public bool LightTheme { get; set; }

    /// <summary>Iniciar junto com o Windows, minimizado na bandeja (espelhado na chave Run do Windows).</summary>
    public bool StartWithWindows { get; set; }

    // Revisão semanal (tarefa agendada "GuiTechRevisao")
    public bool WeeklyReview { get; set; }
    /// <summary>Quando a última revisão semanal rodou (automática ou pedida pelo usuário).</summary>
    public DateTime? WeeklyReviewLast { get; set; }

    /// <summary>Avisos automáticos de hardware (temperatura, disco, bateria).</summary>
    public bool HardwareAlerts { get; set; } = true;

    // Overlay
    public string OverlayCorner { get; set; } = "TL";
    public double OverlayScale { get; set; } = 1.0;
    /// <summary>Itens escolhidos para o overlay (chaves de OverlayMetrics); null = padrão.</summary>
    public List<string>? OverlayItems { get; set; }

    // Ventoinha da GPU
    /// <summary>Temperatura máxima desejada para a GPU (°C). 0 = ventoinha no automático do driver.</summary>
    public int GpuFanTarget { get; set; }

    // Overclock da GPU (Gui_Tech OC)
    /// <summary>Perfis de clock/memória/energia salvos pelo usuário.</summary>
    public List<OcProfile> OcProfiles { get; set; } = new();
    /// <summary>Perfil aplicado automaticamente quando o Gui_Tech abre (null = nenhum).</summary>
    public string? OcProfileOnStart { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] // nunca gravar a chave em texto puro
    public string? HowManyFpsKey
    {
        get
        {
            if (string.IsNullOrEmpty(HowManyFpsKeyProtected)) return null;
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(HowManyFpsKeyProtected), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return null; }
        }
        set
        {
            HowManyFpsKeyProtected = string.IsNullOrWhiteSpace(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser));
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
