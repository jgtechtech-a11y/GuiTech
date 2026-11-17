using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GuiTech;

/// <summary>Programa (agrupado pelo nome do executável) que pode ser fechado para liberar recursos.</summary>
public sealed class BackgroundApp : INotifyPropertyChanged
{
    public string ProcessName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public long MemoryBytes { get; init; }
    public int Count { get; init; }
    public bool HasWindow { get; init; }
    public bool Suggested { get; init; }

    /// <summary>Segundo plano: nenhuma janela na tela (auxiliar de programa ou iniciado junto com o Windows).</summary>
    public bool IsBackground => !HasWindow;

    /// <summary>Não usou CPU na amostra: está parado em segundo plano.</summary>
    public bool IsIdle { get; init; }

    public double CpuPercent { get; init; }

    public string MemoryText => $"{MemoryBytes / 1048576.0:N0} MB";

    /// <summary>Etiqueta curta mostrada na lista.</summary>
    public string KindText => (IsBackground, IsIdle) switch
    {
        (true, true) => "2º PLANO · PARADO",
        (true, false) => "2º PLANO",
        (false, true) => "INATIVO",
        _ => "JANELA",
    };

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record CleanupResult(long FreedBytes, int Trimmed, bool StandbyPurged, string? StandbyError)
{
    /// <summary>O cache de arquivos do sistema (working set do sistema) foi esvaziado?</summary>
    public bool FileCacheCleared { get; init; }
    public string? FileCacheError { get; init; }
}
public sealed record CloseResult(List<string> Closed, List<string> NotClosed);

/// <summary>Limpeza de memória e fechamento de programas em segundo plano.</summary>
public static class Optimizer
{
    /// <summary>Processos que nunca aparecem na lista (sistema, segurança, drivers, o próprio app).</summary>
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso", "svchost",
        "dwm", "explorer", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "RuntimeBroker", "StartMenuExperienceHost",
        "ShellExperienceHost", "SearchHost", "SearchIndexer", "TextInputHost", "SecurityHealthService", "SecurityHealthSystray",
        "MsMpEng", "NisSrv", "SgrmBroker", "audiodg", "conhost", "dllhost", "spoolsv", "WmiPrvSE", "LockApp", "ApplicationFrameHost",
        "SystemSettings", "smartscreen", "MemCompression", "Secure System", "WUDFHost", "dasHost", "nvcontainer", "NVDisplay.Container",
        "atiesrxx", "atieclxx", "amdfendrsr", "RadeonSoftware", "igfxEM", "PawnIO", "GuiTech", "cmd", "powershell", "WindowsTerminal",
        "OpenConsole", "devenv", "Code", "dotnet", "AppVShNotify", "backgroundTaskHost", "TrustedInstaller",
        "CompPkgSrv", "UserOOBEBroker", "SecurityHealthHost",
    };

