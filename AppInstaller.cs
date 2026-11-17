using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32;

namespace GuiTech;

/// <summary>App do catálogo "Instalar apps" (pós-formatação), instalado pelo winget.</summary>
public sealed class InstallableApp : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string WingetId { get; init; } = "";
    /// <summary>Começo do nome em "Aplicativos instalados" (Painel de Controle), para saber se já está instalado.</summary>
    public string DisplayPrefix { get; init; } = "";
    public bool PreSelect { get; init; } = true;

    private bool _selected, _installed;
    private string _status = "";

    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public bool Installed
    {
        get => _installed;
        set { Set(ref _installed, value); OnChanged(nameof(CanSelect)); }
    }
    public bool CanSelect => !Installed;
    public string Status { get => _status; set => Set(ref _status, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(p);
    }
}

/// <summary>
/// Catálogo de apps essenciais e instalação silenciosa pelo winget (Gerenciador de Pacotes do Windows), que baixa
/// sempre a versão oficial mais recente do site do fabricante e confere o hash do instalador.
/// </summary>
public static class AppInstaller
{
    public static List<InstallableApp> Catalog() => new()
    {
        // ---- Jogos e lojas de jogos ----
        new() { Name = "Steam", Description = "Loja e biblioteca de jogos da Valve.", WingetId = "Valve.Steam", DisplayPrefix = "Steam" },
        new() { Name = "Epic Games", Description = "Loja da Epic, com jogos grátis semanais e Fortnite.", WingetId = "EpicGames.EpicGamesLauncher", DisplayPrefix = "Epic", PreSelect = false },
        new() { Name = "GOG Galaxy", Description = "Biblioteca dos jogos GOG (sem DRM) e de outras lojas juntas.", WingetId = "GOG.Galaxy", DisplayPrefix = "GOG", PreSelect = false },
        new() { Name = "Ubisoft Connect", Description = "Biblioteca e loja de jogos Ubisoft (Assassin's Creed, Far Cry…).", WingetId = "Ubisoft.Connect", DisplayPrefix = "Ubisoft", PreSelect = false },
        new() { Name = "Battle.net", Description = "Biblioteca e loja da Blizzard (Warcraft, Diablo, Overwatch).", WingetId = "Blizzard.BattleNet", DisplayPrefix = "Battle", PreSelect = false },
        new() { Name = "EA app", Description = "Biblioteca e loja da EA (FIFA/FC, Battlefield, The Sims).", WingetId = "ElectronicArts.EADesktop", DisplayPrefix = "EA", PreSelect = false },

        // ---- Comunicação e dia a dia ----
        new() { Name = "Discord", Description = "Chat de voz e texto para jogar com os amigos.", WingetId = "Discord.Discord", DisplayPrefix = "Discord" },
        new() { Name = "WhatsApp", Description = "WhatsApp no computador (versão oficial, sem celular conectado).", WingetId = "WhatsApp.Whatsapp", DisplayPrefix = "WhatsApp", PreSelect = false },
        new() { Name = "Telegram", Description = "Mensageiro Telegram, versão de mesa.", WingetId = "Telegram.TelegramDesktop", DisplayPrefix = "Telegram", PreSelect = false },
        new() { Name = "Zoom", Description = "Reuniões por vídeo do Zoom.", WingetId = "Zoom.Zoom", DisplayPrefix = "Zoom", PreSelect = false },

        // ---- Navegadores ----
        new() { Name = "Google Chrome", Description = "Navegador do Google.", WingetId = "Google.Chrome", DisplayPrefix = "Google Chrome" },
        new() { Name = "Firefox", Description = "Navegador da Mozilla, independente e de código aberto.", WingetId = "Mozilla.Firefox", DisplayPrefix = "Mozilla Firefox", PreSelect = false },
        new() { Name = "Brave", Description = "Navegador que bloqueia anúncios e rastreadores por padrão.", WingetId = "Brave.Brave", DisplayPrefix = "Brave", PreSelect = false },

        // ---- Utilitários ----
        new() { Name = "7-Zip", Description = "Compactador gratuito (zip, rar, 7z). Abre e cria arquivos compactados.", WingetId = "7zip.7zip", DisplayPrefix = "7-Zip" },
        new() { Name = "WinRAR", Description = "Compactador clássico (pago após o teste). Escolha ele ou o 7-Zip.", WingetId = "RARLab.WinRAR", DisplayPrefix = "WinRAR", PreSelect = false },
        new() { Name = "Lightshot", Description = "Captura de tela rápida com a tecla Print Screen.", WingetId = "Skillbrains.Lightshot", DisplayPrefix = "Lightshot" },
        new() { Name = "Notepad++", Description = "Editor de texto simples e rápido, muito melhor que o Bloco de Notas.", WingetId = "Notepad++.Notepad++", DisplayPrefix = "Notepad", PreSelect = false },
        new() { Name = "Everything", Description = "Busca de arquivos instantânea no PC inteiro (digite e aparece na hora).", WingetId = "voidtools.Everything", DisplayPrefix = "Everything", PreSelect = false },
        new() { Name = "PowerToys", Description = "Coleção oficial da Microsoft de utilitários (renomear em lote, janelas divididas…).", WingetId = "Microsoft.PowerToys", DisplayPrefix = "PowerToys", PreSelect = false },
        new() { Name = "Sumatra PDF", Description = "Leitor de PDF leve e sem frescuras.", WingetId = "SumatraPDF.SumatraPDF", DisplayPrefix = "Sumatra", PreSelect = false },
        new() { Name = "qBittorrent", Description = "Baixador de torrents gratuito e sem anúncios.", WingetId = "qBittorrent.qBittorrent", DisplayPrefix = "qBittorrent", PreSelect = false },
        new() { Name = "CPU-Z", Description = "Detalhes do processador, placa-mãe e memória.", WingetId = "CPUID.CPU-Z", DisplayPrefix = "CPU", PreSelect = false },
        new() { Name = "GPU-Z", Description = "Detalhes da placa de vídeo (sensores, BIOS, clock).", WingetId = "TechPowerUp.GPU-Z", DisplayPrefix = "GPU-Z", PreSelect = false },
        new() { Name = "CrystalDiskInfo", Description = "Saúde dos discos (S.M.A.R.T.) em detalhe.", WingetId = "CrystalDiskInfo.CrystalDiskInfo", DisplayPrefix = "CrystalDisk", PreSelect = false },

        // ---- Mídia e criação ----
        new() { Name = "VLC", Description = "Player de vídeo e música que abre qualquer formato.", WingetId = "VideoLAN.VLC", DisplayPrefix = "VLC media player" },
        new() { Name = "Spotify", Description = "Música e podcasts do Spotify.", WingetId = "Spotify.Spotify", DisplayPrefix = "Spotify", PreSelect = false },
        new() { Name = "Audacity", Description = "Editor de áudio gratuito (cortar, mixar, gravar).", WingetId = "Audacity.Audacity", PreSelect = false, DisplayPrefix = "Audacity" },
        new() { Name = "HandBrake", Description = "Conversor de vídeo gratuito (MP4, MKV, compressão).", WingetId = "HandBrake.HandBrake", DisplayPrefix = "HandBrake", PreSelect = false },
        new() { Name = "OBS Studio", Description = "Gravação e transmissão de tela para stream.", WingetId = "OBSProject.OBSStudio", DisplayPrefix = "OBS", PreSelect = false },
        new() { Name = "GIMP", Description = "Editor de imagens gratuito (alternativa ao Photoshop).", WingetId = "GIMP.GIMP", DisplayPrefix = "GIMP", PreSelect = false },
        new() { Name = "Krita", Description = "Pintura digital e desenho, gratuita.", WingetId = "Krita.Krita", DisplayPrefix = "Krita", PreSelect = false },
        new() { Name = "Blender", Description = "Modelagem 3D, animação e renderização gratuitas.", WingetId = "BlenderFoundation.Blender", DisplayPrefix = "Blender", PreSelect = false },

        // ---- Programação ----
        new() { Name = "Visual Studio Code", Description = "Editor de código da Microsoft (o mais usado do mundo).", WingetId = "Microsoft.VisualStudioCode", DisplayPrefix = "Microsoft Visual Studio Code", PreSelect = false },
        new() { Name = "Git", Description = "Controle de versão Git, com a linha de comando.", WingetId = "Git.Git", DisplayPrefix = "Git", PreSelect = false },
        new() { Name = "Obsidian", Description = "Notas conectadas em arquivos de texto no seu PC.", WingetId = "Obsidian.Obsidian", DisplayPrefix = "Obsidian", PreSelect = false },

        // ---- Runtime pós-formatação ----
        new() { Name = "Visual C++ Redistributable", Description = "Biblioteca do Visual C++ que vários jogos e programas pedem.", WingetId = "Microsoft.VCRedist.2015+.x64", DisplayPrefix = "Microsoft Visual C++", PreSelect = false },
    };

