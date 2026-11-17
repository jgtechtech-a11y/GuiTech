namespace GuiTech;

/// <summary>Especificações de referência que o hardware não reporta (como o banco de dados do GPU-Z).</summary>
public record GpuSpec(
    string Chip, string Arch, string Process, int DieMm2, double TransistorsBi,
    int Shaders, int Rops, int Tmus, string MemType, int BaseMHz, int BoostMHz, int TdpW,
    string Release, string? NameMatch = null,
    /// <summary>Nome comercial (ex.: "GeForce RTX 5090"). Vazio = usa o Chip.</summary>
    string Model = "",
    /// <summary>Memória de vídeo de fábrica (GB). 0 = usa a memória do sistema (iGPU).</summary>
    int VramGb = 0,
    /// <summary>Largura de barramento da memória (bits).</summary>
    int MemBusBit = 0,
    /// <summary>Velocidade efetiva da memória (Gbps).</summary>
    double MemSpeedGbps = 0)
{
    /// <summary>Largura de banda teórica da memória (GB/s), calculada do barramento e da velocidade.</summary>
    public double BandwidthGBs => MemSpeedGbps * MemBusBit / 8.0;

    /// <summary>Desempenho FP32 teórico no clock de boost (TFLOPS), calculado: shaders × 2 × MHz.</summary>
    public double Tflops => Shaders * 2.0 * BoostMHz / 1_000_000.0;

    /// <summary>Nome para mostrar na interface.</summary>
    public string DisplayName => Model.Length > 0 ? Model : Chip;
}

public static class GpuSpecDb
{
    /// <summary>Chave: "VVVV:DDDD" (vendor:device PCI). Mesma ID com variantes → NameMatch desempata.</summary>
    private static readonly Dictionary<string, GpuSpec[]> Db = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- NVIDIA Blackwell (RTX 50) ----
        ["10DE:2B85"] = new[] { new GpuSpec("GB202", "Blackwell", "TSMC 4N (5 nm)", 750, 92.2, 21760, 176, 680, "GDDR7", 2017, 2407, 575, "30/01/2025", Model: "GeForce RTX 5090", VramGb: 32, MemBusBit: 512, MemSpeedGbps: 28) },
        ["10DE:2C02"] = new[] { new GpuSpec("GB203", "Blackwell", "TSMC 4N (5 nm)", 378, 45.6, 10752, 112, 336, "GDDR7", 2295, 2617, 360, "30/01/2025", Model: "GeForce RTX 5080", VramGb: 16, MemBusBit: 256, MemSpeedGbps: 30) },
        ["10DE:2C05"] = new[] { new GpuSpec("GB203", "Blackwell", "TSMC 4N (5 nm)", 378, 45.6, 8960, 96, 280, "GDDR7", 2295, 2452, 300, "20/02/2025", Model: "GeForce RTX 5070 Ti", VramGb: 16, MemBusBit: 256, MemSpeedGbps: 28) },
        ["10DE:2F04"] = new[] { new GpuSpec("GB205", "Blackwell", "TSMC 4N (5 nm)", 263, 31.1, 6144, 80, 192, "GDDR7", 2325, 2512, 250, "05/03/2025", Model: "GeForce RTX 5070", VramGb: 12, MemBusBit: 192, MemSpeedGbps: 28) },
        ["10DE:2D04"] = new[] { new GpuSpec("GB206", "Blackwell", "TSMC 4N (5 nm)", 181, 21.9, 4608, 48, 144, "GDDR7", 2407, 2572, 180, "16/04/2025", Model: "GeForce RTX 5060 Ti (8 GB)", VramGb: 8, MemBusBit: 128, MemSpeedGbps: 28) },
        ["10DE:2D05"] = new[] { new GpuSpec("GB206", "Blackwell", "TSMC 4N (5 nm)", 181, 21.9, 3840, 48, 120, "GDDR7", 2280, 2497, 145, "19/05/2025", Model: "GeForce RTX 5060", VramGb: 8, MemBusBit: 128, MemSpeedGbps: 28) },

