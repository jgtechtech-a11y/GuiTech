using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GuiTech;

/// <summary>Uma categoria de cache/lixo que pode ser analisada e apagada.</summary>
public sealed class CleanTarget : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>Pastas a esvaziar (o conteúdo, não as pastas).</summary>
    public string[]? Folders { get; init; }

    /// <summary>Se preenchido, só estes padrões de arquivo dentro das pastas.</summary>
    public string[]? Patterns { get; init; }

    public bool Recursive { get; init; } = true;

    /// <summary>Serviços do Windows que precisam parar para os arquivos ficarem livres.</summary>
    public string[]? StopServices { get; init; }

    /// <summary>Limpar a Lixeira (usa a API do Windows em vez de apagar arquivos).</summary>
    public bool RecycleBin { get; init; }

    public bool NeedsAdmin { get; init; }

    private long _bytes;
    public long Bytes
    {
        get => _bytes;
        set { _bytes = value; Raise(nameof(Bytes)); Raise(nameof(SizeText)); }
    }

    private int _files;
    public int Files
    {
        get => _files;
        set { _files = value; Raise(nameof(CountText)); }
    }

    private bool _scanned;
    public bool Scanned
    {
        get => _scanned;
        set { _scanned = value; Raise(nameof(SizeText)); Raise(nameof(CountText)); }
    }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Raise(nameof(Selected)); }
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        set { _status = value; Raise(nameof(Status)); }
    }

    public string SizeText => !Scanned ? "—" : Format(Bytes);
    public string CountText => !Scanned ? "" : Files == 1 ? "1 arquivo" : $"{Files:N0} arquivos";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static string Format(long bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:N1} GB",
        >= 1048576 => $"{bytes / 1048576.0:N1} MB",
        >= 1024 => $"{bytes / 1024.0:N0} KB",
        _ => $"{bytes:N0} B",
    };
}

/// <summary>
/// Resultado de uma limpeza. <paramref name="KeptBytes"/> é o tamanho do que sobrou porque estava
/// em uso — serve para explicar ao usuário por que o total não zerou (e não prometer o impossível).
/// </summary>
public sealed record CleanReport(long FreedBytes, int Deleted, int Kept, long KeptBytes = 0);

/// <summary>
/// Limpeza de cache do Windows: temporários, relatórios de erro, miniaturas, shaders da GPU,
/// cache do Windows Update e Lixeira. Só apaga arquivo de cache — nada de programa ou documento.
/// </summary>
public static class SystemCleaner
{
    private static readonly string NetExtractionDir =
        Path.Combine(Path.GetTempPath(), ".net") + Path.DirectorySeparatorChar;

