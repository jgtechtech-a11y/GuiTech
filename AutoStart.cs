using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace GuiTech;

/// <summary>
/// Liga/desliga o Gui_Tech na inicialização do Windows.
///
/// O método principal é uma <b>tarefa agendada no logon com privilégios máximos</b>. Como o app
/// precisa de administrador para ler os sensores, a chave Run do Windows não é confiável: o Windows
/// 11 mostra o pedido do UAC no logon e, se ele não for aceito, o programa simplesmente não abre.
/// A tarefa agendada inicia com o token elevado, sem pedir confirmação. A chave Run continua como
/// reserva para o caso de a tarefa não poder ser criada (política do sistema, por exemplo).
///
/// Quando ligado, o programa abre com <c>--tray</c> e fica só na bandeja do sistema.
/// </summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GuiTech";
    private const string TaskName = "GuiTech";
    private const string MarkerKey = @"Software\GuiTech";
    private const string MarkerValue = "AutoStartExe";

    /// <summary>Estado atual da inicialização automática.</summary>
    public readonly record struct State(bool Enabled, string? Exe, bool PointsToOtherExe, bool UsesTask);

    public static State GetState()
    {
        bool task = TaskExists();
        string? run = RunExe;
        string? exe = task ? (MarkerExe ?? Environment.ProcessPath) : run;
        return new State(task || run != null, exe, PointsToOtherExe(exe), task);
    }

    /// <summary>Converte a chave Run antiga (v0.21.0 e anteriores) na tarefa agendada.</summary>
    public static void MigrateIfNeeded()
    {
        try
        {
            string? current = Environment.ProcessPath;
            string? run = RunExe;
            if (current == null || run == null) return;

            if (TaskExists())
            {
                // Já existe tarefa. Só remove a chave antiga se ela aponta para esta cópia,
                // para não apagar a inicialização de outra cópia que o usuário tenha guardado.
                string? taskExe = MarkerExe ?? current;
                if (SamePath(taskExe, current)) DeleteRun();
                if (MarkerExe == null) SetMarker(current);
                return;
            }

            // Só migra quando a chave Run aponta para o .exe que está rodando agora.
            if (!SamePath(run, current)) return;
            if (CreateTask(current))
            {
                SetMarker(current);
                DeleteRun();
            }
        }
        catch { }
    }

    public static void SetEnabled(bool on)
    {
        if (!on)
        {
            DeleteTask();
            DeleteRun();
            DeleteMarker();
            return;
        }

        string exe = Environment.ProcessPath
                     ?? throw new InvalidOperationException("Não foi possível descobrir o caminho do Gui_Tech.");

        if (CreateTask(exe))
        {
            SetMarker(exe);
            DeleteRun(); // evita abrir duas vezes e o UAC da chave Run
            return;
        }

        // Reserva: chave Run. Pode pedir confirmação do Windows no logon.
        SetRun(exe);
        DeleteMarker();
    }

    // ------------------------------------------------------------ tarefa agendada

    private static bool CreateTask(string exe)
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name) ?? "";
        string command = SecurityElement.Escape(exe) ?? "";
        string xml =
$@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>Gui_Tech</Author>
    <Description>Inicia o Gui_Tech na bandeja ao entrar no Windows.</Description>
    <URI>\{TaskName}</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{user}</UserId>
      <Delay>PT5S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{user}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{command}</Command>
      <Arguments>--tray</Arguments>
    </Exec>
  </Actions>
</Task>";

        string file = Path.Combine(Path.GetTempPath(), $"GuiTech-autostart-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            return Schtasks("/Create", "/TN", TaskName, "/XML", file, "/F") == 0;
        }
        catch { return false; }
        finally { try { File.Delete(file); } catch { } }
    }

    private static bool TaskExists() => Schtasks("/Query", "/TN", TaskName) == 0;

    private static void DeleteTask()
    {
        try { Schtasks("/Delete", "/TN", TaskName, "/F"); } catch { }
    }

    /// <summary>Executa schtasks.exe sem janela e devolve o código de saída (-1 em falha).</summary>
    internal static int Schtasks(params string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi);
        if (p == null) return -1;

        // Lê a saída em paralelo para não travar se o buffer encher.
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(20000))
        {
            try { p.Kill(); } catch { }
            return -1;
        }
        _ = stdout.Result;
        _ = stderr.Result;
        return p.ExitCode;
    }

    // ------------------------------------------------------------ chave Run (reserva)

    private static string? RunExe
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                string? raw = k?.GetValue(ValueName) as string;
                if (string.IsNullOrWhiteSpace(raw)) return null;
                raw = raw.Trim();
                if (raw.StartsWith('"'))
                {
                    int end = raw.IndexOf('"', 1);
                    return end > 1 ? raw[1..end] : raw.Trim('"');
                }
                // Sem aspas: o Windows grava "caminho argumentos"; o caminho termina em .exe
                int exe = raw.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                return exe >= 0 ? raw[..(exe + 4)] : raw;
            }
            catch { return null; }
        }
    }

    private static void SetRun(string exe)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
                      ?? throw new InvalidOperationException("Não foi possível abrir a chave de inicialização do Windows.");
        k.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
    }

    private static void DeleteRun()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            k?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }

    // ------------------------------------------------------------ marcador do caminho

    private static string? MarkerExe
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(MarkerKey);
                return k?.GetValue(MarkerValue) as string;
            }
            catch { return null; }
        }
    }

    private static void SetMarker(string exe)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(MarkerKey, writable: true);
            k?.SetValue(MarkerValue, exe, RegistryValueKind.String);
        }
        catch { }
    }

    private static void DeleteMarker()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(MarkerKey, writable: true);
            k?.DeleteValue(MarkerValue, throwOnMissingValue: false);
        }
        catch { }
    }

    private static bool SamePath(string? a, string? b)
    {
        if (a == null || b == null) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static bool PointsToOtherExe(string? registered)
    {
        string? current = Environment.ProcessPath;
        if (registered == null || current == null) return false;
        return !SamePath(registered, current);
    }
}