        // ---- NVIDIA Ada Lovelace (RTX 40) ----
        ["10DE:2684"] = new[] { new GpuSpec("AD102", "Ada Lovelace", "TSMC 4N (5 nm)", 609, 76.3, 16384, 176, 512, "GDDR6X", 2235, 2520, 450, "12/10/2022", Model: "GeForce RTX 4090", VramGb: 24, MemBusBit: 384, MemSpeedGbps: 21) },
        ["10DE:2702"] = new[] { new GpuSpec("AD103", "Ada Lovelace", "TSMC 4N (5 nm)", 379, 45.9, 10240, 112, 320, "GDDR6X", 2295, 2550, 320, "31/01/2024", Model: "GeForce RTX 4080 SUPER", VramGb: 16, MemBusBit: 256, MemSpeedGbps: 23) },
        ["10DE:2704"] = new[] { new GpuSpec("AD103", "Ada Lovelace", "TSMC 4N (5 nm)", 379, 45.9, 9728, 112, 304, "GDDR6X", 2205, 2505, 320, "16/11/2022", Model: "GeForce RTX 4080", VramGb: 16, MemBusBit: 256, MemSpeedGbps: 22.4) },
        ["10DE:2705"] = new[] { new GpuSpec("AD103", "Ada Lovelace", "TSMC 4N (5 nm)", 379, 45.9, 8448, 96, 264, "GDDR6X", 2340, 2610, 285, "24/01/2024", Model: "GeForce RTX 4070 Ti SUPER", VramGb: 16, MemBusBit: 256, MemSpeedGbps: 21) },
        ["10DE:2782"] = new[] { new GpuSpec("AD104", "Ada Lovelace", "TSMC 4N (5 nm)", 295, 35.8, 7680, 80, 240, "GDDR6X", 2310, 2610, 285, "05/01/2023", Model: "GeForce RTX 4070 Ti", VramGb: 12, MemBusBit: 192, MemSpeedGbps: 21) },
        ["10DE:2783"] = new[] { new GpuSpec("AD104", "Ada Lovelace", "TSMC 4N (5 nm)", 295, 35.8, 7168, 80, 224, "GDDR6X", 1980, 2475, 220, "17/01/2024", Model: "GeForce RTX 4070 SUPER", VramGb: 12, MemBusBit: 192, MemSpeedGbps: 21) },
        ["10DE:2786"] = new[] { new GpuSpec("AD104", "Ada Lovelace", "TSMC 4N (5 nm)", 295, 35.8, 5888, 64, 184, "GDDR6X", 1920, 2475, 200, "13/04/2023", Model: "GeForce RTX 4070", VramGb: 12, MemBusBit: 192, MemSpeedGbps: 21) },
        ["10DE:2803"] = new[] { new GpuSpec("AD106", "Ada Lovelace", "TSMC 4N (5 nm)", 190, 22.9, 4352, 48, 136, "GDDR6", 2310, 2535, 160, "24/05/2023", Model: "GeForce RTX 4060 Ti (8 GB)", VramGb: 8, MemBusBit: 128, MemSpeedGbps: 18) },
        ["10DE:2805"] = new[] { new GpuSpec("AD106", "Ada Lovelace", "TSMC 4N (5 nm)", 190, 22.9, 4352, 48, 136, "GDDR6", 2310, 2535, 165, "18/07/2023", Model: "GeForce RTX 4060 Ti (16 GB)", VramGb: 16, MemBusBit: 128, MemSpeedGbps: 18) },
        ["10DE:2882"] = new[] { new GpuSpec("AD107", "Ada Lovelace", "TSMC 4N (5 nm)", 159, 18.9, 3072, 48, 96, "GDDR6", 1830, 2460, 115, "29/06/2023", Model: "GeForce RTX 4060", VramGb: 8, MemBusBit: 128, MemSpeedGbps: 17) },

