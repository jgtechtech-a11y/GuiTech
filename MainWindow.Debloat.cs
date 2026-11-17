using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace GuiTech;

/// <summary>Aba Gui_Tech DEBLOAT: catálogo de ajustes do Windows com estado, aplicação e desfazer.</summary>
public partial class MainWindow
{
    private List<DebloatItem> _debloat = new();
    private bool _debloatInited, _debloatBusy;

    private void InitDebloat()
    {
        if (_debloatInited) return;
        _debloatInited = true;

        _debloat = DebloatCatalog.All();
        BuildDebloatUi();
        RefreshDebloatStates();

        DebloatElevation.Visibility = SensorMonitor.IsElevated ? Visibility.Collapsed : Visibility.Visible;
        if (!SensorMonitor.IsElevated)
            DebloatElevation.Text = "Sem permissão de administrador: os ajustes de políticas, serviços, tarefas agendadas e apps " +
                                    "pré-instalados vão falhar. Feche e abra o Gui_Tech aceitando o pedido de administrador.";

        // A contagem de apps instalados usa PowerShell e demora: chega depois, sem travar a tela.
        _ = Task.Run(DebloatCatalog.RefreshBloatCount)
            .ContinueWith(_ => Dispatcher.BeginInvoke(new Action(RefreshDebloatStates)), TaskScheduler.Default);
    }