    public static string WingetPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");

    public static bool WingetAvailable => File.Exists(WingetPath);

    /// <summary>Nomes em "Aplicativos instalados" (máquina 64/32 bits e usuário atual).</summary>
    public static HashSet<string> InstalledNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string path = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32),
                                             (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string n && !string.IsNullOrWhiteSpace(n)) names.Add(n.Trim());
                }
            }
            catch { }
        }
        return names;
    }

    public static void RefreshInstalled(IEnumerable<InstallableApp> apps)
    {
        var names = InstalledNames();
        foreach (var a in apps)
        {
            a.Installed = names.Any(n => n.StartsWith(a.DisplayPrefix, StringComparison.OrdinalIgnoreCase));
            if (a.Installed) a.Selected = false;
        }
    }

    /// <summary>Instala um app em silêncio. Retorna (ok, mensagem curta).</summary>
    public static async Task<(bool Ok, string Message)> InstallAsync(InstallableApp app, CancellationToken ct, bool upgrade = false)
    {
        var psi = new ProcessStartInfo(WingetPath,
            $"{(upgrade ? "upgrade" : "install")} --id {app.WingetId} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o winget.");
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw; }
        string output = await outTask + await errTask;

        return p.ExitCode switch
        {
            0 => (true, "Instalado."),
            unchecked((int)0x8A15002B) or unchecked((int)0x8A150061) => (true, "Já estava instalado."),
            unchecked((int)0x8A150014) => (false, "Pacote não encontrado no winget."),
            unchecked((int)0x8A150008) => (false, "Falha no download: confira a internet e tente de novo."),
            _ => (false, $"Falhou (código 0x{p.ExitCode:X8}). {LastLine(output)}".Trim()),
        };
    }

    /// <summary>Última linha útil da saída do winget (sem as barras de progresso).</summary>
    private static string LastLine(string output) =>
        output.Split('\n', '\r').Select(l => l.Trim())
              .LastOrDefault(l => l.Length > 3 && !l.All(c => "-\\|/█▒ ".Contains(c)) && !l.Contains("KB /") && !l.Contains("MB /")) ?? "";
}
