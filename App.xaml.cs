using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace GuiTech;

public partial class App : Application
{
    // Uma única instância: abrir o Gui_Tech de novo só traz a janela que já está aberta para a frente.
    private static Mutex? _single;

    /// <summary>Pedido de "mostre a janela" quando outra instância é aberta (inclusive vindo da bandeja).</summary>
    private static EventWaitHandle? _show;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Uma exceção não tratada em qualquer handler async derrubava o app sem nenhuma mensagem.
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                MessageBox.Show(
                    "O Gui_Tech encontrou um problema nesta ação:\n\n" + args.Exception.Message +
                    "\n\nO aplicativo continua aberto. Se o erro se repetir, feche e abra o Gui_Tech de novo.",
                    "Gui_Tech", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { }
            args.Handled = true;
        };

        // --tray = aberto pela inicialização do Windows: fica só na bandeja até o usuário pedir.
        bool tray = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                                     a.Equals("/tray", StringComparison.OrdinalIgnoreCase));
        // --revisao = aberto pela tarefa da revisão semanal: roda o VERIFICAR PC em silêncio e sai.
        bool revisao = e.Args.Any(a => a.Equals("--revisao", StringComparison.OrdinalIgnoreCase) ||
                                       a.Equals("/revisao", StringComparison.OrdinalIgnoreCase));
        // --teste = cópia de teste rodando ao lado da instalada: canais próprios (mutex e aviso de janela),
        // para não confundir com a versão oficial que esteja aberta.
        bool teste = e.Args.Any(a => a.Equals("--teste", StringComparison.OrdinalIgnoreCase) ||
                                     a.Equals("/teste", StringComparison.OrdinalIgnoreCase));
        string instance = teste ? ".Teste" : "";

        bool first;
        try { _show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GuiTech.Show" + instance); } catch { }
        try { _single = new Mutex(true, @"Local\GuiTech.SingleInstance" + instance, out first); }
        catch (UnauthorizedAccessException) { first = false; } // criado por outra instância com outro nível de permissão

        if (!first)
        {
            try { _show?.Set(); } catch { }
            if (revisao)
            {
                // O Gui_Tech já está aberto: avisa a instância que está rodando para fazer a revisão.
                try { EventWaitHandle.OpenExisting(@"Local\GuiTech.Revisao").Set(); } catch { }
            }
            IntPtr h = teste ? IntPtr.Zero : FindWindow(null, "Gui_Tech");
            if (h != IntPtr.Zero)
            {
                if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
                SetForegroundWindow(h);
            }
            Shutdown();
            return;
        }
        base.OnStartup(e);

        // Sem interface nesta instância: a revisão roda e o programa fecha.
        if (revisao)
        {
            try
            {
                string summary = WeeklyCheckup.RunSilent();
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppSettings.Folder, "revisao.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {summary}\n"); } catch { }
            }
            catch { }
            Shutdown();
            return;
        }

        // Splash: aparece já, para a espera de 20–40 s (extração do exe + detecção de hardware)
        // não parecer o programa travado. Força um quadro de render antes do construtor pesado.
        var splash = new SplashWindow();
        splash.Show();
        splash.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));

        var window = new MainWindow();
        if (teste) window.Title += " · TESTE";
        try
        {
            if (tray) window.StartInTray();
            else window.Show();
        }
        finally
        {
            try { splash.Close(); } catch { }
        }

        // A chave Run antiga não inicia apps elevados de forma confiável no Windows 11.
        // Converte para tarefa agendada no logon sem travar a abertura da janela.
        Task.Run(AutoStart.MigrateIfNeeded);

        // Outra instância aberta depois desta só precisa pedir para mostrar a janela (o mutex já evita duas cópias).
        var show = _show;
        if (show != null)
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (!show.WaitOne()) continue;
                        window.Dispatcher.BeginInvoke(new Action(window.ShowFromTray));
                    }
                    catch { return; }
                }
            }) { IsBackground = true };
            thread.Start();
        }

        // Tarefa da revisão semanal com o Gui_Tech já aberto: o sinal da outra instância dispara a revisão aqui dentro.
        EventWaitHandle? revisaoSignal = null;
        try { revisaoSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GuiTech.Revisao"); } catch { }
        if (revisaoSignal != null)
        {
            var wt = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (!revisaoSignal.WaitOne()) continue;
                        window.Dispatcher.BeginInvoke(new Action(window.RevisaoFromTask));
                    }
                    catch { return; }
                }
            }) { IsBackground = true };
            wt.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DriverManager.CloseWindowsUpdateSession();
        base.OnExit(e);
    }
}
