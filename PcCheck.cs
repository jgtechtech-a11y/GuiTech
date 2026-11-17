using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace GuiTech;

/// <summary>Como um item da verificação terminou.</summary>
public enum PcCheckLevel { Fixed, Ok, Suggestion, Warning }

/// <summary>Uma linha do resultado do "Verificar PC".</summary>
public sealed class PcCheckItem
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public PcCheckLevel Level { get; init; }

    /// <summary>Tamanho medido (cache em bytes) — usado para montar o antes → depois.</summary>
    public long Bytes { get; init; }
    /// <summary>Fração em uso (memória RAM) — usado para montar o antes → depois.</summary>
    public double Ratio { get; init; }

    public string Badge => Level switch
    {
        PcCheckLevel.Fixed => "CORRIGIDO",
        PcCheckLevel.Ok => "OK",
        PcCheckLevel.Suggestion => "PODE OTIMIZAR",
        _ => "ATENÇÃO",
    };
}

public sealed class PcCheckReport
{
    public List<PcCheckItem> Items { get; } = new();
    public long FreedBytes { get; set; }

    public int FixedCount => Items.Count(i => i.Level == PcCheckLevel.Fixed);
    public int WarningCount => Items.Count(i => i.Level == PcCheckLevel.Warning);
    public int SuggestionCount => Items.Count(i => i.Level == PcCheckLevel.Suggestion);

    public string Summary
    {
        get
        {
            var parts = new List<string> { $"{Items.Count} verificações" };
            if (FixedCount > 0) parts.Add($"{FixedCount} corrigida(s)");
            if (WarningCount > 0) parts.Add($"{WarningCount} atenção");
            if (SuggestionCount > 0) parts.Add($"{SuggestionCount} sugestão(ões)");
            return string.Join(" · ", parts);
        }
    }

    public string Headline => WarningCount > 0
        ? $"Terminei, mas encontrei {WarningCount} ponto(s) de atenção. Veja a lista."
        : SuggestionCount > 0
            ? $"PC saudável. Ainda dá para melhorar em {SuggestionCount} ponto(s) — veja a lista."
            : "Tudo em ordem: nada urgente encontrado nesta verificação.";
}

/// <summary>O que o botão "Corrigir" conseguiu aplicar.</summary>
public sealed class PcFixReport
{
    public long FreedBytes { get; set; }
    public int RemovedStartup { get; set; }

    /// <summary>Parte do que foi liberado que veio de cache/temporários (para o antes → depois).</summary>
    public long CacheFreedBytes { get; set; }
    /// <summary>Arquivos de cache que ficaram porque algum programa estava usando.</summary>
    public int CacheKeptFiles { get; set; }
    public long CacheKeptBytes { get; set; }
    /// <summary>RAM que voltou para o sistema (memória em espera/working set).</summary>
    public long MemoryFreedBytes { get; set; }

    public List<string> Done { get; } = new();
    public List<string> Skipped { get; } = new();

    public string Summary
    {
        get
        {
            if (Done.Count == 0) return "Não havia nada seguro para corrigir automaticamente agora.";
            var parts = new List<string> { string.Join(" · ", Done) };
            if (Skipped.Count > 0) parts.Add($"ficou de fora: {string.Join("; ", Skipped)}");
            return string.Join(". ", parts);
        }
    }
}

/// <summary>
/// "Verificar PC" da tela inicial: confere memória, disco, SMART, dispositivos, inicialização,
/// programas em segundo plano, sistema de arquivos e atualizações pendentes.
/// A verificação é <b>somente leitura</b>: nada é apagado nem alterado. O botão CORRIGIR é quem
/// aplica as otimizações seguras (cache/temporários, memória em espera e inicialização quebrada).
/// Nunca apaga documentos nem a Lixeira.
/// </summary>
public static class PcCheck
{
    /// <summary>Categorias de cache que podem ser apagadas sem risco de perder nada do usuário.</summary>
    private static readonly HashSet<string> AutoSafe = new(StringComparer.OrdinalIgnoreCase)
    {
        "usertemp", "wintemp", "wer", "thumbs", "d3d", "nvidia", "amd", "dumps",
    };

    private const long Mb = 1024L * 1024L;

