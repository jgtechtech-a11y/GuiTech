using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GuiTech;

public sealed record RepairResult(int ExitCode, bool Cancelled, string? Error);

/// <summary>
/// Manutenção do Windows: SFC (arquivos do sistema), DISM (imagem do Windows) e CHKDSK (unidades).
/// Todos precisam de administrador e rodam com log ao vivo; dá para cancelar no meio.
/// </summary>
public static class SystemRepair
{
    private static string Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string Dism => Path.Combine(Windows, "System32", "Dism.exe");
    private static string Sfc => Path.Combine(Windows, "System32", "sfc.exe");
    private static string Chkdsk => Path.Combine(Windows, "System32", "chkdsk.exe");

    /// <summary>Unidades fixas prontas para verificação (C:, D:…).</summary>
    public static List<string> FixedDrives()
    {
        var list = new List<string>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType == DriveType.Fixed && d.IsReady) list.Add(d.Name.TrimEnd('\\'));
                }
                catch { }
            }
        }
        catch { }
        return list;
    }

    // ------------------------------------------------------------------ scripts

    public static string ScriptSfc() => $"& '{Sfc}' /scannow";

    public static string ScriptDismScan(bool repair) =>
        repair ? $"& '{Dism}' /Online /Cleanup-Image /RestoreHealth"
               : $"& '{Dism}' /Online /Cleanup-Image /ScanHealth";

    public static string ScriptChkdsk(string drive, bool repair) =>
        repair ? $"'Y' | & '{Chkdsk}' '{drive}:' /f"
               : $"& '{Chkdsk}' '{drive}:' /scan";

    // ------------------------------------------------------------------ execução

    /// <summary>
    /// Roda um comando (script PowerShell) lendo a saída linha por linha.
    /// A saída é decodificada como UTF-8 pelo próprio PowerShell, que entende a página de código de
    /// cada ferramenta — assim os acentos do log não saem trocados.
    /// </summary>
    public static async Task<RepairResult> RunAsync(string script, IProgress<string>? onLine, CancellationToken ct)
    {
        string full = "$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
                      "$ErrorActionPreference = 'Continue'; " +
                      script + " 2>&1 | Out-String -Stream; exit $LASTEXITCODE";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", full })
            psi.ArgumentList.Add(a);

        Process? p = null;
        try
        {
            p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o PowerShell.");

            void Emit(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                // O progresso do DISM/CHKDSK usa \r: cada trecho vira uma linha do log.
                foreach (var part in raw.Split('\r', StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = Clean(part);
                    if (line.Length > 0) onLine?.Report(line);
                }
            }

            p.OutputDataReceived += (_, e) => Emit(e.Data);
            p.ErrorDataReceived += (_, e) => Emit(e.Data);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch { }
                return new RepairResult(-1, true, null);
            }

            // Dá um instante para as últimas linhas chegarem.
            await Task.Delay(200, CancellationToken.None);
            return new RepairResult(p.ExitCode, false, null);
        }
        catch (Exception ex)
        {
            return new RepairResult(-1, false, ex.Message);
        }
        finally
        {
            p?.Dispose();
        }
    }

    /// <summary>Percentual mostrado por DISM/CHKDSK ("[========== 42,3% ]"), quando existir.</summary>
    public static double? Percent(string line)
    {
        var m = Regex.Match(line, @"(\d{1,3}(?:[.,]\d{1,2})?)\s*%");
        if (!m.Success) return null;
        return double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private static string Clean(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c == '\b') { if (sb.Length > 0) sb.Length--; continue; }
            if (char.IsControl(c) && c != '\t') continue;
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