    public static List<CleanTarget> Targets()
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return new List<CleanTarget>
        {
            new()
            {
                Key = "usertemp", Name = "Temporários do usuário",
                Description = "%TEMP% — restos de instaladores, extrações e programas",
                Folders = new[] { Path.GetTempPath() }, Selected = true,
            },
            new()
            {
                Key = "wintemp", Name = "Temporários do Windows",
                Description = "C:\\Windows\\Temp — usado por instaladores e atualizações",
                Folders = new[] { Path.Combine(win, "Temp") }, NeedsAdmin = true, Selected = true,
            },
            new()
            {
                Key = "wer", Name = "Relatórios de erro",
                Description = "Relatórios de travamento guardados pelo Windows",
                Folders = new[] { Path.Combine(programData, "Microsoft", "Windows", "WER") }, NeedsAdmin = true,
            },
            new()
            {
                Key = "thumbs", Name = "Miniaturas e ícones",
                Description = "Cache de miniaturas do Explorer (o Windows recria sozinho)",
                Folders = new[] { Path.Combine(local, "Microsoft", "Windows", "Explorer") },
                Patterns = new[] { "thumbcache_*.db", "iconcache_*.db", "thumbs.db" },
                Recursive = false, Selected = true,
            },
            new()
            {
                Key = "d3d", Name = "Cache do DirectX",
                Description = "Shaders compilados por jogos e pela GPU (recriados ao abrir o jogo)",
                Folders = new[] { Path.Combine(local, "D3DSCache") }, Selected = true,
            },
            new()
            {
                Key = "nvidia", Name = "Shaders NVIDIA",
                Description = "Cache de shaders do driver NVIDIA (recriado ao abrir o jogo)",
                Folders = new[]
                {
                    Path.Combine(local, "NVIDIA", "DXCache"),
                    Path.Combine(local, "NVIDIA", "GLCache"),
                },
            },
            new()
            {
                Key = "amd", Name = "Shaders AMD",
                Description = "Cache de shaders do driver AMD (recriado ao abrir o jogo)",
                Folders = new[]
                {
                    Path.Combine(local, "AMD", "DxCache"),
                    Path.Combine(local, "AMD", "DxcCache"),
                    Path.Combine(local, "AMD", "GLCache"),
                },
            },
            new()
            {
                Key = "dumps", Name = "Despejos de memória",
                Description = "Arquivos .dmp deixados por programas que travaram",
                Folders = new[] { Path.Combine(local, "CrashDumps") },
            },
            new()
            {
                Key = "wu", Name = "Cache do Windows Update",
                Description = "Instaladores já aplicados em Windows\\SoftwareDistribution\\Download",
                Folders = new[] { Path.Combine(win, "SoftwareDistribution", "Download") },
                NeedsAdmin = true, StopServices = new[] { "wuauserv" },
            },
            new()
            {
                Key = "do", Name = "Cache de entrega de atualizações",
                Description = "Pedaços de atualizações baixados para outros PCs da rede",
                Folders = new[] { Path.Combine(programData, "Microsoft", "Network", "Downloader") },
                NeedsAdmin = true, StopServices = new[] { "dosvc" },
            },
            new()
            {
                Key = "recycle", Name = "Lixeira",
                Description = "Arquivos que você já excluiu (não dá para recuperar depois)",
                RecycleBin = true,
            },
        };
    }

    /// <summary>Mede o tamanho de cada categoria marcada. Roda em segundo plano.</summary>
    public static void Scan(IEnumerable<CleanTarget> targets, Action? progress = null)
    {
        foreach (var t in targets)
        {
            long bytes = 0;
            int files = 0;
            try
            {
                if (t.RecycleBin)
                {
                    var info = QueryRecycleBin();
                    bytes = info.Size;
                    files = (int)Math.Min(int.MaxValue, info.Items);
                }
                else
                {
                    foreach (var f in Enumerate(t))
                    {
                        try { bytes += f.Length; files++; } catch { }
                    }
                }
            }
            catch { }
            t.Bytes = bytes;
            t.Files = files;
            t.Scanned = true;
            progress?.Invoke();
        }
    }

    /// <summary>Apaga o conteúdo das categorias marcadas. Roda em segundo plano.</summary>
    public static CleanReport Clean(IEnumerable<CleanTarget> targets, Action<string>? progress = null)
    {
        var list = targets.ToList();
        long freed = 0;
        int deleted = 0, kept = 0;
        long keptBytes = 0;

        // Serviços que precisam parar para os arquivos ficarem livres (Windows Update e entrega).
        var stopped = new List<string>();
        foreach (var s in list.SelectMany(t => t.StopServices ?? Array.Empty<string>()).Distinct())
        {
            if (!SensorMonitor.IsElevated) break;
            progress?.Invoke($"Parando o serviço {s}…");
            if (Run("net", $"stop {s} /y")) stopped.Add(s);
        }

        try
        {
            foreach (var t in list)
            {
                if (t.RecycleBin)
                {
                    long size = QueryRecycleBin().Size;
                    if (EmptyRecycleBin())
                    {
                        freed += size;
                        deleted += t.Files;
                        progress?.Invoke($"Lixeira esvaziada ({CleanTarget.Format(size)}).");
                    }
                    else
                    {
                        kept++;
                        progress?.Invoke("A Lixeira não pôde ser esvaziada.");
                    }
                    continue;
                }

                progress?.Invoke($"Limpando {t.Name.ToLowerInvariant()}…");
                foreach (var f in Enumerate(t))
                {
                    long len = 0;
                    try
                    {
                        len = f.Length;
                        f.Delete();
                        freed += len;
                        deleted++;
                    }
                    catch { kept++; keptBytes += len; } // arquivo em uso por algum programa: fica para a próxima
                }

                // Remove as subpastas que ficaram vazias (as pastas principais continuam existindo).
                try
                {
                    if (!t.Recursive || t.Folders == null) continue;
                    foreach (var root in t.Folders)
                    {
                        if (!Directory.Exists(root)) continue;
                        foreach (var d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                                                     .OrderByDescending(p => p.Length))
                        {
                            try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
                        }
                    }
                }
                catch { }
            }
        }
        finally
        {
            foreach (var s in stopped)
            {
                progress?.Invoke($"Religando o serviço {s}…");
                Run("net", $"start {s}");
            }
        }

        return new CleanReport(freed, deleted, kept, keptBytes);
    }

    private static bool Run(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(20_000)) { try { p.Kill(true); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static IEnumerable<FileInfo> Enumerate(CleanTarget t)
    {
        if (t.Folders == null) yield break;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = t.Recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var root in t.Folders)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

            List<string> found = new();
            try
            {
                if (t.Patterns is { Length: > 0 })
                {
                    foreach (var pattern in t.Patterns)
                        found.AddRange(Directory.EnumerateFiles(root, pattern, options));
                }
                else
                {
                    found.AddRange(Directory.EnumerateFiles(root, "*", options));
                }
            }
            catch
            {
                // pasta protegida ou inexistente: devolve o que já conseguiu listar
            }

            foreach (var f in found)
            {
                // %TEMP%\.net é onde o .NET extrai as bibliotecas nativas dos apps single-file:
                // são arquivos do app que está aberto, travados e gerenciados pelo próprio .NET.
                // Contar isso como "lixo" faria o ponteiro nunca chegar a zero.
                if (f.StartsWith(NetExtractionDir, StringComparison.OrdinalIgnoreCase)) continue;
                FileInfo info;
                try { info = new FileInfo(f); } catch { continue; }
                yield return info;
            }
        }
    }

    // ------------------------------------------------------------------ Lixeira

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    private static (long Size, long Items) QueryRecycleBin()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        try
        {
            return SHQueryRecycleBin(null, ref info) == 0 ? (info.i64Size, info.i64NumItems) : (0, 0);
        }
        catch { return (0, 0); }
    }

    private static bool EmptyRecycleBin()
    {
        const uint NoConfirmation = 0x1, NoProgress = 0x2, NoSound = 0x4;
        try { return SHEmptyRecycleBin(IntPtr.Zero, null, NoConfirmation | NoProgress | NoSound) == 0; }
        catch { return false; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