        // ---- NVIDIA Ampere (RTX 30) ----
        ["10DE:2204"] = new[] { new GpuSpec("GA102", "Ampere", "Samsung 8 nm", 628, 28.3, 10496, 112, 328, "GDDR6X", 1395, 1695, 350, "24/09/2020", Model: "GeForce RTX 3090", VramGb: 24, MemBusBit: 384, MemSpeedGbps: 19.5) },
        ["10DE:2206"] = new[] { new GpuSpec("GA102", "Ampere", "Samsung 8 nm", 628, 28.3, 8704, 96, 272, "GDDR6X", 1440, 1710, 320, "17/09/2020", Model: "GeForce RTX 3080", VramGb: 10, MemBusBit: 320, MemSpeedGbps: 19) },
        ["10DE:2484"] = new[] { new GpuSpec("GA104", "Ampere", "Samsung 8 nm", 392, 17.4, 5888, 96, 184, "GDDR6", 1500, 1725, 220, "29/10/2020", Model: "GeForce RTX 3070", VramGb: 8, MemBusBit: 256, MemSpeedGbps: 14) },
        ["10DE:2486"] = new[] { new GpuSpec("GA104", "Ampere", "Samsung 8 nm", 392, 17.4, 4864, 80, 152, "GDDR6", 1410, 1665, 200, "02/12/2020", Model: "GeForce RTX 3060 Ti", VramGb: 8, MemBusBit: 256, MemSpeedGbps: 14) },
        ["10DE:2503"] = new[] { new GpuSpec("GA106", "Ampere", "Samsung 8 nm", 276, 12.0, 3584, 48, 112, "GDDR6", 1320, 1777, 170, "25/02/2021", Model: "GeForce RTX 3060 (12 GB)", VramGb: 12, MemBusBit: 192, MemSpeedGbps: 15) },
        ["10DE:2504"] = new[] { new GpuSpec("GA106", "Ampere", "Samsung 8 nm", 276, 12.0, 3584, 48, 112, "GDDR6", 1320, 1777, 170, "25/02/2021", Model: "GeForce RTX 3060 (12 GB)", VramGb: 12, MemBusBit: 192, MemSpeedGbps: 15) },

