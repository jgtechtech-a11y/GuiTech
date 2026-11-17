using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GuiTech;

/// <summary>
/// Página Gui_Tech OTIMIZA, parte 2: segundo plano/inativos, limpeza de cache e manutenção do Windows.
/// </summary>
public partial class MainWindow
{
    private List<CleanTarget> _clean = new();
    private CancellationTokenSource? _maintCts;
    private string _maintDrive = "C:";

    // ------------------------------------------------------------ segundo plano

    private void BtnMarkSuggested_Click(object sender, RoutedEventArgs e) => Mark(a => a.Suggested);

    private void BtnMarkBackground_Click(object sender, RoutedEventArgs e) => Mark(a => a.IsBackground || a.IsIdle);

    private void BtnMarkNone_Click(object sender, RoutedEventArgs e) => Mark(_ => false);

    private void Mark(Func<BackgroundApp, bool> rule)
    {
        foreach (var app in _apps) app.Selected = rule(app);
    }

    /// <summary>Fecha de uma vez tudo que está sem janela ou parado, sem passar pelo botão principal.</summary>
    private async void BtnCloseBackground_Click(object sender, RoutedEventArgs e)
    {
        var targets = _apps.Where(a => a.IsBackground || a.IsIdle).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Nenhum programa em segundo plano ou inativo encontrado agora.",
                            "Gui_Tech OTIMIZA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string list = string.Join("\n", targets.Take(12).Select(a => $"  • {a.Title}"));
        if (targets.Count > 12) list += $"\n  … e mais {targets.Count - 12}";
        var answer = MessageBox.Show(this,
            $"Fechar {targets.Count} programa(s) em segundo plano ou inativos?\n\n{list}\n\n" +
            "Sem janela, o processo é encerrado na hora. Com janela, o programa recebe o pedido de fechar " +
            "(como clicar no X) e pode pedir para salvar. Continuar?",
            "Gui_Tech OTIMIZA", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        BtnOptimize.IsEnabled = false;
        BtnOptimize.Content = "Fechando…";
        OptResult.Text = "Fechando programas em segundo plano…";
        var closed = await Task.Run(() => Optimizer.CloseApps(targets));
        var mem = await Task.Run(Optimizer.CleanMemory);
        BtnOptimize.IsEnabled = true;
        BtnOptimize.Content = "Gui_Tech OTIMIZA";

        var parts = new List<string>();
        if (closed.Closed.Count > 0) parts.Add($"fechados: {string.Join(", ", closed.Closed)}");
        if (closed.NotClosed.Count > 0) parts.Add($"não fecharam (podem ter algo não salvo): {string.Join(", ", closed.NotClosed)}");
        parts.Add($"memória liberada agora: {mem.FreedBytes / 1048576.0:N0} MB");
        OptResult.Text = string.Join(" · ", parts) + ".";

        RefreshApps();
        UpdateRamStatus();
    }

    // ------------------------------------------------------------ limpeza de cache

    private void InitMaintenance()
    {
        if (CleanList.ItemsSource == null)
        {
            _clean = SystemCleaner.Targets();
            CleanList.ItemsSource = _clean;
            CleanResult.Text = "Toque em ANALISAR para medir o que dá para liberar (nada é apagado sem você mandar).";
        }
        if (MaintDrives.Children.Count == 0) BuildDrives();
        if (MaintLog.Text.Length == 0) MaintLog.Text = "O log do reparo aparece aqui.";
    }

    private void BuildDrives()
    {
        var drives = SystemRepair.FixedDrives();
        if (drives.Count == 0) drives.Add("C:");

        bool first = true;
        foreach (var letter in drives)
        {
            var chip = new RadioButton
            {
                Style = (Style)FindResource("Chip"),
                Content = letter,
                GroupName = "MaintDrive",
                Tag = letter,
                IsChecked = first,
                Margin = new Thickness(0, 0, 6, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            string value = letter;
            chip.Checked += (_, _) => _maintDrive = value;
            if (first) _maintDrive = letter;
            MaintDrives.Children.Add(chip);
            first = false;
        }
    }

    private async void BtnCacheScan_Click(object sender, RoutedEventArgs e)
    {
        BtnCacheScan.IsEnabled = false;
        BtnCacheClean.IsEnabled = false;
        var targets = _clean.ToList();
        int done = 0;
        var progress = new Progress<int>(n => CleanResult.Text = $"Analisando… {n} de {targets.Count}");
        CleanResult.Text = "Analisando…";

        await Task.Run(() => SystemCleaner.Scan(targets, () =>
        {
            int n = Interlocked.Increment(ref done);
            ((IProgress<int>)progress).Report(n);
        }));

        BtnCacheScan.IsEnabled = true;
        BtnCacheClean.IsEnabled = true;

        long all = targets.Sum(t => t.Bytes);
        long marked = targets.Where(t => t.Selected).Sum(t => t.Bytes);
        CleanResult.Text = all == 0
            ? "Nada para limpar agora: os caches já estão vazios."
            : $"Encontrado {CleanTarget.Format(all)} no total. Os itens marcados somam {CleanTarget.Format(marked)}.";
    }

    private async void BtnCacheClean_Click(object sender, RoutedEventArgs e)
    {
        var targets = _clean.Where(t => t.Selected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Marque pelo menos um item para limpar.", "Gui_Tech OTIMIZA",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (targets.Any(t => !t.Scanned))
        {
            BtnCacheScan_Click(sender, e);
            MessageBox.Show(this, "A análise ainda não terminou. Confira os tamanhos e toque em LIMPAR MARCADOS de novo.",
                            "Gui_Tech OTIMIZA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long total = targets.Sum(t => t.Bytes);
        string names = string.Join("\n", targets.Select(t => $"  • {t.Name} — {CleanTarget.Format(t.Bytes)}"));
        var answer = MessageBox.Show(this,
            $"Apagar {CleanTarget.Format(total)} de cache?\n\n{names}\n\n" +
            "Nada de documentos ou programas é tocado. O que estiver em uso por algum programa fica para depois. " +
            (targets.Any(t => t.RecycleBin) ? "\n\nA LIXEIRA será esvaziada e não dá para recuperar o que está nela." : ""),
            "Gui_Tech OTIMIZA", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        BtnCacheScan.IsEnabled = false;
        BtnCacheClean.IsEnabled = false;
        BtnCacheClean.Content = "Limpando…";
        CleanResult.Text = "Limpando…";

        var progress = new Progress<string>(s => CleanResult.Text = s);
        var report = await Task.Run(() => SystemCleaner.Clean(targets, s => ((IProgress<string>)progress).Report(s)));

        foreach (var t in targets) { t.Bytes = 0; t.Files = 0; t.Status = ""; }

        BtnCacheScan.IsEnabled = true;
        BtnCacheClean.IsEnabled = true;
        BtnCacheClean.Content = "Limpar marcados";

        var parts = new List<string> { $"Liberado {CleanTarget.Format(report.FreedBytes)} ({report.Deleted:N0} arquivos apagados)" };
        if (report.Kept > 0) parts.Add($"{report.Kept:N0} arquivos ficaram por estarem em uso");
        CleanResult.Text = string.Join(" · ", parts) + ". Toque em ANALISAR para conferir.";
    }

    // ------------------------------------------------------------ manutenção

    private async void BtnSfc_Click(object sender, RoutedEventArgs e) =>
        await RunRepair("SFC · arquivos do Windows", SystemRepair.ScriptSfc());

    private async void BtnDismScan_Click(object sender, RoutedEventArgs e) =>
        await RunRepair("DISM · verificar imagem", SystemRepair.ScriptDismScan(false));

    private async void BtnDismRepair_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "O DISM vai usar o Windows Update para baixar os arquivos de reparo que faltam na imagem do Windows.\n\n" +
            "Pode levar de 5 a 30 minutos e precisa de internet. Continuar?",
            "Gui_Tech · Manutenção", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        await RunRepair("DISM · reparar imagem", SystemRepair.ScriptDismScan(true));
    }

    private async void BtnChkdskScan_Click(object sender, RoutedEventArgs e) =>
        await RunRepair($"CHKDSK {_maintDrive}: · verificar disco", SystemRepair.ScriptChkdsk(_maintDrive, false));

    private async void BtnChkdskFix_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var answer = MessageBox.Show(this,
            $"Reparar a unidade {_maintDrive}:?\n\n" +
            "Se a unidade estiver em uso pelo Windows (caso do C:), o reparo é agendado e roda no próximo " +
            "reinício — o PC fica alguns minutos na tela preta antes de ligar. Nas outras unidades pode rodar agora.\n\n" +
            "Continuar?",
            "Gui_Tech · Manutenção", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        await RunRepair($"CHKDSK {_maintDrive}: · reparar", SystemRepair.ScriptChkdsk(_maintDrive, true));
    }

    private void BtnMaintCancel_Click(object sender, RoutedEventArgs e)
    {
        try { _maintCts?.Cancel(); } catch { }
        MaintState.Text = "cancelando…";
    }

    private async Task RunRepair(string title, string script)
    {
        if (!RequireAdmin()) return;
        if (_maintCts != null)
        {
            MessageBox.Show(this, "Já existe uma manutenção rodando. Cancele ou espere terminar.",
                            "Gui_Tech · Manutenção", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _maintCts = new CancellationTokenSource();
        SetMaintRunning(true);
        MaintLog.Clear();
        MaintState.Text = "iniciando…";
        MaintLine($"=== {title} ===");

        var progress = new Progress<string>(line =>
        {
            MaintLine(line);
            if (SystemRepair.Percent(line) is double pct) MaintState.Text = $"{pct:0}%";
        });

        var result = await SystemRepair.RunAsync(script, progress, _maintCts.Token);

        _maintCts.Dispose();
        _maintCts = null;
        SetMaintRunning(false);

        if (result.Cancelled)
        {
            MaintState.Text = "cancelado";
            MaintLine("=== Cancelado pelo usuário (o Windows pode ter ficado no meio do reparo; rode de novo). ===");
        }
        else if (result.Error != null)
        {
            MaintState.Text = "erro";
            MaintLine($"=== Não foi possível executar: {result.Error} ===");
        }
        else
        {
            MaintState.Text = "concluído";
            MaintLine($"=== Concluído (código {result.ExitCode}). ===");
            MaintLine(Interpret(result.ExitCode));
        }
    }

    /// <summary>Explica os códigos de saída das ferramentas em uma frase.</summary>
    private static string Interpret(int code) => code switch
    {
        0 => "Nenhum erro encontrado ou tudo foi corrigido.",
        1 => "Foram encontrados e corrigidos problemas.",
        2 => "Há problemas que não puderam ser corrigidos sozinhos; pode ser preciso reparar a imagem (DISM) ou ver o log.",
        _ => "Veja as mensagens acima para o resultado.",
    };

    private void SetMaintRunning(bool running)
    {
        BtnSfc.IsEnabled = !running;
        BtnDismScan.IsEnabled = !running;
        BtnDismRepair.IsEnabled = !running;
        BtnChkdskScan.IsEnabled = !running;
        BtnChkdskFix.IsEnabled = !running;
        BtnMaintCancel.IsEnabled = running;
    }

    private void MaintLine(string line)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => MaintLine(line)); return; }
        MaintLog.AppendText(line + Environment.NewLine);

        // Log muito grande deixaria a janela lenta: guarda as últimas linhas.
        string text = MaintLog.Text;
        if (text.Length > 200_000)
        {
            int cut = text.IndexOf('\n', text.Length - 120_000);
            if (cut > 0) MaintLog.Text = text[(cut + 1)..];
        }
        MaintLog.ScrollToEnd();
    }
}
