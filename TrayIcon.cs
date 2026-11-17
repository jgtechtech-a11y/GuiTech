using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace GuiTech;

/// <summary>
/// Ícone do Gui_Tech na bandeja do sistema (perto do relógio), feito direto com Shell_NotifyIcon.
/// Clique abre a janela; botão direito mostra "Abrir Gui_Tech" e "Sair".
/// Usado quando o programa é minimizado ou iniciado pelo Windows com <c>--tray</c>.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int CallbackMessage = 0x8000 + 1; // WM_APP + 1
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    private const int NIM_ADD = 0x0;
    private const int NIM_DELETE = 0x2;
    private const int NIF_MESSAGE = 0x1;
    private const int NIF_ICON = 0x2;
    private const int NIF_TIP = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    private readonly HwndSource _source;
    private readonly ContextMenu _menu;
    private readonly IntPtr _icon;
    private readonly string _tip;
    private readonly int _id = 0x4754; // "GT"
    private bool _added;
    private bool _disposed;
    private DispatcherTimer? _retry;

    /// <summary>Clique no ícone (ou "Abrir Gui_Tech" no menu).</summary>
    public event Action? Open;

    /// <summary>"Sair" no menu do ícone.</summary>
    public event Action? Exit;

    public TrayIcon(string tooltip)
    {
        var parameters = new HwndSourceParameters("GuiTechTray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(Hook);

        _menu = new ContextMenu { Placement = PlacementMode.MousePoint, StaysOpen = false };
        var open = new MenuItem { Header = "Abrir Gui_Tech" };
        open.Click += (_, _) => Open?.Invoke();
        var exit = new MenuItem { Header = "Sair" };
        exit.Click += (_, _) => Exit?.Invoke();
        _menu.Items.Add(open);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(exit);

        _icon = LoadAppIcon();
        _tip = tooltip.Length > 120 ? tooltip[..120] : tooltip;
        _added = AddIcon();

        // No logon o Explorer pode ainda não estar pronto para receber o ícone.
        // Tenta de novo por até ~60 s em vez de ficar sem bandeja para sempre.
        if (!_added) StartRetry();
    }

    private bool AddIcon()
    {
        var data = Data();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        data.szTip = _tip;
        return Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private void StartRetry()
    {
        int tries = 0;
        _retry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _retry.Tick += (_, _) =>
        {
            if (_disposed) { _retry?.Stop(); return; }
            if (AddIcon()) { _added = true; _retry?.Stop(); return; }
            if (++tries >= 20) _retry?.Stop();
        };
        _retry.Start();
    }

    private NotifyIconData Data() => new()
    {
        cbSize = Marshal.SizeOf<NotifyIconData>(),
        hWnd = _source.Handle,
        uID = _id,
    };

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            int evt = lParam.ToInt32() & 0xFFFF;
            if (evt is WM_LBUTTONUP or WM_LBUTTONDBLCLK) { Open?.Invoke(); handled = true; }
            else if (evt is WM_RBUTTONUP or WM_CONTEXTMENU) { ShowMenu(); handled = true; }
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        // Sem foco na janela, o menu não fecharia ao clicar fora.
        SetForegroundWindow(_source.Handle);
        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    private static IntPtr LoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && ExtractIconEx(exe, 0, out var large, out var small, 1) > 0)
                return large != IntPtr.Zero ? large : small != IntPtr.Zero ? small : LoadIcon(IntPtr.Zero, (IntPtr)32512);
        }
        catch { }
        return LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _retry?.Stop(); } catch { }
        try
        {
            if (_added)
            {
                var data = Data();
                Shell_NotifyIcon(NIM_DELETE, ref data);
            }
        }
        catch { }
        if (_icon != IntPtr.Zero) { try { DestroyIcon(_icon); } catch { } }
        try { _source.RemoveHook(Hook); _source.Dispose(); } catch { }
        try { _menu.IsOpen = false; } catch { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(int message, ref NotifyIconData data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);

    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
}
