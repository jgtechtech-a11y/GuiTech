using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace GuiTech;

/// <summary>Uma linha do inventário de drivers instalados.</summary>
public sealed class DriverRow
{
    public string Device { get; init; } = "";
    public string Class { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string Version { get; init; } = "";
    public string Date { get; init; } = "";
    public bool Signed { get; init; }
    public string SignedText => Signed ? "assinado" : "sem assinatura";
}

/// <summary>Resultado da consulta de um driver de vídeo.</summary>
public sealed class GpuDriverStatus
{
    public string Vendor { get; set; } = "";       // NVIDIA / AMD / Intel / ""
    public string GpuName { get; set; } = "";
    public string Installed { get; set; } = "";    // versão mostrada pelo Windows (ex.: 546.01)
    public string InstalledAdrenalin { get; set; } = ""; // AMD: versão do próprio AMD Software (CNVersion)
    public string Latest { get; set; } = "";       // vazio se não foi possível consultar
    public string DownloadUrl { get; set; } = "";
    public string SizeText { get; set; } = "";
    public string OfficialPage { get; set; } = "";
    public string Message { get; set; } = "";
    /// <summary>Caminho do instalador oficial já baixado e verificado (vazio se ainda não baixou).</summary>
    public string DownloadedPath { get; set; } = "";

    public string DownloadedText => DownloadedPath.Length == 0
        ? ""
        : "Instalador baixado e com assinatura verificada — use INSTALAR AGORA.";

    /// <summary>Texto do instalador oficial mostrado no cartão (só informa; nada é baixado pelo app).</summary>
    public string DownloadText => DownloadUrl.Length == 0
        ? ""
        : (SizeText.Length > 0 ? $"Instalador oficial: {SizeText}" : "Instalador oficial disponível no site do fabricante.");

    /// <summary>O que mostrar como "instalado": para AMD, a versão do AMD Software (mesmo formato do site).</summary>
    public string InstalledText => InstalledAdrenalin.Length > 0 ? $"Adrenalin {InstalledAdrenalin}" : Installed;

    public string LatestText => Latest.Length == 0 ? "não consultado" : Latest;

    /// <summary>
    /// Só diz "há versão nova" quando as duas versões são do mesmo formato. Para AMD a comparação
    /// certa é contra a versão do AMD Software (CNVersion), não contra a versão do driver do Windows.
    /// </summary>
    public bool UpdateAvailable
    {
        get
        {
            if (Latest.Length == 0) return false;
            string have = Vendor switch
            {
                "NVIDIA" => Installed,
                "AMD" => InstalledAdrenalin,
                _ => "",
            };
            return have.Length > 0 && CompareVersions(Latest, have) > 0;
        }
    }