    private void BuildDebloatUi()
    {
        DebloatPanel.Children.Clear();
        var template = (DataTemplate)PageDebloat.FindResource("DebloatRow");
        var cardStyle = (Style)FindResource("CardBorder");
        var labelStyle = (Style)FindResource("Label");

        foreach (var group in _debloat.GroupBy(i => i.Category))
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = group.Key.ToUpperInvariant(), Style = labelStyle });
            stack.Children.Add(new ItemsControl
            {
                ItemsSource = group.ToList(),
                ItemTemplate = template,
                Focusable = false,
            });

            DebloatPanel.Children.Add(new Border { Style = cardStyle, Margin = new Thickness(0, 0, 0, 14), Child = stack });
        }
    }

    /// <summary>Relê o estado de cada item no Windows e atualiza o contador.</summary>
    private void RefreshDebloatStates()
    {
        if (_debloat.Count == 0) return;
        foreach (var item in _debloat) item.Refresh();
        UpdateDebloatSummary();
    }

    private void UpdateDebloatSummary()
    {
        if (!_debloatInited) return;
        int total = _debloat.Count;
        int applied = _debloat.Count(i => i.IsApplied);
        int marked = _debloat.Count(i => i.IsChecked && !i.IsApplied);

        DebloatCount.Text = $"{applied} de {total} aplicados";
        DebloatBar.ScaleX = total == 0 ? 0 : (double)applied / total;
        BtnDebloatApply.Content = marked > 0 ? $"Aplicar selecionados ({marked})" : "Aplicar selecionados";
        BtnDebloatApply.IsEnabled = marked > 0 && !_debloatBusy;
    }

    private void DebloatCheck_Click(object sender, RoutedEventArgs e) => UpdateDebloatSummary();

    private void BtnDebloatAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _debloat)
            if (item.CanSelect) item.IsChecked = true;
        UpdateDebloatSummary();
    }

    private void BtnDebloatNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _debloat)
            if (item.CanSelect) item.IsChecked = false;
        UpdateDebloatSummary();
    }

    private async void BtnDebloatApply_Click(object sender, RoutedEventArgs e)
    {
        if (_debloatBusy) return;
        var selected = _debloat.Where(i => i.IsChecked && !i.IsApplied).ToList();
        if (selected.Count == 0) { DebloatStatus.Text = "Nada marcado para aplicar."; return; }

        var ask = MessageBox.Show(this,
            $"Aplicar {selected.Count} ajuste(s) no Windows?\n\n" +
            "As mudanças valem na hora (algumas só após reiniciar o Explorer ou o PC). Cada item tem DESFAZER.\n" +
            "Os apps pré-instalados removidos não voltam por aqui — só pela Microsoft Store.",
            "Gui_Tech DEBLOAT", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes) return;

        _debloatBusy = true;
        UpdateDebloatSummary();
        int ok = 0;
        var errors = new List<string>();

        try
        {
            foreach (var item in selected)
            {
                DebloatStatus.Text = $"Aplicando: {item.Title}…";
                string? err = await Task.Run(item.ApplyNow);
                item.Refresh();
                if (err == null && item.IsApplied) ok++;
                else errors.Add($"{item.Title}: {err ?? "o Windows não confirmou a mudança"}");
            }
        }
        finally
        {
            _debloatBusy = false;
            UpdateDebloatSummary();
        }

        DebloatStatus.Text = errors.Count == 0
            ? $"{ok} ajuste(s) aplicado(s)."
            : $"{ok} aplicado(s), {errors.Count} com problema: {string.Join(" · ", errors.Take(4))}";

        if (ok > 0 && selected.Any(i => i.IsApplied && i.NeedsExplorer))
        {
            var restart = MessageBox.Show(this,
                "Alguns ajustes são da barra de tarefas e do menu Iniciar. Reiniciar o Explorer agora para vê-los?\n\n" +
                "As janelas do Explorador de Arquivos abertas serão fechadas (nada é perdido).",
                "Gui_Tech DEBLOAT", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (restart == MessageBoxResult.Yes) await RestartExplorerAsync();
        }
    }

    private async void BtnDebloatUndoAll_Click(object sender, RoutedEventArgs e)
    {
        if (_debloatBusy) return;
        var applied = _debloat.Where(i => i.IsApplied && i.CanUndo).ToList();
        if (applied.Count == 0) { DebloatStatus.Text = "Nada aplicado para desfazer."; return; }

        var ask = MessageBox.Show(this, $"Voltar {applied.Count} ajuste(s) ao padrão do Windows?",
            "Gui_Tech DEBLOAT", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes) return;

        _debloatBusy = true;
        UpdateDebloatSummary();
        try
        {
            foreach (var item in applied)
            {
                DebloatStatus.Text = $"Desfazendo: {item.Title}…";
                await Task.Run(item.UndoNow);
                item.Refresh();
            }
        }
        finally
        {
            _debloatBusy = false;
            UpdateDebloatSummary();
        }
        DebloatStatus.Text = $"{applied.Count} ajuste(s) voltaram ao padrão do Windows.";
    }

    private async void DebloatUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_debloatBusy || sender is not FrameworkElement { DataContext: DebloatItem item }) return;
        _debloatBusy = true;
        UpdateDebloatSummary();
        try
        {
            DebloatStatus.Text = $"Desfazendo: {item.Title}…";
            await Task.Run(item.UndoNow);
            item.Refresh();
            DebloatStatus.Text = $"“{item.Title}” voltou ao padrão do Windows.";
        }
        finally
        {
            _debloatBusy = false;
            UpdateDebloatSummary();
        }
    }

    private async void BtnDebloatRestore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        b.IsEnabled = false;
        DebloatStatus.Text = "Criando ponto de restauração (pode levar até um minuto)…";
        try
        {
            string msg = await DriverManager.CreateRestorePointAsync();
            DebloatStatus.Text = msg;
        }
        finally { b.IsEnabled = true; }
    }

    private async void BtnDebloatExplorer_Click(object sender, RoutedEventArgs e)
    {
        var ask = MessageBox.Show(this,
            "Reiniciar o Explorer? As janelas de pasta abertas serão fechadas (nada é perdido).",
            "Gui_Tech DEBLOAT", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ask == MessageBoxResult.Yes) await RestartExplorerAsync();
    }

    private async Task RestartExplorerAsync()
    {
        DebloatStatus.Text = "Reiniciando o Explorer…";
        await Task.Run(() =>
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
                try { p.Kill(); } catch { }
            Thread.Sleep(800);
            try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
        });
        DebloatStatus.Text = "Explorer reiniciado.";
    }
}