        // ---- AMD RDNA 4 / 3 / 2 ----
        ["1002:7550"] = new[]
        {
            new GpuSpec("Navi 48", "RDNA 4", "TSMC N4P (4 nm)", 357, 53.9, 4096, 128, 256, "GDDR6", 2400, 2970, 304, "06/03/2025", "9070 XT", "Radeon RX 9070 XT", 16, 256, 20.1),
            new GpuSpec("Navi 48", "RDNA 4", "TSMC N4P (4 nm)", 357, 53.9, 3584, 128, 224, "GDDR6", 2070, 2520, 220, "06/03/2025", "9070", "Radeon RX 9070", 16, 256, 20.1),
        },
        ["1002:744C"] = new[]
        {
            new GpuSpec("Navi 31", "RDNA 3", "TSMC 5 nm + 6 nm", 529, 57.7, 6144, 192, 384, "GDDR6", 1855, 2499, 355, "13/12/2022", "XTX", "Radeon RX 7900 XTX", 24, 384, 20),
            new GpuSpec("Navi 31", "RDNA 3", "TSMC 5 nm + 6 nm", 529, 57.7, 5376, 192, 336, "GDDR6", 1500, 2394, 315, "13/12/2022", "7900 XT", "Radeon RX 7900 XT", 20, 320, 20),
        },
        ["1002:747E"] = new[]
        {
            new GpuSpec("Navi 32", "RDNA 3", "TSMC 5 nm + 6 nm", 346, 28.1, 3840, 96, 240, "GDDR6", 1295, 2430, 263, "06/09/2023", "7800", "Radeon RX 7800 XT", 16, 256, 19.5),
            new GpuSpec("Navi 32", "RDNA 3", "TSMC 5 nm + 6 nm", 346, 28.1, 3456, 96, 216, "GDDR6", 1435, 2544, 245, "06/09/2023", "7700", "Radeon RX 7700 XT", 12, 192, 18),
        },
        ["1002:7480"] = new[] { new GpuSpec("Navi 33", "RDNA 3", "TSMC 6 nm", 204, 13.3, 2048, 64, 128, "GDDR6", 1720, 2655, 165, "25/05/2023", Model: "Radeon RX 7600", VramGb: 8, MemBusBit: 128, MemSpeedGbps: 18) },
        ["1002:164E"] = new[] { new GpuSpec("Raphael (iGPU)", "RDNA 2", "TSMC 6 nm", 122, 3.4, 128, 4, 8, "Memória do sistema (DDR5)", 400, 2200, 0, "27/09/2022", Model: "AMD Radeon (Raphael, iGPU)", VramGb: 0, MemBusBit: 0, MemSpeedGbps: 0) },
    };

    /// <summary>Recursos por arquitetura, para a linha "Tecnologias".</summary>
    public static readonly Dictionary<string, string> ArchFeatures = new()
    {
        ["Blackwell"] = "DLSS 4 (Multi Frame Generation) · Ray Tracing 4ª ger. · Tensor 5ª ger. · Reflex 2 · NVENC 9ª ger.",
        ["Ada Lovelace"] = "DLSS 3 (Frame Generation) · Ray Tracing 3ª ger. · Tensor 4ª ger. · Reflex · NVENC AV1",
        ["Ampere"] = "DLSS 2 · Ray Tracing 2ª ger. · Tensor 3ª ger. · Reflex · Resizable BAR",
        ["RDNA 4"] = "FSR 4 · Ray Accelerators 3ª ger. · AI Accelerators · Anti-Lag 2 · AV1",
        ["RDNA 3"] = "FSR 3 · Ray Accelerators 2ª ger. · AI Accelerators · AV1",
        ["RDNA 2"] = "FSR · Ray Accelerators 1ª ger.",
    };

    public static GpuSpec? Find(string vendorId, string deviceId, string name)
    {
        if (!Db.TryGetValue($"{vendorId}:{deviceId}", out var specs)) return null;
        return specs.FirstOrDefault(s => s.NameMatch != null && name.Contains(s.NameMatch, StringComparison.OrdinalIgnoreCase))
               ?? specs[0];
    }

    /// <summary>Todas as placas do catálogo para a comparação: (nome para mostrar, especificação). Sem duplicatas.</summary>
    public static List<(string Label, GpuSpec Spec)> All()
    {
        var list = new List<(string, GpuSpec)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var specs in Db.Values)
            foreach (var s in specs)
                if (seen.Add(s.DisplayName)) list.Add((s.DisplayName, s));
        return list.OrderBy(x => x.Item1, StringComparer.CurrentCulture).ToList();
    }

    public static string SubvendorName(string id) => id.ToUpperInvariant() switch
    {
        "1043" => "ASUS", "1458" => "Gigabyte", "1462" => "MSI", "3842" => "EVGA", "19DA" => "Zotac",
        "196E" => "PNY", "10DE" => "NVIDIA", "1002" => "AMD", "1DA2" => "Sapphire", "1682" => "XFX",
        "148C" => "PowerColor", "1849" => "ASRock", "7377" => "Colorful", "1569" => "Palit", "10B0" => "Gainward",
        "1B4C" => "Galax / KFA2", "8086" => "Intel", "1028" => "Dell", "103C" => "HP", "17AA" => "Lenovo",
        "1025" => "Acer", "144D" => "Samsung", "1D05" => "Tongfang",
        _ => $"Desconhecido ({id})"
    };
}