    /// <summary>Processos de segundo plano seguros de fechar durante o jogo (pré-marcados).</summary>
    private static readonly Dictionary<string, string> SuggestedApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OneDrive"] = "Sincronização do OneDrive (pode usar disco e rede)",
        ["ms-teams"] = "Microsoft Teams",
        ["Teams"] = "Microsoft Teams (clássico)",
        ["Skype"] = "Skype",
        ["PhoneExperienceHost"] = "Vincular ao Celular",
        ["YourPhone"] = "Vincular ao Celular",
        ["Widgets"] = "Widgets do Windows",
        ["WidgetService"] = "Serviço de Widgets",
        ["AdobeARM"] = "Atualizador da Adobe",
        ["CCXProcess"] = "Adobe Creative Cloud",
        ["Creative Cloud"] = "Adobe Creative Cloud",
        ["AdobeIPCBroker"] = "Adobe (comunicação em segundo plano)",
        ["GoogleDriveFS"] = "Google Drive",
        ["Dropbox"] = "Dropbox",
        ["Cortana"] = "Cortana",
        ["OfficeClickToRun"] = "Atualizador do Office",
    };

    /// <summary>
    /// Lista o que pode ser fechado antes de jogar: programas com janela, programas de segundo plano (sem janela)
    /// e programas parados. Programas do próprio Windows (pasta Windows) e processos de sistema ficam de fora.
    /// A chamada demora ~0,7 s porque mede a CPU para saber quem está parado: rode em segundo plano.
    /// </summary>
    public static List<BackgroundApp> ListApps()
    {
        int session = Process.GetCurrentProcess().SessionId;
        var groups = new Dictionary<string, List<Process>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.SessionId != session || Protected.Contains(p.ProcessName)) continue;
                if (!groups.TryGetValue(p.ProcessName, out var list)) groups[p.ProcessName] = list = new List<Process>();
                list.Add(p);
            }
            catch { }
        }

        // Amostra curta de CPU: quem não gastou CPU nesse intervalo está parado.
        var before = new Dictionary<int, TimeSpan>();
        foreach (var p in groups.Values.SelectMany(l => l))
        {
            try { before[p.Id] = p.TotalProcessorTime; } catch { }
        }
        var sw = Stopwatch.StartNew();
        Thread.Sleep(SampleMilliseconds);
        sw.Stop();
        double seconds = Math.Max(0.05, sw.Elapsed.TotalSeconds);
        int cores = Math.Max(1, Environment.ProcessorCount);

        var result = new List<BackgroundApp>();
        foreach (var (name, procs) in groups)
        {
            bool hasWindow = procs.Any(p => SafeWindow(p) != IntPtr.Zero);
            bool suggested = SuggestedApps.ContainsKey(name);

            // Sem janela: só entra se for programa de usuário (fora de Windows e WindowsApps).
            if (!hasWindow && IsSystemComponent(procs[0])) continue;

            double cpuMs = 0;
            foreach (var p in procs)
            {
                try
                {
                    if (before.TryGetValue(p.Id, out var t0)) cpuMs += (p.TotalProcessorTime - t0).TotalMilliseconds;
                }
                catch { }
            }
            double cpu = cpuMs / 1000.0 / seconds / cores * 100.0;

            string title = name;
            try
            {
                if (procs[0].MainModule?.FileVersionInfo.FileDescription is { Length: > 0 } d) title = d.Trim();
            }
            catch { }

            var parts = new List<string>();
            if (suggested) parts.Add(SuggestedApps[name]);
            parts.Add(hasWindow ? "Programa aberto" : "Segundo plano · sem janela");
            if (cpu < IdleCpuPercent) parts.Add($"parado ({cpu:0.#}% de CPU)");
            if (procs.Count > 1) parts.Add($"{procs.Count} processos");

            long mem = procs.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } });

            result.Add(new BackgroundApp
            {
                ProcessName = name, Title = title, Detail = string.Join(" · ", parts), MemoryBytes = mem, Count = procs.Count,
                HasWindow = hasWindow, Suggested = suggested, IsIdle = cpu < IdleCpuPercent, CpuPercent = cpu,
                Selected = suggested
            });
        }
        return result
            .OrderByDescending(a => a.Suggested)
            .ThenByDescending(a => a.MemoryBytes)
            .ToList();
    }

    /// <summary>Programa de dentro da pasta Windows ou de Apps do Windows não é oferecido para fechar.</summary>
    private static bool IsSystemComponent(Process p)
    {
        try
        {
            string? path = p.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return true; // sem acesso ao caminho: trata como componente do sistema
            string sep = Path.DirectorySeparatorChar.ToString();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (path.StartsWith(win + sep, StringComparison.OrdinalIgnoreCase)) return true;

            string apps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            return path.StartsWith(apps + sep, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    private const int SampleMilliseconds = 700;
    private const double IdleCpuPercent = 1.0;

    private static IntPtr SafeWindow(Process p) { try { return p.MainWindowHandle; } catch { return IntPtr.Zero; } }

    /// <summary>
    /// Fecha os programas: com janela → pede para fechar (como clicar no X), sem forçar, para não perder nada
    /// não salvo; sem janela (segundo plano) → encerra.
    /// </summary>
    public static CloseResult CloseApps(IEnumerable<BackgroundApp> apps)
    {
        var closed = new List<string>();
        var notClosed = new List<string>();
        foreach (var app in apps)
        {
            foreach (var p in Process.GetProcessesByName(app.ProcessName))
            {
                try
                {
                    if (SafeWindow(p) != IntPtr.Zero) p.CloseMainWindow();
                    // Sem janela: encerra o processo e os filhos (ex.: auxiliares do navegador/app).
                    else p.Kill(true);
                }
                catch { }
            }
            // Dá até 4 s para fecharem sozinhos.
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 4 && Process.GetProcessesByName(app.ProcessName).Length > 0) Thread.Sleep(200);

            // O que sobrou sem janela não tem como salvar nada: encerra (com os filhos).
            foreach (var p in Process.GetProcessesByName(app.ProcessName))
            {
                try
                {
                    if (SafeWindow(p) == IntPtr.Zero) p.Kill(true);
                }
                catch { }
            }
            if (Process.GetProcessesByName(app.ProcessName).Length == 0) closed.Add(app.Title);
            else notClosed.Add(app.Title);
        }
        return new CloseResult(closed, notClosed);
    }

    /// <summary>
    /// Esvazia a lista de espera (standby) da RAM, enxuga o working set dos processos e limpa o
    /// cache de arquivos do sistema. A lista de espera é cache que o Windows libera sozinho quando
    /// precisa; esvaziá-la antes de jogar ajuda quando a RAM está quase cheia e o jogo engasga ao
    /// carregar áreas novas.
    /// </summary>
    public static CleanupResult CleanMemory()
    {
        long before = AvailableBytes();
        int trimmed = 0;
        int self = Environment.ProcessId;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == self || p.Id <= 4) continue;
                if (EmptyWorkingSet(p.Handle)) trimmed++;
            }
            catch { }
        }

        bool purged = false;
        string? error = null;
        try
        {
            EnablePrivilege("SeProfileSingleProcessPrivilege");
            int command = 4; // MemoryPurgeStandbyList
            int status = NtSetSystemInformation(80 /* SystemMemoryListInformation */, ref command, sizeof(int));
            purged = status == 0;
            if (!purged) error = $"o Windows recusou (0x{status:X8}); é preciso rodar como administrador";
        }
        catch (Exception ex) { error = ex.Message; }

        // Cache de arquivos do sistema ("Empty System Working Set" do RAMMap): devolve ao Windows
        // a memória usada como cache de arquivos. Exige SeIncreaseQuotaPrivilege (o app roda como admin).
        bool cacheCleared = false;
        string? cacheError = null;
        try
        {
            EnablePrivilege("SeIncreaseQuotaPrivilege");
            if (SetSystemFileCacheSize(-1, -1, 0)) cacheCleared = true;
            else cacheError = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }
        catch (Exception ex) { cacheError = ex.Message; }

        Thread.Sleep(300);
        return new CleanupResult(Math.Max(0, AvailableBytes() - before), trimmed, purged, error)
        {
            FileCacheCleared = cacheCleared,
            FileCacheError = cacheError,
        };
    }

    public static (long Total, long Available) MemoryStatus()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref m) ? ((long)m.ullTotalPhys, (long)m.ullAvailPhys) : (0, 0);
    }

    private static long AvailableBytes() => MemoryStatus().Available;

    private static void EnablePrivilege(string name)
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out var token)) // ADJUST | QUERY
            throw new Win32Exception();
        try
        {
            if (!LookupPrivilegeValue(null, name, out long luid)) throw new Win32Exception();
            var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = 2 }; // SE_PRIVILEGE_ENABLED
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally { CloseHandle(token); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public long Luid; public uint Attributes; }

    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr hProcess);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemFileCacheSize(nint minimumFileCacheSize, nint maximumFileCacheSize, uint flags);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, uint len, IntPtr prev, IntPtr retLen);
}
