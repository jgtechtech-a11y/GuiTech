using System.Windows;

namespace GuiTech;

/// <summary>Bandeja do sistema e inicialização junto com o Windows.</summary>
public partial class MainWindow
{
    private TrayIcon? _tray;

    /// <summary>Inicia o Gui_Tech só na bandeja (usado pelo Windows com o argumento --tray).</summary>
    public void StartInTray()
    {
        InitTray();
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
        _timer.Stop(); // ocioso na bandeja: não fica lendo sensores sem a janela aberta
        // A janela fica criada, mas só aparece quando o usuário clicar no ícone.
    }

    /// <summary>
    /// Esconde a janela e deixa só o ícone na bandeja do sistema (perto do relógio).
    /// Usado pelo botão minimizar: o app continua rodando e volta ao clicar no ícone.
    /// </summary>
    public void MinimizeToTray()
    {
        InitTray();
        ShowInTaskbar = false;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Hide();
        _timer.Stop(); // ocioso na bandeja: não fica lendo sensores sem a janela aberta
    }

    private void InitTray()
    {
        if (_tray != null) return;
        _tray = new TrayIcon("Gui_Tech · clique para abrir");
        _tray.Open += ShowFromTray;
        _tray.Exit += ExitFromTray;
        Closed += (_, _) => { _tray?.Dispose(); _tray = null; };
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (!_timer.IsEnabled) _timer.Start();
        Activate();
        Topmost = true;
        Topmost = false; // garante que a janela venha para a frente mesmo com outro programa em foco
        Focus();
    }

    private void ExitFromTray()
    {
        try { Close(); } catch { }
        Application.Current?.Shutdown();
    }

    // ------------------------------------------------------------ iniciar com o Windows

    private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs e)
    {
        bool on = StartWithWindowsToggle.IsChecked == true;
        try
        {
            AutoStart.SetEnabled(on);
            _settings.StartWithWindows = on;
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Não foi possível alterar a inicialização do Windows:\n\n{ex.Message}",
                            "Gui_Tech · Inicialização", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        UpdateStartWithWindows();
    }

    private void UpdateStartWithWindows()
    {
        var state = AutoStart.GetState();
        StartWithWindowsToggle.IsChecked = state.Enabled;
        StartWithWindowsState.Text = !state.Enabled
            ? "Desligado · o Gui_Tech abre só quando você clica no atalho."
            : state.PointsToOtherExe
                ? "Ligado, mas apontando para outra cópia do programa. Desligue e ligue de novo para corrigir."
                : state.UsesTask
                    ? "Ligado · o Windows abre o Gui_Tech em segundo plano ao entrar, sem pedir confirmação."
                    : "Ligado pelo método alternativo (chave Run). Se não abrir ao iniciar, desligue e ligue esta opção de novo.";
    }
}