    /// <summary>Compara versões parte por parte ("26.10.2" &gt; "25.10.45.16"), não como número decimal.</summary>
    public static int CompareVersions(string a, string b)
    {
        static int[] Parts(string s) => s.Split(new[] { '.', ',', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p =>
            {
                string digits = new(p.TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
            }).ToArray();

        var x = Parts(a);
        var y = Parts(b);
        int len = Math.Max(x.Length, y.Length);
        for (int i = 0; i < len; i++)
        {
            int p = i < x.Length ? x[i] : 0;
            int q = i < y.Length ? y[i] : 0;
            if (p != q) return p.CompareTo(q);
        }
        return 0;
    }
}

/// <summary>Placa-mãe / fabricante e as páginas oficiais de suporte.</summary>
public sealed class BoardInfo
{
    public string Maker { get; set; } = "";
    public string Model { get; set; } = "";
    public string Bios { get; set; } = "";
    public string Brand { get; set; } = "";   // ASUS / MSI / Gigabyte / outro
    public string SupportUrl { get; set; } = "";
}

/// <summary>Um driver disponível no Windows Update.</summary>
public sealed class WuDriver
{
    public string Title { get; init; } = "";
    public long SizeBytes { get; init; }
    public string DriverClass { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    /// <summary>
    /// Falso para pacotes de extensão (ex.: "Canon Extension Driver Update"), que o Windows Update
    /// devolve na mesma consulta mas não são driver de dispositivo.
    /// </summary>
    public bool IsRealDriver { get; init; }
    public string SizeText => SizeBytes >= 1048576
        ? $"{SizeBytes / 1048576.0:F1} MB"
        : $"{Math.Max(1, SizeBytes / 1024.0):F0} KB";
    public object? Update { get; init; }
}

/// <summary>Resultado da busca no Windows Update, separando drivers de dispositivo do resto.</summary>
public sealed class WuSearchResult
{
    public List<WuDriver> Drivers { get; init; } = new();
    public int Ignored { get; init; }
    public string IgnoredSample { get; init; } = "";
}

/// <summary>Dispositivo que o Windows reconhece mas está sem driver ou com problema (Gerenciador de Dispositivos).</summary>
public sealed class DeviceProblem
{
    public string Name { get; init; } = "";
    public string DeviceClass { get; init; } = "";
    public int Code { get; init; }

    public string Reason => Code switch
    {
        1 => "não está configurado corretamente",
        10 => "não consegue iniciar",
        18 => "precisa ser reinstalado",
        19 => "registro do driver com problema",
        28 => "sem driver instalado",
        43 => "o Windows parou o dispositivo",
        45 => "não está conectado agora",
        _ => $"código {Code}",
    };
}

/// <summary>
/// Inventário, verificação de versão e instalação de drivers. Só NVIDIA tem API pública de versão e download direto;
/// AMD e os fabricantes de placa-mãe abrem a página oficial já com o modelo detectado. O Windows Update cobre o resto.
/// </summary>
public static class DriverManager
{
    private static readonly HttpClient Http = CreateClient();

    /// <summary>
    /// Falhas de leitura (WMI, registro) do último VERIFICAR TUDO. Antes elas eram engolidas por
    /// catch vazio e a tela mostrava listas vazias como se estivesse tudo certo.
    /// </summary>
    public static List<string> ReadErrors { get; } = new();

    private static void Note(string what, Exception ex) => ReadErrors.Add($"{what}: {ex.Message}");

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) GuiTech");
        return c;
    }

    // ------------------------------------------------------------------ WMI

    private static string? Str(ManagementBaseObject o, string p) => o[p]?.ToString()?.Trim();

    private static string WmiDate(string? s)
    {
        try { if (!string.IsNullOrEmpty(s)) return ManagementDateTimeConverter.ToDateTime(s).ToString("dd/MM/yyyy"); } catch { }
        return "—";
    }

    /// <summary>Drivers instalados (sem os genéricos da Microsoft, exceto vídeo, rede e áudio).</summary>
    public static List<DriverRow> Inventory()
    {
        var list = new List<DriverRow>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, IsSigned FROM Win32_PnPSignedDriver");
            foreach (ManagementObject o in s.Get())
            {
                string name = Str(o, "DeviceName") ?? "";
                if (name.Length == 0) continue;
                string cls = Str(o, "DeviceClass") ?? "";
                string maker = Str(o, "Manufacturer") ?? "";
                bool main = cls is "DISPLAY" or "NET" or "MEDIA" or "BLUETOOTH" or "HDC" or "SCSIADAPTER" or "USB"
                                 or "SYSTEM" or "DISKDRIVE" or "HIDCLASS" or "MONITOR" or "PRINTQUEUE" or "BIOS"
                                 or "BATTERY" or "IMAGE" or "CAMERA" or "PORTS" or "SMARTCARDREADER" or "SDHOST";
                // Fora os dispositivos genéricos do Windows ("(Standard system devices)", "PCI standard..."),
                // que só poluem a lista e não têm driver de fabricante para atualizar.
                bool generic = maker.StartsWith("(", StringComparison.Ordinal)
                            || maker.Contains("Standard", StringComparison.OrdinalIgnoreCase)
                            || maker.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("Standard ", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("PCI standard", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("Motherboard resources", StringComparison.OrdinalIgnoreCase);
                if (generic && cls is not ("DISPLAY" or "NET" or "MEDIA")) continue;
                if (name.StartsWith("WAN Miniport", StringComparison.OrdinalIgnoreCase)) continue; // pilha de rede do Windows
                if (!main) continue;
                list.Add(new DriverRow
                {
                    Device = name, Class = cls, Vendor = maker,
                    Version = Str(o, "DriverVersion") ?? "—",
                    Date = WmiDate(Str(o, "DriverDate")),
                    Signed = o["IsSigned"] is bool b && b
                });
                o.Dispose();
            }
        }
        catch (Exception ex) { Note("Não foi possível ler os drivers instalados", ex); }
        // Um mesmo driver aparece repetido quando vários dispositivos usam o mesmo pacote.
        return list.GroupBy(r => r.Device + "|" + r.Version)
                   .Select(g => g.First())
                   .OrderBy(r => r.Class).ThenBy(r => r.Device).ToList();
    }

    /// <summary>"31.0.15.4601" (NVIDIA) vira "546.01".</summary>
    public static string NvidiaFriendly(string wmiVersion)
    {
        var p = wmiVersion.Split('.');
        if (p.Length != 4) return wmiVersion;
        string digits = p[2] + p[3];
        if (digits.Length < 5) return wmiVersion;
        digits = digits[^5..];
        return digits[..3] + "." + digits[3..];
    }

    // ------------------------------------------------------------------ GPU

    public static List<GpuDriverStatus> ReadGpus()
    {
        var list = new List<GpuDriverStatus>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterCompatibility FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
            {
                string name = Str(o, "Name") ?? "";
                string maker = (Str(o, "AdapterCompatibility") ?? "") + " " + name;
                string ver = Str(o, "DriverVersion") ?? "";
                if (name.Length == 0 || name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)) continue;
                var g = new GpuDriverStatus { GpuName = name };
                if (maker.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    g.Vendor = "NVIDIA"; g.Installed = NvidiaFriendly(ver);
                    g.OfficialPage = "https://www.nvidia.com/Download/index.aspx";
                }
                else if (maker.Contains("AMD", StringComparison.OrdinalIgnoreCase) || maker.Contains("Radeon", StringComparison.OrdinalIgnoreCase) || maker.Contains("Advanced Micro", StringComparison.OrdinalIgnoreCase))
                {
                    g.Vendor = "AMD"; g.Installed = ver;
                    // A versão que o AMD Software mostra (25.10.45.16) é a que se compara com o site.
                    g.InstalledAdrenalin = ReadAmdVersion();
                    g.OfficialPage = "https://www.amd.com/en/support/download/drivers.html";
                }
                else if (maker.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                {
                    g.Vendor = "Intel"; g.Installed = ver;
                    g.OfficialPage = "https://www.intel.com/content/www/us/en/download-center/home.html";
                }
                else { g.Vendor = ""; g.Installed = ver; }
                list.Add(g);
                o.Dispose();
            }
        }
        catch (Exception ex) { Note("Não foi possível ler a placa de vídeo", ex); }
        return list;
    }

    /// <summary>Versão do AMD Software instalada (CNVersion). É o número que a AMD usa no site.</summary>
    public static string ReadAmdVersion()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var cn = baseKey.OpenSubKey(@"SOFTWARE\AMD\CN");
                string? v = (cn?.GetValue("CNVersion") ?? cn?.GetValue("DriverVersion")) as string;
                if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
            }
            catch { }
        }
        return "";
    }

    /// <summary>Consulta a versão mais recente na NVIDIA (mesmo serviço da página oficial de drivers).</summary>
    public static async Task CheckNvidiaAsync(GpuDriverStatus g, bool studio, CancellationToken ct)
    {
        g.Latest = ""; g.DownloadUrl = ""; g.SizeText = "";
        try
        {
            var (psid, pfid) = await ResolveNvidiaIdsAsync(g.GpuName, ct);
            string url = "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php" +
                         $"?func=DriverManualLookup&psid={psid}&pfid={pfid}&osID=57&languageCode=1033&beta=0&isWHQL=1&dch=1" +
                         $"&upCRD={(studio ? 1 : 0)}&qnf=0&sort1=1&numberOfResults=1";
            string json = await Http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            string? ver = null, dl = null, size = null;
            Find(doc.RootElement, ref ver, ref dl, ref size);
            if (string.IsNullOrEmpty(ver) || string.IsNullOrEmpty(dl))
            {
                g.Message = "A NVIDIA não devolveu um driver para esta placa. Use a página oficial.";
                return;
            }
            g.Latest = ver!.Trim();
            g.DownloadUrl = dl!.Replace("http://", "https://");
            g.SizeText = size ?? "";
            g.Message = g.Installed.Length == 0
                ? $"A NVIDIA oferece a versão {g.Latest}. Não foi possível ler a versão instalada."
                : g.UpdateAvailable
                    ? $"Há um driver mais novo: {g.Latest} (instalado: {g.Installed})."
                    : $"Você já está com a versão mais recente ({g.Installed}).";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { g.Message = "Não foi possível consultar a NVIDIA: " + ex.Message; }
    }


    /// <summary>
    /// AMD não tem API de versão: abre a página oficial do modelo (a URL segue um padrão) e procura o link direto do instalador.
    /// Se o site mudar ou bloquear, cai para abrir a página manualmente.
    /// </summary>
    public static async Task CheckAmdAsync(GpuDriverStatus g, CancellationToken ct)
    {
        g.Latest = ""; g.DownloadUrl = ""; g.SizeText = "";
        try
        {
            var m = Regex.Match(g.GpuName, @"RX\s*(\d{4})\s*(XTX|XT|GRE|M)?", RegexOptions.IgnoreCase);
            if (!m.Success) { g.Message = "AMD: modelo não reconhecido para consulta automática. Use a página oficial."; return; }
            int n = int.Parse(m.Groups[1].Value);
            string fam = $"radeon-rx-{n / 1000}000-series";
            string suffix = m.Groups[2].Success ? "-" + m.Groups[2].Value.ToLowerInvariant() : "";
            string page = $"https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/{fam}/amd-radeon-rx-{n}{suffix}.html";
            g.OfficialPage = page;
            using var req = new HttpRequestMessage(HttpMethod.Get, page);
            req.Headers.Referrer = new Uri("https://www.amd.com/");
            using var resp = await Http.SendAsync(req, ct);
            string html = await resp.Content.ReadAsStringAsync(ct);
            var hit = Regex.Match(html, @"https://drivers\.amd\.com/drivers/[^""'\s<>\\]*win11[^""'\s<>\\]*\.exe", RegexOptions.IgnoreCase);
            if (!hit.Success) hit = Regex.Match(html, @"https://drivers\.amd\.com/drivers/[^""'\s<>\\]*\.exe", RegexOptions.IgnoreCase);
            // WHQL é o instalador "recomendado". Beta/preview e instaladores mínimos só confundem.
            var whql = Regex.Matches(html, @"https://drivers\.amd\.com/drivers/[^""'\s<>\\]*\.exe", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .FirstOrDefault(x => x.Value.Contains("whql", StringComparison.OrdinalIgnoreCase)
                                     && !x.Value.Contains("beta", StringComparison.OrdinalIgnoreCase)
                                     && !x.Value.Contains("minimalsetup", StringComparison.OrdinalIgnoreCase));
            if (whql != null) hit = whql;
            if (!hit.Success) { g.Message = "AMD: o site não devolveu o link do instalador (pode estar protegido). Abra a página oficial do seu modelo."; return; }
            g.DownloadUrl = hit.Value;
            var v = Regex.Match(hit.Value, @"edition-([\d.]+)", RegexOptions.IgnoreCase);
            g.Latest = v.Success ? v.Groups[1].Value : Path.GetFileNameWithoutExtension(hit.Value);
            g.Message = g.InstalledAdrenalin.Length == 0
                ? $"AMD Software Adrenalin {g.Latest} disponível. Não foi possível ler a versão do AMD Software instalada " +
                  $"(o Windows mostra {g.Installed}); confira no próprio AMD Software."
                : g.UpdateAvailable
                    ? $"Há uma versão mais nova: Adrenalin {g.Latest} (instalada: {g.InstalledAdrenalin})."
                    : $"Você já está com a versão mais recente do Adrenalin ({g.InstalledAdrenalin}).";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { g.Message = "Não foi possível consultar a AMD: " + ex.Message; }
    }

    private static void Find(JsonElement e, ref string? ver, ref string? dl, ref string? size)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    if (p.NameEquals("Version") && ver == null) ver = p.Value.GetString();
                    else if (p.NameEquals("DownloadURL") && dl == null) dl = p.Value.GetString();
                    else if (p.NameEquals("DownloadURLFileSize") && size == null) size = p.Value.GetString();
                }
                else Find(p.Value, ref ver, ref dl, ref size);
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var i in e.EnumerateArray()) Find(i, ref ver, ref dl, ref size);
    }

    /// <summary>Descobre os códigos de série (psid) e produto (pfid) da placa na base de drivers da NVIDIA.</summary>
    private static async Task<(int psid, int pfid)> ResolveNvidiaIdsAsync(string gpuName, CancellationToken ct)
    {
        bool mobile = Regex.IsMatch(gpuName, @"Laptop|Max-Q|Mobile|Notebook", RegexOptions.IgnoreCase);
        var m = Regex.Match(gpuName, @"(RTX|GTX|GT)\s*(\d{3,4})", RegexOptions.IgnoreCase);
        if (!m.Success) throw new InvalidOperationException("Modelo de placa não reconhecido: " + gpuName);
        string prefix = m.Groups[1].Value.ToUpperInvariant();
        int n = int.Parse(m.Groups[2].Value);

        string series;
        if (prefix == "RTX") series = $"RTX {n / 100} Series";
        else if (n >= 1600 && n < 1700) series = "16 Series";
        else if (n >= 1000 && n < 1100) series = "10 Series";
        else if (n >= 900 && n < 1000) series = "900 Series";
        else if (n >= 700 && n < 800) series = "700 Series";
        else series = $"{n / 100 * 100} Series";
        if (prefix == "RTX" && n >= 2000) series = $"RTX {n / 100} Series";

        string xml = await Http.GetStringAsync("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=2&ParentID=1", ct);
        var seriesList = XDocument.Parse(xml).Descendants("LookupValue")
            .Select(x => (Name: (string?)x.Element("Name") ?? "", Value: (string?)x.Element("Value") ?? "")).ToList();
        var cand = seriesList.Where(s => s.Name.Contains(series, StringComparison.OrdinalIgnoreCase)
                                         && s.Name.Contains("Notebook", StringComparison.OrdinalIgnoreCase) == mobile).OrderBy(s => s.Name.Length).ToList();
        if (cand.Count == 0) throw new InvalidOperationException("Série da placa não encontrada na NVIDIA (" + series + ").");
        int psid = int.Parse(cand[0].Value);

        xml = await Http.GetStringAsync($"https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3&ParentID={psid}", ct);
        var prods = XDocument.Parse(xml).Descendants("LookupValue")
            .Select(x => (Name: (string?)x.Element("Name") ?? "", Value: (string?)x.Element("Value") ?? "")).ToList();
        if (prods.Count == 0) throw new InvalidOperationException("Nenhum produto listado para a série.");
        string key = Regex.Replace(gpuName, @"^NVIDIA\s+", "", RegexOptions.IgnoreCase).Replace("Laptop GPU", "").Trim();
        var exact = prods.FirstOrDefault(p => string.Equals(p.Name.Trim(), key, StringComparison.OrdinalIgnoreCase));
        var pick = exact.Value != null ? exact : prods.FirstOrDefault(p => p.Name.Contains(m.Value, StringComparison.OrdinalIgnoreCase));
        if (pick.Value == null) pick = prods[0]; // o driver é unificado por série: qualquer produto da série devolve o mesmo
        return (psid, int.Parse(pick.Value));
    }

    /// <summary>Cria um ponto de restauração antes de instalar um driver manualmente (se a Proteção do Sistema estiver ligada).</summary>
    public static async Task<string> CreateRestorePointAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -Command \"Checkpoint-Computer -Description 'Gui_Tech - antes do driver' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using var p = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await p.WaitForExitAsync(cts.Token);
            return p.ExitCode == 0 ? "Ponto de restauração criado." : "Ponto de restauração não criado (proteção do sistema desligada ou já criado hoje).";
        }
        catch { return "Ponto de restauração não criado."; }
    }

    // ------------------------------------------------------------------ Placa-mãe

    public static BoardInfo ReadBoard()
    {
        var b = new BoardInfo();
        try
        {
            using (var s = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                foreach (ManagementObject o in s.Get()) { b.Maker = Str(o, "Manufacturer") ?? ""; b.Model = Str(o, "Product") ?? ""; break; }
            using (var s = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                foreach (ManagementObject o in s.Get()) { b.Bios = $"{Str(o, "SMBIOSBIOSVersion")} ({WmiDate(Str(o, "ReleaseDate"))})"; break; }
            string sys = "", sysModel = "";
            using (var s = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                foreach (ManagementObject o in s.Get()) { sys = Str(o, "Manufacturer") ?? ""; sysModel = Str(o, "Model") ?? ""; break; }

            string all = b.Maker + " " + sys;
            string q = Uri.EscapeDataString(b.Model.Length > 0 && !b.Model.Contains("Default", StringComparison.OrdinalIgnoreCase) ? b.Model : sysModel);
            if (all.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
            { b.Brand = "ASUS"; b.SupportUrl = "https://www.asus.com/support/download-center/"; }
            else if (all.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || all.Contains("MSI", StringComparison.OrdinalIgnoreCase))
            { b.Brand = "MSI"; b.SupportUrl = "https://www.msi.com/search/" + q; }
            else if (all.Contains("GIGABYTE", StringComparison.OrdinalIgnoreCase))
            { b.Brand = "Gigabyte"; b.SupportUrl = "https://www.gigabyte.com/Support"; }
            else if (all.Contains("ASRock", StringComparison.OrdinalIgnoreCase))
            { b.Brand = "ASRock"; b.SupportUrl = "https://www.asrock.com/support/index.asp"; }
            else if (all.Contains("Dell", StringComparison.OrdinalIgnoreCase)) { b.Brand = "Dell"; b.SupportUrl = "https://www.dell.com/support/home/"; }
            else if (all.Contains("LENOVO", StringComparison.OrdinalIgnoreCase)) { b.Brand = "Lenovo"; b.SupportUrl = "https://support.lenovo.com/"; }
            else if (all.Contains("HP", StringComparison.OrdinalIgnoreCase) || all.Contains("Hewlett", StringComparison.OrdinalIgnoreCase)) { b.Brand = "HP"; b.SupportUrl = "https://support.hp.com/"; }
            else if (all.Contains("Acer", StringComparison.OrdinalIgnoreCase)) { b.Brand = "Acer"; b.SupportUrl = "https://www.acer.com/support"; }
            if (b.Maker.Length == 0) b.Maker = sys;
            if (b.Model.Length == 0) b.Model = sysModel;
        }
        catch (Exception ex) { Note("Não foi possível ler a placa-mãe", ex); }
        return b;
    }

    // ------------------------------------------------------------------ Windows Update

    private static dynamic NewCom(string progId)
        => Activator.CreateInstance(Type.GetTypeFromProgID(progId) ?? throw new InvalidOperationException(
               "O Windows Update não está disponível. Confira se o serviço 'Windows Update' (wuauserv) está em execução."))!;

    // Uma única sessão para procurar, baixar e instalar: é o padrão documentado pela Microsoft.
    // Criar outra sessão na hora de instalar deixa os IUpdate "soltos" e rende SUS_E_NOT_INITIALIZED.
    private static object? _wuSession;

    private static dynamic WuSession()
    {
        if (_wuSession == null) _wuSession = NewCom("Microsoft.Update.Session");
        return _wuSession;
    }

    /// <summary>Encerra a sessão do Windows Update (usado ao fechar o aplicativo).</summary>
    public static void CloseWindowsUpdateSession()
    {
        try { if (_wuSession != null) Marshal.FinalReleaseComObject(_wuSession); } catch { }
        _wuSession = null;
    }

    // O catálogo do Windows Update também devolve "extensões" de aplicativo (DriverClass = Extension),
    // que não são driver de dispositivo — no PC de teste o único item era "Canon Extension Driver Update".
    private static readonly string[] DeviceDriverClasses =
    {
        "Display", "Net", "MEDIA", "HDC", "SCSIAdapter", "USB", "HIDClass", "DiskDrive", "Printer",
        "System", "Bluetooth", "Image", "Camera", "SmartCardReader", "SDHost", "Battery", "Mouse",
        "Keyboard", "Ports", "NetTrans", "Modem", "Monitor",
    };

    private static string TitleOf(dynamic u)
    {
        try { return (string)u.Title ?? ""; } catch { return ""; }
    }

    private static bool IsDeviceDriver(dynamic u, string title)
    {
        string cls = "";
        try { cls = (string)u.DriverClass ?? ""; } catch { }
        if (cls.Equals("Extension", StringComparison.OrdinalIgnoreCase)) return false;
        if (title.Contains("Extension", StringComparison.OrdinalIgnoreCase)) return false;
        // Classe desconhecida não é descartada: melhor mostrar do que esconder um driver de verdade.
        return cls.Length == 0 || DeviceDriverClasses.Contains(cls, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Procura drivers de dispositivo no catálogo oficial do Windows Update. Extensões de aplicativo
    /// ficam de fora e são contadas em <see cref="WuSearchResult.Ignored"/> para a tela explicar o motivo.
    /// </summary>
    public static Task<WuSearchResult> SearchWindowsUpdateAsync() => Task.Run(() =>
    {
        dynamic session = WuSession();
        dynamic searcher = session.CreateUpdateSearcher();
        // Catálogo da Microsoft, sem depender do padrão da máquina (que pode apontar para WSUS).
        try { searcher.ServerSelection = 2; } catch { }
        try { searcher.ServiceID = "7971f918-a847-4430-9279-4a52d1efe18d"; } catch { }

        dynamic result = searcher.Search("IsInstalled=0 and Type='Driver' and IsHidden=0");
        var all = new List<WuDriver>();
        foreach (dynamic u in result.Updates)
        {
            string title = TitleOf(u);
            string cls = ""; try { cls = (string)u.DriverClass ?? ""; } catch { }
            string maker = ""; try { maker = (string)u.DriverManufacturer ?? ""; } catch { }
            string model = ""; try { model = (string)u.DriverModel ?? ""; } catch { }
            long size = 0; try { size = Convert.ToInt64(u.MaxDownloadSize); } catch { }
            all.Add(new WuDriver
            {
                Title = title,
                SizeBytes = size,
                DriverClass = cls,
                Manufacturer = maker,
                Model = model,
                IsRealDriver = IsDeviceDriver(u, title),
                Update = u,
            });
        }

        var drivers = all.Where(d => d.IsRealDriver).ToList();
        var ignored = all.Where(d => !d.IsRealDriver).ToList();
        return new WuSearchResult
        {
            Drivers = drivers,
            Ignored = ignored.Count,
            IgnoredSample = ignored.Count > 0 ? ignored[0].Title : "",
        };
    });

    /// <summary>
    /// Baixa e instala de verdade e confere cada resultado. O código geral do Windows Update
    /// ("2 = sucesso") aparece mesmo quando nenhum driver foi instalado, então o texto final é
    /// montado a partir do resultado individual de cada pacote.
    /// </summary>
    public static Task<string> InstallWindowsUpdateAsync(List<WuDriver> items, Action<string> progress) => Task.Run(() =>
    {
        if (items.Count == 0) return "Nenhum driver de dispositivo para instalar.";

        dynamic session = WuSession();
        dynamic coll = NewCom("Microsoft.Update.UpdateColl");
        foreach (var d in items)
        {
            dynamic u = d.Update!;
            try { if (!(bool)u.EulaAccepted) u.AcceptEula(); } catch { }
            coll.Add(u);
        }

        progress($"Baixando {items.Count} driver(s) do Windows Update (pode levar alguns minutos)…");
        dynamic downloader = session.CreateUpdateDownloader();
        downloader.Updates = coll;
        dynamic dl = downloader.Download();
        int dlCode = 0;
        try { dlCode = (int)dl.ResultCode; } catch { }
        if (dlCode is not (2 or 3))
        {
            string hr = "";
            try { int h = (int)dl.HResult; if (h != 0) hr = $" (0x{h:X8})"; } catch { }
            return $"O download falhou antes de instalar (código {dlCode}{hr}). Nada foi alterado; tente de novo em alguns minutos.";
        }

        // Só manda instalar o que realmente baixou.
        dynamic ready = NewCom("Microsoft.Update.UpdateColl");
        var names = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            int rc = 3;
            try { rc = (int)dl.GetUpdateResult(i).ResultCode; } catch { }
            if (rc is 2 or 3) { ready.Add(items[i].Update!); names.Add(items[i].Title); }
        }
        if (names.Count == 0) return "O Windows Update não conseguiu baixar nenhum dos drivers. Nada foi instalado.";

        progress($"Instalando {names.Count} driver(s)…");
        dynamic installer = session.CreateUpdateInstaller();
        installer.Updates = ready;
        try { installer.ForceQuiet = true; } catch { }
        dynamic res = installer.Install();

        int ok = 0;
        var failed = new List<string>();
        for (int i = 0; i < names.Count; i++)
        {
            int rc = 3, hr = 0;
            try
            {
                dynamic r = res.GetUpdateResult(i);
                rc = (int)r.ResultCode;
                hr = (int)r.HResult;
            }
            catch { }
            if (rc is 2 or 3) ok++;
            else failed.Add(names[i] + (hr != 0 ? $" (0x{hr:X8})" : ""));
        }

        string reboot = "";
        try { if ((bool)res.RebootRequired) reboot = " Reinicie o PC para finalizar."; } catch { }

        string text = ok == names.Count
            ? $"{ok} driver(s) instalados."
            : $"{ok} de {names.Count} driver(s) instalados.";
        if (failed.Count > 0)
            text += " Falhou: " + string.Join("; ", failed.Take(4)) + (failed.Count > 4 ? "…" : "") + ".";
        return text + reboot;
    });

    /// <summary>Dispositivos com problema no Gerenciador de Dispositivos (sem driver, erro etc.).</summary>
    public static List<DeviceProblem> ProblemDevices() => ProblemDevices(out _);

    /// <summary>
    /// Igual ao anterior, mas <paramref name="error"/> recebe o motivo quando a leitura falha —
    /// lista vazia por falha não pode ser exibida como "todos em dia".
    /// </summary>
    public static List<DeviceProblem> ProblemDevices(out string? error)
    {
        error = null;
        var list = new List<DeviceProblem>();
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
            foreach (ManagementObject o in s.Get())
            {
                int code = 0;
                try { code = Convert.ToInt32(o["ConfigManagerErrorCode"]); } catch { }
                if (code == 0) continue;
                list.Add(new DeviceProblem
                {
                    Name = Str(o, "Name") is { Length: > 0 } n ? n : "Dispositivo sem nome",
                    DeviceClass = Str(o, "PNPClass") ?? "",
                    Code = code,
                });
            }
        }
        catch (Exception ex) { error = ex.Message; }
        return list.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Pede ao Windows para procurar driver para tudo que está conectado (pnputil /scan-devices).</summary>
    public static Task<string> ScanDevicesAsync() => Task.Run(() =>
    {
        try
        {
            var psi = new ProcessStartInfo("pnputil.exe", "/scan-devices")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return "Não foi possível executar o pnputil.";
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(120_000)) { try { p.Kill(true); } catch { } return "A busca de drivers demorou demais e foi interrompida."; }
            return p.ExitCode == 0
                ? "O Windows procurou driver para os dispositivos conectados."
                : $"O Windows respondeu com código {p.ExitCode} ao procurar drivers.";
        }
        catch (Exception ex) { return "Falha ao procurar drivers: " + ex.Message; }
    });

    // ------------------------------------------------------------------ Backup

    /// <summary>Exporta todos os drivers de terceiros para uma pasta (pnputil). Serve para reinstalar depois de formatar.</summary>
    public static async Task<string> BackupAsync(Action<string> progress, CancellationToken ct)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                  "GuiTech-Drivers", DateTime.Now.ToString("yyyyMMdd-HHmm"));
        Directory.CreateDirectory(dir);
        var psi = new ProcessStartInfo("pnputil.exe", $"/export-driver * \"{dir}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        using var p = Process.Start(psi)!;
        int n = 0;
        string? line;
        while ((line = await p.StandardOutput.ReadLineAsync(ct)) != null)
            if (line.Contains(".inf", StringComparison.OrdinalIgnoreCase)) progress($"Exportando drivers… {++n}");
        await p.WaitForExitAsync(ct);
        int folders = Directory.GetDirectories(dir).Length;
        return folders > 0 ? $"{folders} drivers salvos em {dir}" : "Nada foi exportado (execute o programa como administrador).";
    }

    // ------------------------------------------------------------------ Instalador oficial (download)

    /// <summary>Resultado do download do instalador do fabricante.</summary>
    public sealed class InstallerDownload
    {
        public bool Ok { get; init; }
        public bool Canceled { get; init; }
        public string Path { get; init; } = "";
        public long SizeBytes { get; init; }
        public string Sha256 { get; init; } = "";
        public string Signer { get; init; } = "";
        public string Message { get; init; } = "";
    }

    // Só baixamos de servidor oficial do fabricante. Qualquer outro host é recusado.
    private static readonly string[] OfficialHosts =
    {
        "drivers.amd.com", "download.amd.com", "www.amd.com", "amd.com",
        "international.download.nvidia.com", "us.download.nvidia.com", "download.nvidia.com",
        "downloadmirror.nvidia.com", "www.nvidia.com",
        "downloadmirror.intel.com", "downloadcenter.intel.com", "www.intel.com",
    };

    // Cliente próprio: instalador de driver pode passar de 1 GB e o timeout padrão de 30 min cortaria o download.
    private static readonly HttpClient DownloadHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    private static string VendorRoot(string vendor) => vendor switch
    {
        "AMD" => "https://www.amd.com/",
        "NVIDIA" => "https://www.nvidia.com/",
        "Intel" => "https://www.intel.com/",
        _ => "https://www.amd.com/",
    };

    private static bool HasRoom(string dir, long needed)    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dir));
            if (string.IsNullOrEmpty(root)) return true;
            return new DriveInfo(root).AvailableFreeSpace > needed + 300L * 1024 * 1024;
        }
        catch { return true; }
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>Monta o pedido com cabeçalhos de navegador (CDNs recusam pedidos "crus").</summary>
    private static HttpRequestMessage InstallerRequest(Uri uri, GpuDriverStatus gpu)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, uri);
        // Alguns CDNs (AMD, por exemplo) devolvem uma página de erro se o pedido não parecer um
        // navegador vindo do site do fabricante. Sem isso o "instalador" vem com 2 KB de HTML.
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                                       "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        req.Headers.Accept.ParseAdd("*/*");
        string referer = gpu.OfficialPage.Length > 0 ? gpu.OfficialPage : VendorRoot(gpu.Vendor);
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refUri)) req.Headers.Referrer = refUri;
        return req;
    }

    /// <summary>
    /// Só descobre o tamanho do instalador (sem baixar), para o usuário decidir antes de gastar
    /// centenas de MB de internet. Também confirma que o link ainda é o arquivo, e não uma página.
    /// </summary>
    public static async Task<(bool Ok, long? Size, string? Error)> ProbeInstallerAsync(
        GpuDriverStatus gpu, CancellationToken ct)
    {
        if (!Uri.TryCreate(gpu.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return (false, null, "O link do instalador não é https. Download cancelado por segurança.");
        if (!OfficialHosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase)))
            return (false, null, $"O link não é de um servidor oficial ({uri.Host}). Download cancelado por segurança.");

        try
        {
            using var req = InstallerRequest(uri, gpu);
            using var resp = await DownloadHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                return (false, null, $"O site do fabricante respondeu {(int)resp.StatusCode}. Use ABRIR PÁGINA OFICIAL.");

            string media = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                media.Contains("json", StringComparison.OrdinalIgnoreCase))
                return (false, null, "O servidor do fabricante devolveu uma página em vez do instalador " +
                                     "(o CDN pode bloquear downloads automáticos). Use ABRIR PÁGINA OFICIAL.");

            return (true, resp.Content.Headers.ContentLength, null);
        }
        catch (OperationCanceledException) { return (false, null, "Consulta cancelada."); }
        catch (Exception ex) { return (false, null, "Não foi possível consultar o instalador: " + ex.Message); }
    }

    /// <summary>
    /// Baixa o instalador .exe direto do servidor oficial do fabricante, com progresso e cancelamento,
    /// e só aceita o arquivo se a assinatura digital da AMD/NVIDIA/Intel for válida.
    /// </summary>
    public static async Task<InstallerDownload> DownloadInstallerAsync(
        GpuDriverStatus gpu, IProgress<(long Received, long? Total)> progress, CancellationToken ct)
    {
        if (gpu.DownloadUrl.Length == 0)
            return new InstallerDownload { Message = "Não há link de instalador para esta placa. Use ABRIR PÁGINA OFICIAL." };
        if (!Uri.TryCreate(gpu.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new InstallerDownload { Message = "O link do instalador não é https. Download cancelado por segurança." };
        if (!OfficialHosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase)))
            return new InstallerDownload { Message = $"O link não é de um servidor oficial ({uri.Host}). Download cancelado por segurança." };

        string dir;
        try
        {
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GuiTech-Drivers", "Instaladores");
            Directory.CreateDirectory(dir);
            // Documents pode estar somente leitura (perfil corporativo): testa antes de baixar 1 GB.
            string probe = Path.Combine(dir, ".escrita");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
        }
        catch
        {
            try
            {
                dir = Path.Combine(Path.GetTempPath(), "GuiTech", "Instaladores");
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                return new InstallerDownload { Message = "Não há pasta para salvar o instalador: " + ex.Message };
            }
        }

        string name = Path.GetFileName(uri.LocalPath);
        if (name.Length < 5 || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = $"driver-{gpu.Vendor.ToLowerInvariant()}-{gpu.Latest}.exe";
        name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        string target = Path.Combine(dir, name);
        string part = target + ".part";
        long received = 0;

        try
        {
            try { if (File.Exists(part)) File.Delete(part); } catch { }

            using var req = InstallerRequest(uri, gpu);
            using var resp = await DownloadHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                return new InstallerDownload
                {
                    Message = $"O site do fabricante respondeu {(int)resp.StatusCode}. Tente de novo mais tarde ou use ABRIR PÁGINA OFICIAL.",
                };

            // Se o CDN devolveu página (bloqueio, aviso, página de erro), não insiste.
            string media = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                media.Contains("json", StringComparison.OrdinalIgnoreCase))
                return new InstallerDownload
                {
                    Message = "O servidor do fabricante devolveu uma página em vez do instalador " +
                              "(o CDN pode bloquear downloads automáticos). Use ABRIR PÁGINA OFICIAL.",
                };

            long? total = resp.Content.Headers.ContentLength;
            if (total is long need && !HasRoom(dir, need))
                return new InstallerDownload
                {
                    Message = $"Falta espaço em disco: o instalador tem {need / 1048576.0:N0} MB. Libere espaço e tente de novo.",
                };

            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.Asynchronous))
            {
                var buffer = new byte[1 << 20];
                int read;
                long last = 0;
                while ((read = await src.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    received += read;
                    if (received - last >= 4L * 1024 * 1024) { last = received; progress.Report((received, total)); }
                }
            }
            progress.Report((received, total ?? received));

            if (total is long expected && received < expected)
            {
                try { File.Delete(part); } catch { }
                return new InstallerDownload { Message = "O download parou antes do fim (conexão instável?). Tente de novo." };
            }
            if (received < 1024 * 1024)
            {
                try { File.Delete(part); } catch { }
                return new InstallerDownload { Message = "O arquivo baixado é pequeno demais para ser um instalador de driver. Nada foi mantido." };
            }

            if (File.Exists(target)) File.Delete(target);
            File.Move(part, target);
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(part)) File.Delete(part); } catch { }
            return new InstallerDownload { Canceled = true, Message = "Download cancelado. Nada ficou pela metade." };
        }
        catch (Exception ex)
        {
            try { if (File.Exists(part)) File.Delete(part); } catch { }
            return new InstallerDownload
            {
                Message = "Não foi possível baixar: " + (ex is HttpRequestException
                    ? "sem conexão com o site do fabricante (verifique a internet)."
                    : ex.Message),
            };
        }

        string sha = "";
        try { sha = Sha256Of(target); } catch { }

        var (valid, status) = VerifySignature(target);
        string signer = "";
        try
        {
            // SYSLIB0057: CreateFromSignedFile ficou marcado como obsoleto no .NET 10, mas continua
            // funcionando e não tem substituto direto (extrai o certificado de um .exe/.sys assinado).
            // Suprimido de propósito — trocar só se o .NET remover de fato.
#pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(target));
#pragma warning restore SYSLIB0057
            signer = cert.Subject;
        }
        catch { }

        if (!valid)
        {
            try { File.Delete(target); } catch { }
            return new InstallerDownload
            {
                Message = $"O arquivo baixado não passou na verificação de assinatura digital ({status}) e foi apagado " +
                          "por segurança. Use ABRIR PÁGINA OFICIAL para baixar direto do site do fabricante.",
            };
        }

        return new InstallerDownload
        {
            Ok = true,
            Path = target,
            SizeBytes = received,
            Sha256 = sha,
            Signer = signer,
            Message = $"{Path.GetFileName(target)} — {received / 1048576.0:N0} MB, {status}.",
        };
    }

    /// <summary>Abre o instalador baixado (o app já está elevado, então ele herda o administrador).</summary>
    public static string RunInstaller(string path, string vendor, bool quiet)
    {
        if (!File.Exists(path)) return "O instalador não está mais em " + path + ".";
        try
        {
            // Só AMD e NVIDIA têm instalação silenciosa confiável; nos demais abrimos o instalador normal.
            bool silent = quiet && vendor is "NVIDIA" or "AMD";
            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? "",
            };
            if (silent)
            {
                psi.Arguments = vendor switch
                {
                    "NVIDIA" => "-s -noreboot",
                    "AMD" => "-INSTALL",
                    _ => "",
                };
            }
            Process.Start(psi);
            return silent
                ? "Instalação silenciosa iniciada. A tela pode piscar e o PC pode reiniciar no fim — salve o que estiver aberto."
                : "Instalador aberto. Siga as telas do fabricante; não desligue o PC e salve o que estiver aberto.";
        }
        catch (Exception ex)
        {
            return "Não foi possível abrir o instalador: " + ex.Message;
        }
    }

    /// <summary>Procura o instalador desta placa já baixado em execuções anteriores.</summary>
    public static string? ExistingInstaller(GpuDriverStatus gpu)
    {
        try
        {
            if (!Uri.TryCreate(gpu.DownloadUrl, UriKind.Absolute, out var uri)) return null;
            string name = Path.GetFileName(uri.LocalPath);
            if (name.Length < 5) return null;
            name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                       "GuiTech-Drivers", "Instaladores", name);
            return File.Exists(path) ? path : null;
        }
        catch { return null; }
    }

    // --- assinatura digital (WinVerifyTrust): sem PowerShell, funciona com caminho acentuado e perfil restrito

    private static readonly Guid WintrustGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, IntPtr data);

    /// <summary>Confere a assinatura Authenticode do arquivo. Retorna false se não for válida.</summary>
    private static (bool Valid, string Status) VerifySignature(string path)
    {
        var action = WintrustGenericVerifyV2;
        IntPtr filePtr = IntPtr.Zero;
        IntPtr dataPtr = IntPtr.Zero;
        try
        {
            var file = new WintrustFileInfo
            {
                Size = (uint)Marshal.SizeOf<WintrustFileInfo>(),
                Path = path,
                FileHandle = IntPtr.Zero,
                KnownSubject = IntPtr.Zero,
            };
            filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
            Marshal.StructureToPtr(file, filePtr, false);

            var data = new WintrustData
            {
                Size = (uint)Marshal.SizeOf<WintrustData>(),
                UiChoice = 2,          // WTD_UI_NONE
                RevocationChecks = 0,  // WTD_REVOKE_NONE
                UnionChoice = 1,       // WTD_CHOICE_FILE
                File = filePtr,
                StateAction = 1,       // WTD_STATEACTION_VERIFY
                ProvFlags = 0x10,      // WTD_REVOCATION_CHECK_NONE
            };
            dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustData>());
            Marshal.StructureToPtr(data, dataPtr, false);

            int hr = WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

            var close = Marshal.PtrToStructure<WintrustData>(dataPtr);
            close.StateAction = 2; // WTD_STATEACTION_CLOSE
            Marshal.StructureToPtr(close, dataPtr, false);
            WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

            string status = hr switch
            {
                0 => "assinatura digital válida",
                unchecked((int)0x800B0100) => "arquivo sem assinatura digital",
                unchecked((int)0x800B0109) => "certificado não confiável neste PC",
                unchecked((int)0x800B0101) => "certificado expirado",
                _ => $"assinatura inválida (0x{hr:X8})",
            };
            return (hr == 0, status);
        }
        catch (Exception ex)
        {
            return (false, "não foi possível verificar a assinatura (" + ex.Message + ")");
        }
        finally
        {
            if (dataPtr != IntPtr.Zero) Marshal.FreeHGlobal(dataPtr);
            if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
        }
    }
}
