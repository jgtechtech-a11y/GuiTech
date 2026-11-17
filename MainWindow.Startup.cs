using System.Windows;
using System.Windows.Controls;

namespace GuiTech;

/// <summary>Aba Gui_Tech FERRAMENTAS · Inicialização: programas que abrem junto com o Windows.</summary>
public partial class MainWindow
{
    private void ScanStartup()
    {
        UpdateStartWithWindows();
        UpdateWeeklyReview();

        List<StartupEntry> list;
        try { list = StartupManager.List(); }
        catch (Exception ex) { StartupResult.Text = $"Não foi possível listar: {ex.Message}"; return; }

        StartupList.ItemsSource = list;
        int on = list.Count(e => e.Enabled);
        StartupSummary.Text = list.Count == 0 ? "NENHUM PROGRAMA NA INICIALIZAÇÃO"
                            : $"{list.Count} PROGRAMAS · {on} ATIVADOS · {list.Count - on} DESATIVADOS";
        BtnStartupRestore.IsEnabled = _settings.StartupRemoved.Count > 0;
        BtnStartupRestore.Content = _settings.StartupRemoved.Count > 0
            ? $"Restaurar removidos ({_settings.StartupRemoved.Count})" : "Restaurar removidos";
    }

    private void BtnStartupScan_Click(object sender, RoutedEventArgs e)
    {
        StartupResult.Text = "";
        ScanStartup();
    }

    // ---------------- Revisão semanal ----------------

    private void UpdateWeeklyReview()
    {
        var state = WeeklyCheckup.GetState();
        WeeklyReviewToggle.IsChecked = _settings.WeeklyReview && state.TaskExists;
        string last = state.LastRun != null ? $"Última revisão: {state.LastRun:dd/MM/yyyy HH:mm} · " : "";
        WeeklyReviewState.Text = state.TaskExists
            ? $"{last}tarefa ativa no Agendador do Windows (domingo, ~11:00). Resultado em revisao.txt."
            : last.Length > 0 ? $"{last}tarefa desligada." : "";
    }

    private void WeeklyReviewToggle_Click(object sender, RoutedEventArgs e)
    {
        bool on = WeeklyReviewToggle.IsChecked == true;
        if (!RequireAdmin())
        {
            WeeklyReviewToggle.IsChecked = false;
            return;
        }
        try
        {
            if (!WeeklyCheckup.SetEnabled(on))
            {
                WeeklyReviewState.Text = "O Windows recusou a criação da tarefa agendada (política do sistema?).";
                WeeklyReviewToggle.IsChecked = false;
                return;
            }
            _settings.WeeklyReview = on;
            _settings.Save();
            Toast("Revisão semanal", on ? "Ativada: domingo, por volta das 11:00." : "Desligada.", on ? "\uE9D2" : "\uED60");
        }
        catch (Exception ex)
        {
            WeeklyReviewState.Text = $"Falhou: {ex.Message}";
            WeeklyReviewToggle.IsChecked = false;
        }
        UpdateWeeklyReview();
    }

    /// <summary>Chamada quando a tarefa semanal sinaliza (Gui_Tech já aberto): roda a revisão em segundo plano.</summary>
    private bool _revisaoRunning;

    internal async void RevisaoFromTask()
    {
        if (_revisaoRunning) return;
        _revisaoRunning = true;
        try
        {
            string summary = await Task.Run(WeeklyCheckup.RunSilent);
            Toast("Revisão semanal concluída", summary, "\uE9D2");
        }
        catch (Exception ex)
        {
            Toast("Revisão semanal", "Falhou: " + ex.Message, "\uE783");
        }
        finally
        {
            _revisaoRunning = false;
            UpdateWeeklyReview();
        }
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: StartupEntry item } box) return;
        bool turnOn = box.IsChecked == true;
        if (item.Machine && !RequireAdmin()) { box.IsChecked = item.Enabled; return; }
        try
        {
            StartupManager.SetEnabled(item, turnOn);
            StartupResult.Text = $"{item.Name}: {(turnOn ? "ativado" : "desativado")}. Vale na próxima inicialização do Windows.";
        }
        catch (Exception ex) { StartupResult.Text = $"{item.Name}: falhou ({ex.Message})."; }
        ScanStartup();
    }

    private void StartupRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: StartupEntry item }) return;
        if (item.Machine && !RequireAdmin()) return;
        var r = MessageBox.Show($"Remover \"{item.Name}\" da inicialização do Windows?\n\n{item.Command}\n\n" +
                                "O programa continua instalado, só deixa de abrir sozinho. " +
                                "Dá para desfazer em \"Restaurar removidos\".\n\n" +
                                "Se só quiser testar, prefira desligar o interruptor.",
                                "Inicialização", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            _settings.StartupRemoved.Add(StartupManager.Remove(item));
            _settings.Save();
            StartupResult.Text = $"{item.Name} removido da inicialização.";
        }
        catch (Exception ex) { StartupResult.Text = $"{item.Name}: não foi possível remover ({ex.Message})."; }
        ScanStartup();
    }

    private void BtnStartupRestore_Click(object sender, RoutedEventArgs e)
    {
        var removed = _settings.StartupRemoved;
        if (removed.Count == 0) return;
        if (removed.Any(b => b.Machine) && !RequireAdmin()) return;
        string names = string.Join("\n", removed.Take(12).Select(b => $"  • {b.Name}"))
                       + (removed.Count > 12 ? $"\n  … e mais {removed.Count - 12}" : "");
        var r = MessageBox.Show($"Devolver {removed.Count} programas à inicialização do Windows?\n\n{names}",
                                "Inicialização", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        var failed = new List<string>();
        foreach (var b in removed.ToList())
        {
            try { StartupManager.Restore(b); removed.Remove(b); }
            catch (Exception ex) { failed.Add($"{b.Name} ({ex.Message})"); }
        }
        _settings.Save();
        StartupResult.Text = failed.Count == 0 ? "Programas restaurados." : $"Não restaurados: {string.Join("; ", failed)}";
        ScanStartup();
    }
}
