using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GuiTech;

/// <summary>Capa e ícone de um jogo na biblioteca (a capa é montada fora da thread da janela).</summary>
public sealed partial class LibGame : INotifyPropertyChanged
{
    private ImageBrush? _art;
    private Brush? _placeholder;
    private ImageSource? _icon;

    /// <summary>Arte encontrada no próprio PC: capa do jogo, ou a imagem larga (header) como reserva.</summary>
    internal bool ArtIsFallback { get; set; }

    /// <summary>Fundo do cartão: a capa quando existe, senão um gradiente estável tirado do nome.</summary>
    public Brush Cover => (Brush?)_art ?? Placeholder;

    /// <summary>Ícone do .exe (usado quando a loja não guarda capa, como Epic e GOG).</summary>
    public ImageSource? Icon => _icon;

    public bool HasArt => _art != null;
    public bool ShowIcon => _art == null && _icon != null;
    public bool ShowInitials => _art == null && _icon == null;

    /// <summary>Duas letras do nome, quando não há capa nem ícone.</summary>
    public string Initials
    {
        get
        {
            string name = (Name ?? "").Trim();
            if (name.Length == 0) return "?";
            var words = name.Split(new[] { ' ', '-', ':', '_', '.', '\'' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return (char.ToUpperInvariant(words[0][0]).ToString() + char.ToUpperInvariant(words[1][0])).Trim();
            return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
        }
    }

    private Brush Placeholder => _placeholder ??= GameArt.Placeholder(Name);

    internal void SetArt(ImageBrush? art, bool fallback = false)
    {
        _art = art;
        ArtIsFallback = art != null && fallback;
        Changed(nameof(Cover));
        Changed(nameof(HasArt));
        Changed(nameof(ShowIcon));
        Changed(nameof(ShowInitials));
    }

    internal void SetIcon(ImageSource? icon)
    {
        _icon = icon;
        Changed(nameof(Icon));
        Changed(nameof(ShowIcon));
        Changed(nameof(ShowInitials));
    }

    private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Descobre a arte dos jogos: capas guardadas pela Steam (inclusive as personalizadas), ícone do .exe das outras lojas
/// e, quando o PC não tem a capa vertical, a capa oficial do CDN da Steam (guardada em %LOCALAPPDATA%\GuiTech\capas).
/// </summary>
public static class GameArt
{
    private static readonly string[] PortraitExts = { "p.png", "p.jpg", "p.jpeg", "p.webp", ".png", ".jpg", ".jpeg", ".webp" };

    public static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuiTech", "capas");

    /// <summary>Preenche o que já está no disco. Roda na thread de varredura (tudo é congelado antes de ir para a tela).</summary>
    public static void AssignLocal(IEnumerable<LibGame> games)
    {
        string? steam = null;
        try { steam = SteamInfo.SteamPath(); } catch { }

        foreach (var g in games)
        {
            try
            {
                if (g.IsSteam && steam != null)
                {
                    var (path, portrait) = SteamArt(g.Id, steam);

                    // 1) arte vertical que já está no PC (inclusive a personalizada pelo usuário)
                    if (path != null && portrait && LoadImage(path) is { } local)
                    {
                        g.SetArt(FrozenBrush(local));
                        continue;
                    }

                    // 2) capa baixada antes do CDN (vale até a Steam ter a arte local)
                    string cached = Path.Combine(CacheDir, "steam_" + g.Id + ".jpg");
                    if (File.Exists(cached) && LoadImage(cached) is { } c)
                    {
                        g.SetArt(FrozenBrush(c));
                        continue;
                    }

                    // 3) imagem larga (header) como reserva até a capa vertical chegar
                    if (path != null && LoadImage(path, 320) is { } wide)
                    {
                        g.SetArt(FrozenBrush(wide), fallback: true);
                        continue;
                    }
                }

                if (g.ExePath is { Length: > 0 } exe)
                    g.SetIcon(ExeIcon(exe));
            }
            catch { }
        }
    }

    /// <summary>O pincel nasce na thread de varredura: tem de estar congelado para a tela usá-lo.</summary>
    private static ImageBrush FrozenBrush(ImageSource image)
    {
        var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        brush.Freeze();
        return brush;
    }

    /// <summary>Busca no CDN da Steam as capas verticais que faltam (só jogos da Steam). Atualiza a lista conforme chegam.</summary>
    public static async Task FetchSteamCoversAsync(IReadOnlyList<LibGame> games, CancellationToken ct = default)
    {
        var pending = games.Where(g => g.IsSteam && (!g.HasArt || g.ArtIsFallback) && long.TryParse(g.Id, out _)).ToList();
        if (pending.Count == 0) return;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GuiTech/1.0 (+biblioteca local)");
        using var gate = new SemaphoreSlim(4);

        var tasks = pending.Select(async g =>
        {
            try
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
            }
            catch { return; }

            try
            {
                string file = Path.Combine(CacheDir, "steam_" + g.Id + ".jpg");
                if (!File.Exists(file))
                {
                    string url = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{g.Id}/library_600x900.jpg";
                    byte[] bytes = await http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                    if (bytes.Length < 1500) return;
                    Directory.CreateDirectory(CacheDir);
                    await File.WriteAllBytesAsync(file, bytes, ct).ConfigureAwait(false);
                }

                if (LoadImage(file) is { } bmp)
                    g.SetArt(FrozenBrush(bmp));
            }
            catch { }
            finally { gate.Release(); }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ arquivos locais

    private static (string? Path, bool Portrait) SteamArt(string id, string steam)
    {
        string cache = Path.Combine(steam, "appcache", "librarycache");

        // 1) arte personalizada do usuário (vertical, a preferida)
        try
        {
            string userdata = Path.Combine(steam, "userdata");
            if (Directory.Exists(userdata))
                foreach (var user in Directory.GetDirectories(userdata).OrderByDescending(SafeWrite))
                {
                    string grid = Path.Combine(user, "config", "grid");
                    foreach (var ext in PortraitExts)
                    {
                        string f = Path.Combine(grid, id + ext);
                        if (File.Exists(f)) return (f, true);
                    }
                }
        }
        catch { }

        // 2) capa vertical que a própria Steam baixou
        foreach (var f in new[]
                 {
                     Path.Combine(cache, id, "library_600x900.jpg"),
                     Path.Combine(cache, id + "_library_600x900.jpg"),
                     Path.Combine(cache, id, "library_600x900_2x.jpg"),
                     Path.Combine(cache, id + "_library_600x900_2x.jpg"),
                 })
            if (File.Exists(f)) return (f, true);

        // 3) imagem larga (header): serve de reserva até a capa vertical chegar
        foreach (var f in new[]
                 {
                     Path.Combine(cache, id, "header.jpg"),
                     Path.Combine(cache, id + "_header.jpg"),
                     Path.Combine(cache, id, "library_header.jpg"),
                 })
            if (File.Exists(f)) return (f, false);

        return (null, false);
    }

    private static DateTime SafeWrite(string dir)
    {
        try { return Directory.GetLastWriteTimeUtc(dir); } catch { return DateTime.MinValue; }
    }

    private static BitmapImage? LoadImage(string path, int decodeWidth = 208)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.StreamSource = fs;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------ ícone do .exe

    private static ImageSource? ExeIcon(string exe)
    {
        IntPtr hicon = IntPtr.Zero;
        try
        {
            var info = new SHFILEINFO();
            IntPtr ok = SHGetFileInfo(exe, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_SYSICONINDEX);
            if (ok == IntPtr.Zero) return null;

            var iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"); // IID_IImageList
            if (SHGetImageList(SHIL_JUMBO, ref iid, out IntPtr listPtr) == 0 && listPtr != IntPtr.Zero)
            {
                var list = (IImageList)Marshal.GetObjectForIUnknown(listPtr);
                Marshal.Release(listPtr);
                if (list.GetIcon(info.iIcon, ILD_TRANSPARENT, out hicon) != 0) hicon = IntPtr.Zero;
            }

            if (hicon == IntPtr.Zero) // reserva: ícone grande (32 px)
            {
                var info2 = new SHFILEINFO();
                SHGetFileInfo(exe, 0, ref info2, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
                hicon = info2.hIcon;
            }
            if (hicon == IntPtr.Zero) return null;

            var src = Imaging.CreateBitmapSourceFromHIcon(hicon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
        finally { if (hicon != IntPtr.Zero) DestroyIcon(hicon); }
    }

    // ------------------------------------------------------------------ gradiente do cartão

    /// <summary>Gradiente estável (mesmo jogo, mesma cor) em tons frios, para não competir com o acento teal.</summary>
    public static Brush Placeholder(string name)
    {
        uint h = 2166136261;
        foreach (char c in name ?? "") { h ^= c; h *= 16777619; }
        double hue = 188 + h % 52;                 // 188..240: ciano → azul frio (longe do roxo)
        double sat = 0.26 + (h >> 8) % 12 / 100.0;  // 0.26..0.37
        var brush = new LinearGradientBrush(Hsl(hue, sat, 0.31), Hsl(hue + 12, sat + 0.05, 0.13), 40);
        brush.Freeze();
        return brush;
    }

    private static Color Hsl(double h, double s, double l)
    {
        h = (h % 360 + 360) % 360 / 360.0;
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        static double Channel(double t, double p, double q)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        byte r = (byte)Math.Round(Channel(h + 1.0 / 3, p, q) * 255);
        byte g = (byte)Math.Round(Channel(h, p, q) * 255);
        byte b = (byte)Math.Round(Channel(h - 1.0 / 3, p, q) * 255);
        return Color.FromRgb(r, g, b);
    }

    // ------------------------------------------------------------------ Win32

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_SYSICONINDEX = 0x4000;
    private const int SHIL_JUMBO = 0x4;
    private const uint ILD_TRANSPARENT = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, out int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, uint flags, out IntPtr picon);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