    public static PcCheckReport Run(Action<string, double>? progress = null, bool includeFileSystem = true)
    {
        var report = new PcCheckReport();
        void Step(double fraction, string text) => progress?.Invoke(text, fraction);

        // 1) Memória: só informa; quem libera é o botão CORRIGIR.
        Step(0.04, "Conferindo a memória RAM…");
        try
        {
            var (total, avail) = Optimizer.MemoryStatus();
            if (total > 0)
            {
                double used = (double)(total - avail) / total;
                if (used >= 0.75)
                {
                    report.Items.Add(new PcCheckItem
                    {
                        Level = PcCheckLevel.Suggestion,
                        Title = "Memória RAM apertada",
                        Detail = $"Está com {used:P0} em uso ({avail / (double)Mb:N0} MB livres de {total / (double)Mb:N0} MB). " +
                                 "Clique em CORRIGIR para devolver a memória em espera.",
                        Ratio = used, Bytes = avail,
                    });
                }
                else
                {
                    report.Items.Add(new PcCheckItem
                    {
                        Level = PcCheckLevel.Ok,
                        Title = "Memória RAM",
                        Detail = $"{used:P0} em uso ({avail / (double)Mb:N0} MB livres de {total / (double)Mb:N0} MB). Sem aperto.",
                        Ratio = used, Bytes = avail,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Warning, Title = "Memória RAM", Detail = "Não foi possível ler: " + ex.Message });
        }

        // 2) Temporários e cache: só mede; quem apaga é o botão CORRIGIR.
        Step(0.14, "Procurando temporários e cache acumulado…");
        try
        {
            var targets = SystemCleaner.Targets().Where(t => AutoSafe.Contains(t.Key)).ToList();
            SystemCleaner.Scan(targets);
            long bytes = targets.Sum(t => t.Bytes);
            int files = targets.Sum(t => t.Files);

            if (bytes >= 10 * Mb)
            {
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Temporários e cache acumulados",
                    Detail = $"{files:N0} arquivo(s) somando {CleanTarget.Format(bytes)}. " +
                             "CORRIGIR apaga o que estiver livre — o que estiver aberto em outro programa " +
                             "só sai quando ele fechar (nada de documentos ou Lixeira é tocado).",
                    Bytes = bytes,
                });
            }
            else
            {
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Ok,
                    Title = "Temporários e cache",
                    Detail = bytes > 0 ? $"Só {CleanTarget.Format(bytes)} acumulados — nada que valha a pena apagar agora."
                                       : "Nada acumulado: os caches já estão vazios.",
                    Bytes = bytes,
                });
            }
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Warning, Title = "Temporários e cache", Detail = "Falhou: " + ex.Message });
        }

        // 3) Saúde dos discos (SMART) e espaço livre.
        Step(0.46, "Conferindo a saúde dos discos (S.M.A.R.T.)…");
        try
        {
            var disks = DiskHealth.Read();
            var alerts = disks.Where(d => d.IsAlert).ToList();
            if (disks.Count == 0)
            {
                report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Ok, Title = "Saúde dos discos", Detail = "Este Windows não informou o S.M.A.R.T. dos discos." });
            }
            else if (alerts.Count == 0)
            {
                report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Ok, Title = "Saúde dos discos", Detail = $"{disks.Count} disco(s) com saúde boa." });
            }
            else
            {
                foreach (var d in alerts)
                    report.Items.Add(new PcCheckItem
                    {
                        Level = PcCheckLevel.Warning,
                        Title = $"Disco: {d.Title}",
                        Detail = $"{d.VerdictText}{(d.Reasons.Length > 0 ? " · " + d.Reasons : "")}. Faça backup e confira a página Discos.",
                    });
            }
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Suggestion, Title = "Saúde dos discos", Detail = "Não foi possível ler agora: " + ex.Message });
        }

        try
        {
            var full = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady || d.TotalSize <= 0) continue;
                double free = (double)d.AvailableFreeSpace / d.TotalSize;
                if (free < 0.10)
                    full.Add($"{d.Name} com {d.AvailableFreeSpace / (double)(1024L * 1024L * 1024L):0.0} GB livres de " +
                             $"{d.TotalSize / (double)(1024L * 1024L * 1024L):0.0} GB ({free:P0})");
            }
            if (full.Count > 0)
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Warning,
                    Title = "Pouco espaço em disco",
                    Detail = string.Join("; ", full) + ". Libere espaço ou use a limpeza de cache para o Windows respirar.",
                });
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Warning, Title = "Espaço em disco", Detail = "Não foi possível ler: " + ex.Message });
        }

        // 4) Dispositivos com problema (drivers).
        Step(0.58, "Procurando dispositivos com problema…");
        try
        {
            var problems = DriverManager.ProblemDevices();
            if (problems.Count == 0)
                report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Ok, Title = "Dispositivos", Detail = "Todos os dispositivos estão com driver instalado." });
            else
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = $"{problems.Count} dispositivo(s) com problema",
                    Detail = string.Join("; ", problems.Take(3).Select(p => $"{p.Name} ({p.Reason})")) +
                             (problems.Count > 3 ? "…" : "") + " Use ATUALIZAR DRIVERS ou o site do fabricante.",
                });
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Warning, Title = "Dispositivos", Detail = "Não foi possível verificar: " + ex.Message });
        }

        // 5) Inicialização do Windows.
        Step(0.68, "Conferindo os programas de inicialização…");
        try
        {
            var list = StartupManager.List();
            int on = list.Count(e => e.Enabled);
            int missing = list.Count(e => e.FileMissing);
            if (missing > 0)
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Inicialização com entradas quebradas",
                    Detail = $"{missing} programa(s) de inicialização apontam para arquivos que não existem mais. " +
                             "Abra Inicialização e remova essas entradas.",
                });
            if (on > 8)
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Muitos programas abrem com o Windows",
                    Detail = $"{on} programas ativados. Cada um atrasa o login e consome memória: desative em Inicialização o que não usa todo dia.",
                });
            if (missing == 0 && on <= 8)
                report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Ok, Title = "Inicialização", Detail = $"{on} programa(s) ativados — dentro do normal." });
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Warning, Title = "Inicialização", Detail = "Não foi possível verificar: " + ex.Message });
        }

        // 6) O que está rodando sem janela / parado.
        Step(0.78, "Vendo o que está rodando em segundo plano…");
        try
        {
            var apps = Optimizer.ListApps();
            var background = apps.Where(a => a.IsBackground || a.IsIdle).ToList();
            long memory = background.Sum(a => a.MemoryBytes);
            if (background.Count >= 4)
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Programas em segundo plano",
                    Detail = $"{background.Count} programas sem janela ou parados usam {memory / Mb:N0} MB. " +
                             "Use MODO JOGO ou FECHAR 2º PLANO antes de jogar.",
                });
            else
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Ok,
                    Title = "Segundo plano",
                    Detail = background.Count == 0 ? "Nenhum programa extra rodando sem janela." : $"Só {background.Count} programa(s) em segundo plano.",
                });
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Warning, Title = "Segundo plano", Detail = "Não foi possível verificar: " + ex.Message });
        }

        // 7) Sistema de arquivos (chkdsk /scan, só leitura e só com administrador).
        if (includeFileSystem)
        {
            Step(0.88, "Verificando o sistema de arquivos…");
            if (SensorMonitor.IsElevated) CheckFileSystem(report, progress);
        }

        // 8) Reinício pendente de atualizações.
        Step(0.96, "Conferindo atualizações pendentes…");
        try
        {
            if (RebootPending())
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Reinicie o PC",
                    Detail = "Há atualizações ou instalações esperando um reinício. Reiniciar conclui o serviço e evita lentidão.",
                });
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Suggestion, Title = "Reinício pendente", Detail = "Não foi possível verificar: " + ex.Message });
        }

        if (!SensorMonitor.IsElevated)
            report.Items.Add(new PcCheckItem
            {
                Level = PcCheckLevel.Suggestion,
                Title = "Abrir como administrador",
                Detail = "Sem permissão de administrador, a limpeza do Windows e a verificação do sistema de arquivos ficaram limitadas.",
            });

        if (report.Items.Count == 0)
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Ok, Title = "Tudo verificado", Detail = "Nenhum problema encontrado." });

        // A lista final prioriza o que ainda pode ser otimizado.
        report.Items.Sort((a, b) => Rank(a.Level).CompareTo(Rank(b.Level)));
        Step(1, "Verificação concluída.");
        return report;
    }

    private static int Rank(PcCheckLevel level) => level switch
    {
        PcCheckLevel.Warning => 0,
        PcCheckLevel.Suggestion => 1,
        PcCheckLevel.Fixed => 2,
        _ => 3,
    };

    // ------------------------------------------------------------------ corrigir

    /// <summary>
    /// Aplica as otimizações seguras que a verificação encontrou: limpa cache/temporários (inclusive
    /// restos do Windows Update), devolve a memória em espera e remove entradas de inicialização que
    /// apontam para arquivos que não existem mais (com backup, restaurável em Inicialização).
    /// Não toca em documentos, na Lixeira nem em programas abertos.
    /// </summary>
    public static PcFixReport Fix(AppSettings settings, Action<string, double>? progress = null)
    {
        var fix = new PcFixReport();
        void Step(double fraction, string text) => progress?.Invoke(text, fraction);

        // 1) Cache e temporários: os itens seguros da verificação + caches de atualização.
        Step(0.05, "Limpando temporários e cache…");
        try
        {
            var keys = new HashSet<string>(AutoSafe, StringComparer.OrdinalIgnoreCase) { "wu", "do" };
            var targets = SystemCleaner.Targets().Where(t => keys.Contains(t.Key)).ToList();
            int total = Math.Max(1, targets.Count);
            int done = 0;
            // Sem Scan antes: o Clean já mede o que apaga (antes o Scan era jogado fora e o trabalho dobrava).
            var cleaned = SystemCleaner.Clean(targets, text =>
                Step(0.05 + 0.35 * Math.Min(1.0, ++done / (double)total), text));
            fix.FreedBytes += cleaned.FreedBytes;
            fix.CacheFreedBytes += cleaned.FreedBytes;
            fix.CacheKeptFiles += cleaned.Kept;
            fix.CacheKeptBytes += cleaned.KeptBytes;
            if (cleaned.FreedBytes > 0)
                fix.Done.Add($"{CleanTarget.Format(cleaned.FreedBytes)} de cache e temporários liberados");
            else
                fix.Skipped.Add("cache: não havia nada apagável agora");
            if (cleaned.Kept > 0) fix.Skipped.Add($"{cleaned.Kept:N0} arquivo(s) em uso");
        }
        catch (Exception ex) { fix.Skipped.Add("cache: " + ex.Message); }

        // 2) Memória em espera (só devolve o que o Windows já pode reaproveitar).
        Step(0.45, "Devolvendo a memória em espera…");
        try
        {
            var (total, avail) = Optimizer.MemoryStatus();
            double used = total > 0 ? (double)(total - avail) / total : 0;
            var cleaned = Optimizer.CleanMemory();
            if (cleaned.FreedBytes > 0)
            {
                fix.FreedBytes += cleaned.FreedBytes;
                fix.MemoryFreedBytes += cleaned.FreedBytes;
                fix.Done.Add($"{cleaned.FreedBytes / Mb:N0} MB de RAM devolvidos (estava em {used:P0})");
            }
            else
            {
                fix.Skipped.Add("memória: nada em espera para liberar");
            }
            // O Windows só limpa a lista em espera/cache de arquivos com privilégio de administrador.
            if (!string.IsNullOrWhiteSpace(cleaned.StandbyError))
                fix.Skipped.Add("lista em espera: " + cleaned.StandbyError);
            if (!string.IsNullOrWhiteSpace(cleaned.FileCacheError))
                fix.Skipped.Add("cache de arquivos: " + cleaned.FileCacheError);
        }
        catch (Exception ex) { fix.Skipped.Add("memória: " + ex.Message); }

        // 3) Entradas de inicialização que apontam para arquivos inexistentes.
        Step(0.72, "Removendo entradas quebradas da inicialização…");
        try
        {
            var broken = StartupManager.List().Where(e => e.FileMissing).ToList();
            int needAdmin = 0;
            foreach (var entry in broken)
            {
                if (entry.Machine && !SensorMonitor.IsElevated) { needAdmin++; continue; }
                try
                {
                    var backup = StartupManager.Remove(entry);
                    settings.StartupRemoved.Add(backup);
                    fix.RemovedStartup++;
                    // Salva a cada remoção: se algo falhar depois, o backup já está no disco.
                    try { settings.Save(); }
                    catch (Exception ex) { fix.Skipped.Add($"{entry.Name}: removido, mas o backup não pôde ser salvo ({ex.Message})"); }
                }
                catch (Exception ex) { fix.Skipped.Add($"{entry.Name}: {ex.Message}"); }
            }
            if (fix.RemovedStartup > 0)
                fix.Done.Add($"{fix.RemovedStartup} entrada(s) quebrada(s) de inicialização removidas " +
                             "(dá para restaurar na página Inicialização)");
            if (needAdmin > 0)
                fix.Skipped.Add($"{needAdmin} entrada(s) de inicialização de todos os usuários precisam de administrador");
        }
        catch (Exception ex) { fix.Skipped.Add("inicialização: " + ex.Message); }

        Step(1, "Correções aplicadas.");
        return fix;
    }

    /// <summary>
    /// Marca na lista o que o CORRIGIR realmente mudou. Sem isso a verificação seguinte mostra os
    /// mesmos títulos e o usuário fica com a impressão de que nada aconteceu — mesmo quando centenas
    /// de MB saíram do disco ou a RAM melhorou. O texto é sempre o antes → depois medido, sem inventar.
    /// </summary>
    public static void ApplyFixResult(PcCheckReport after, PcCheckReport? before, PcFixReport fix)
    {
        static bool IsCache(string title) => title.StartsWith("Temporários e cache", StringComparison.OrdinalIgnoreCase);
        static bool IsMemory(string title) => title.StartsWith("Memória RAM", StringComparison.OrdinalIgnoreCase);

        // --- Cache e temporários -------------------------------------------------
        int cacheIndex = after.Items.FindIndex(x => IsCache(x.Title));
        long cacheWas = before?.Items.FirstOrDefault(x => IsCache(x.Title))?.Bytes ?? 0;
        long cacheNow = cacheIndex >= 0 ? after.Items[cacheIndex].Bytes : 0;
        if (cacheIndex >= 0 && fix.CacheFreedBytes > 0)
        {
            string text = cacheWas > 0
                ? $"{CleanTarget.Format(fix.CacheFreedBytes)} liberados: eram {CleanTarget.Format(cacheWas)}, " +
                  $"agora são {CleanTarget.Format(cacheNow)}."
                : $"{CleanTarget.Format(fix.CacheFreedBytes)} liberados.";
            if (fix.CacheKeptFiles > 0)
                text += $" {fix.CacheKeptFiles:N0} arquivo(s) ({CleanTarget.Format(fix.CacheKeptBytes)}) seguem em uso " +
                        "por programas abertos e saem quando eles fecharem.";
            after.Items[cacheIndex] = new PcCheckItem
            {
                Level = PcCheckLevel.Fixed,
                Title = "Temporários e cache",
                Detail = text,
                Bytes = cacheNow,
            };
        }
        else if (cacheIndex >= 0 &&
                 after.Items[cacheIndex].Level == PcCheckLevel.Suggestion &&
                 fix.CacheFreedBytes == 0 && fix.CacheKeptFiles > 0)
        {
            // Nada era apagável: o que sobrou está preso por programas abertos. Não é "otimização
            // pendente" — é o estado normal enquanto esses programas rodam.
            after.Items[cacheIndex] = new PcCheckItem
            {
                Level = PcCheckLevel.Ok,
                Title = "Temporários e cache",
                Detail = $"{CleanTarget.Format(cacheNow)} em {fix.CacheKeptFiles:N0} arquivo(s) abertos por outros " +
                         "programas — o Windows libera sozinho quando eles fecharem. Nada seguro para apagar agora.",
                Bytes = cacheNow,
            };
        }

        // --- Memória RAM ---------------------------------------------------------
        int memIndex = after.Items.FindIndex(x => IsMemory(x.Title));
        double memWas = before?.Items.FirstOrDefault(x => IsMemory(x.Title))?.Ratio ?? 0;
        double memNow = memIndex >= 0 ? after.Items[memIndex].Ratio : 0;
        if (fix.MemoryFreedBytes > 0 && (memIndex >= 0 || memWas > 0))
        {
            string text = memWas > 0
                ? $"Era {memWas:P0} em uso e agora está {memNow:P0}: {fix.MemoryFreedBytes / Mb:N0} MB devolvidos ao sistema."
                : $"{fix.MemoryFreedBytes / Mb:N0} MB devolvidos ao sistema.";
            text += memNow >= 0.75
                ? " Ainda está apertada: feche programas pesados ou use MODO JOGO antes de jogar."
                : " Sem aperto agora.";
            var item = new PcCheckItem
            {
                Level = PcCheckLevel.Fixed,
                Title = "Memória RAM",
                Detail = text,
                Ratio = memNow,
            };
            if (memIndex >= 0) after.Items[memIndex] = item;
            else after.Items.Add(item);
        }

        // --- Inicialização -------------------------------------------------------
        if (fix.RemovedStartup > 0)
            after.Items.Add(new PcCheckItem
            {
                Level = PcCheckLevel.Fixed,
                Title = "Inicialização",
                Detail = $"{fix.RemovedStartup} entrada(s) quebrada(s) removida(s) — apontavam para arquivos que não " +
                         "existem mais. Dá para restaurar na página Inicialização.",
            });

        after.Items.Sort((a, b) => Rank(a.Level).CompareTo(Rank(b.Level)));
    }

    /// <summary>
    /// O que o CORRIGIR não faz sozinho (e por quê). Serve para a tela explicar o que sobrou,
    /// em vez de o usuário achar que a correção "não terminou".
    /// </summary>
    public static List<string> ManualOnly(PcCheckReport report)
    {
        var list = new List<string>();
        foreach (var item in report.Items)
        {
            if (item.Level is not (PcCheckLevel.Suggestion or PcCheckLevel.Warning)) continue;
            string title = item.Title;
            // Memória e temporários já foram tentados pelo CORRIGIR: o resultado (inclusive o que
            // ficou em uso) aparece em "Ficou de fora", então não entram na lista de pendências.
            if (title.Contains("memória", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("temporários", StringComparison.OrdinalIgnoreCase))
                continue;
            if (title.Contains("disco", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("espaço", StringComparison.OrdinalIgnoreCase))
                list.Add($"{title} (backup/liberar espaço)");
            else if (title.Contains("dispositivo", StringComparison.OrdinalIgnoreCase))
                list.Add($"{title} (ATUALIZAR DRIVERS)");
            else if (title.Contains("inicialização", StringComparison.OrdinalIgnoreCase) &&
                     title.Contains("muitos", StringComparison.OrdinalIgnoreCase))
                list.Add($"{title} (página Inicialização)");
            else if (title.Contains("segundo plano", StringComparison.OrdinalIgnoreCase))
                list.Add($"{title} (MODO JOGO)");
            else if (title.Contains("arquivo", StringComparison.OrdinalIgnoreCase))
                list.Add($"{title} (OTIMIZA › MANUTENÇÃO)");
            else if (title.Contains("reinicie", StringComparison.OrdinalIgnoreCase))
                list.Add("reinicie o PC");
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ------------------------------------------------------------------ sistema de arquivos

    private static void CheckFileSystem(PcCheckReport report, Action<string, double>? progress = null)
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        string drive = root.TrimEnd('\\');
        try
        {
            var psi = new ProcessStartInfo("chkdsk.exe", $"{drive} /scan")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return;

            var readOut = p.StandardOutput.ReadToEndAsync();
            var readErr = p.StandardError.ReadToEndAsync();

            // Dá sinal de vida a cada segundo: sem isso a tela fica 3 minutos sem nenhuma mensagem.
            int seconds = 0;
            while (!p.WaitForExit(1000))
            {
                seconds++;
                progress?.Invoke($"Verificando o sistema de arquivos… ({seconds}s)", 0.88);
                if (seconds >= 180) break;
            }

            if (!p.HasExited)
            {
                try { p.Kill(true); } catch { }
                report.Items.Add(new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Sistema de arquivos",
                    Detail = "A verificação do disco demorou mais de 3 minutos e foi interrompida. Rode CHKDSK pela página Manutenção quando puder.",
                });
                return;
            }

            _ = readOut.Result; _ = readErr.Result;
            report.Items.Add(p.ExitCode switch
            {
                0 => new PcCheckItem
                {
                    Level = PcCheckLevel.Ok,
                    Title = "Sistema de arquivos",
                    Detail = "Nenhum erro encontrado no disco do Windows.",
                },
                1 or 2 or 3 => new PcCheckItem
                {
                    Level = PcCheckLevel.Warning,
                    Title = "Erros no sistema de arquivos",
                    Detail = "O Windows encontrou problemas no disco. Abra OTIMIZA › MANUTENÇÃO e rode CHKDSK (reparar) — " +
                             "no C: o reparo roda no próximo reinício.",
                },
                _ => new PcCheckItem
                {
                    Level = PcCheckLevel.Suggestion,
                    Title = "Sistema de arquivos",
                    Detail = $"A verificação terminou com o código {p.ExitCode}. Rode CHKDSK pela página Manutenção para ver o detalhe.",
                },
            });
        }
        catch (Exception ex)
        {
            report.Items.Add(new PcCheckItem { Level = PcCheckLevel.Suggestion, Title = "Sistema de arquivos", Detail = "Não foi possível verificar: " + ex.Message });
        }
    }

    private static bool RebootPending()
    {
        static bool KeyExists(RegistryKey hive, string path)
        {
            using var k = hive.OpenSubKey(path);
            return k != null;
        }

        if (KeyExists(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending")) return true;
        if (KeyExists(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) return true;

        using var session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
        return session?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 };
    }
}
