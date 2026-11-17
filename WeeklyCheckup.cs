using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace GuiTech;

/// <summary>
/// Revisão semanal automática: uma tarefa agendada abre o Gui_Tech com <c>--revisao</c> uma vez por
/// semana (domingo, por volta das 11:00 — se o PC estiver desligado, roda na próxima vez que ligar).
/// A revisão roda o VERIFICAR PC, aplica as correções seguras e grava o resultado em
/// %AppData%\GuiTech\revisao.txt. Se o Gui_Tech já estiver aberto, a revisão roda dentro dele e
/// aparece como aviso na tela.
/// </summary>
public static class WeeklyCheckup
{
    private const string TaskName = "GuiTechRevisao";

    /// <summary>Estado atual da revisão semanal, para a interface.</summary>
    public readonly record struct State(bool TaskExists, DateTime? LastRun);

    public static State GetState()
    {
        DateTime? last = null;
        try { last = AppSettings.Load().WeeklyReviewLast; } catch { }
        return new State(IsEnabled(), last);
    }

    public static bool IsEnabled()
    {
        try { return Schtasks("/Query", "/TN", TaskName) == 0; }
        catch { return false; }
    }

    /// <summary>Cria (ou recria) a tarefa semanal para o .exe atual. false = o Windows recusou.</summary>
    public static bool SetEnabled(bool on)
    {
        if (!on)
        {
            try { Schtasks("/Delete", "/TN", TaskName, "/F"); } catch { }
            return true;
        }

        string? exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível descobrir o caminho do Gui_Tech.");
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name) ?? "";
        string command = SecurityElement.Escape(exe) ?? "";
        // Domingo 11:00 com StartWhenAvailable: se o PC estiver desligado no horário, roda ao ligar.
        string xml =
$@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>Gui_Tech</Author>
    <Description>Revisão semanal do Gui_Tech: verificar e corrigir o PC com segurança.</Description>
    <URI>\{TaskName}</URI>
  </RegistrationInfo>
  <Triggers>
    <CalendarTrigger>
      <ScheduleByWeek>
        <DaysOfWeek><Sunday/></DaysOfWeek>
        <WeeksInterval>1</WeeksInterval>
      </ScheduleByWeek>
      <StartBoundary>2026-01-04T11:00:00</StartBoundary>
      <Enabled>true</Enabled>
    </CalendarTrigger>
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
    <ExecutionTimeLimit>PT2H</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{command}</Command>
      <Arguments>--revisao</Arguments>
    </Exec>
  </Actions>
</Task>";

        string file = Path.Combine(Path.GetTempPath(), $"GuiTech-weekly-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            return Schtasks("/Create", "/TN", TaskName, "/XML", file, "/F") == 0;
        }
        catch { return false; }
        finally { try { File.Delete(file); } catch { } }
    }

    /// <summary>Roda a revisão sem gui: VERIFICAR PC, corrigir o que é seguro e verificar de novo.
    /// Grava o relatório em revisao.txt e devolve um resumo curto para o aviso na tela.</summary>
    public static string RunSilent()
    {
        var settings = AppSettings.Load();
        var before = PcCheck.Run((_, _) => { });
        var fix = PcCheck.Fix(settings, (_, _) => { });
        var after = PcCheck.Run((_, _) => { });

        settings.WeeklyReviewLast = DateTime.Now;
        try { settings.Save(); } catch { }

        var sb = new StringBuilder();
        sb.AppendLine($"Gui_Tech · revisão automática de {DateTime.Now:dd/MM/yyyy HH:mm}");
        sb.AppendLine();
        sb.AppendLine($"Antes:   {before.Summary}");
        sb.AppendLine($"Corrigir: {fix.Summary}");
        if (fix.FreedBytes > 0) sb.AppendLine($"          {fix.FreedBytes / (1024.0 * 1024.0):0.#} MB liberados");
        sb.AppendLine($"Depois:  {after.Summary}");
        sb.AppendLine();
        foreach (var item in after.Items)
            sb.AppendLine($"[{item.Badge,-12}] {item.Title} — {item.Detail}");
        sb.AppendLine();
        foreach (var line in fix.Done) sb.AppendLine("Feito: " + line);
        foreach (var line in fix.Skipped) sb.AppendLine("Deixado de fora: " + line);

        try
        {
            Directory.CreateDirectory(AppSettings.Folder);
            File.WriteAllText(Path.Combine(AppSettings.Folder, "revisao.txt"), sb.ToString());
        }
        catch { } // sem lugar para gravar: a revisão ainda aconteceu

        string freed = fix.FreedBytes > 0 ? $", ~{fix.FreedBytes / (1024.0 * 1024.0):0.#} MB liberados" : "";
        return $"Liberei espaço{freed}. {after.Headline}";
    }

    /// <summary>Executa schtasks.exe sem janela (mesmo helper do AutoStart).</summary>
    private static int Schtasks(params string[] args) => AutoStart.Schtasks(args);
}
