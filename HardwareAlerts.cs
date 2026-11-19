using System;
using System.Collections.Generic;
using System.Linq;

namespace GuiTech;

/// <summary>Um aviso de hardware (mostrado em toast e no histórico de eventos).</summary>
public sealed class HardwareAlert
{
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public string Icon { get; init; } = "";
}

/// <summary>Amostra de um disco para a verificação de avisos.</summary>
public sealed record DiskSample(string Title, DiskVerdict Verdict, double? TempC, double? Wear);

/// <summary>
/// Avisos automáticos de hardware em segundo plano: temperatura da CPU/GPU, saúde dos discos e
/// desgaste da bateria. Toda a decisão (limiares, leituras seguidas, intervalo entre avisos)
/// fica aqui, separada da interface, para poder ser testada sem hardware.
/// </summary>
public static class HardwareAlerts
{
    /// <summary>Temperatura da CPU a partir da qual avisa (°C).</summary>
    public const float CpuTempLimit = 90f;
    /// <summary>Temperatura da GPU a partir da qual avisa (°C).</summary>
    public const float GpuTempLimit = 88f;
    /// <summary>Temperatura de disco a partir da qual avisa (°C).</summary>
    public const float DiskTempLimit = 60f;
    /// <summary>Desgaste de SSD a partir do qual avisa (%).</summary>
    public const double DiskWearLimit = 80;
    /// <summary>Desgaste da bateria a partir do qual avisa (%).</summary>
    public const double BatteryWearLimit = 30;

    /// <summary>Leituras de 1 s seguidas acima do limite antes de avisar (evita picos de 1 segundo).</summary>
    public const int StreakNeeded = 3;

    /// <summary>Tempo mínimo entre dois avisos do mesmo tipo.</summary>
    public static readonly TimeSpan TempCooldown = TimeSpan.FromHours(2);
    public static readonly TimeSpan DiskCooldown = TimeSpan.FromDays(7);
    public static readonly TimeSpan BatteryCooldown = TimeSpan.FromDays(7);

    private static readonly Dictionary<string, int> _streak = new();
    private static readonly Dictionary<string, DateTime> _lastFired = new();
    /// <summary>As temperaturas são lidas na thread da interface e os discos/bateria em segundo plano.</summary>
    private static readonly object _gate = new();

    /// <summary>Zera o estado interno (usado nos testes).</summary>
    public static void Reset()
    {
        lock (_gate)
        {
            _streak.Clear();
            _lastFired.Clear();
        }
    }

    private static bool CanFire(string kind, DateTime now, TimeSpan cooldown)
    {
        lock (_gate)
            return !_lastFired.TryGetValue(kind, out var last) || now - last >= cooldown;
    }

    private static void MarkFired(string kind, DateTime now)
    {
        lock (_gate) _lastFired[kind] = now;
    }

    private static bool CountStreak(string kind, bool above)
    {
        lock (_gate)
        {
            int n = above ? (_streak.TryGetValue(kind, out var s) ? s + 1 : 1) : 0;
            _streak[kind] = n;
            return n >= StreakNeeded;
        }
    }

    /// <summary>
    /// Chamado a cada segundo com as temperaturas atuais. Avisa quando a leitura fica
    /// <see cref="StreakNeeded"/> segundos seguidos acima do limite (e respecta o intervalo).
    /// </summary>
    public static List<HardwareAlert> Sample(float? cpuTemp, float? gpuTemp, DateTime now)
    {
        var list = new List<HardwareAlert>();

        if (cpuTemp is float cpu && cpu > 0)
        {
            if (CountStreak("cpu", cpu >= CpuTempLimit) && CanFire("cpu", now, TempCooldown))
            {
                MarkFired("cpu", now);
                list.Add(new HardwareAlert
                {
                    Kind = "cpu",
                    Title = "Temperatura da CPU alta",
                    Text = $"A CPU está em {cpu:0} °C há alguns segundos (limite {CpuTempLimit:0} °C). " +
                           "Feche programas pesados ou verifique a refrigeração.",
                    Icon = "\uE7BA",
                });
            }
        }
        else CountStreak("cpu", false);

        if (gpuTemp is float gpu && gpu > 0)
        {
            if (CountStreak("gpu", gpu >= GpuTempLimit) && CanFire("gpu", now, TempCooldown))
            {
                MarkFired("gpu", now);
                list.Add(new HardwareAlert
                {
                    Kind = "gpu",
                    Title = "Temperatura da GPU alta",
                    Text = $"A placa de vídeo está em {gpu:0} °C há alguns segundos (limite {GpuTempLimit:0} °C). " +
                           "Reduza os gráficos do jogo ou melhore a ventilação do gabinete.",
                    Icon = "\uE7BA",
                });
            }
        }
        else CountStreak("gpu", false);

        return list;
    }

    /// <summary>Avisos de disco: veredito ruim, desgaste alto ou temperatura alta.</summary>
    public static List<HardwareAlert> CheckDisks(IEnumerable<DiskSample> disks, DateTime now)
    {
        var list = new List<HardwareAlert>();
        foreach (var d in disks)
        {
            if (d.Verdict == DiskVerdict.Critical && CanFire("disk-critical", now, DiskCooldown))
            {
                MarkFired("disk-critical", now);
                list.Add(new HardwareAlert
                {
                    Kind = "disk",
                    Title = "Disco com problema",
                    Text = $"{d.Title}: o Windows reportou falha ou risco de falha. Faça backup dos dados agora.",
                    Icon = "\uE7BA",
                });
                continue;
            }

            if (d.TempC is double t && t >= DiskTempLimit && CanFire("disk-temp", now, DiskCooldown))
            {
                MarkFired("disk-temp", now);
                list.Add(new HardwareAlert
                {
                    Kind = "disk",
                    Title = "Disco muito quente",
                    Text = $"{d.Title} está em {t:0} °C (limite {DiskTempLimit:0} °C). Melhore a ventilação perto dos discos.",
                    Icon = "\uE7BA",
                });
                continue;
            }

            if (d.Wear is double w && w >= DiskWearLimit && CanFire("disk-wear", now, DiskCooldown))
            {
                MarkFired("disk-wear", now);
                list.Add(new HardwareAlert
                {
                    Kind = "disk",
                    Title = "SSD perto do fim da vida útil",
                    Text = $"{d.Title} já consumiu {w:0}% da vida de escrita (limite {DiskWearLimit:0}%). Considere trocar o disco.",
                    Icon = "\uE7BA",
                });
            }
        }
        return list;
    }

    /// <summary>Aviso de desgaste da bateria (notebooks).</summary>
    public static List<HardwareAlert> CheckBattery(double? wear, DateTime now)
    {
        var list = new List<HardwareAlert>();
        if (wear is double w && w >= BatteryWearLimit && CanFire("battery", now, BatteryCooldown))
        {
            MarkFired("battery", now);
            list.Add(new HardwareAlert
            {
                Kind = "battery",
                Title = "Bateria desgastada",
                Text = $"A bateria já perdeu {w:0}% da capacidade original. A autonomia caiu; considere trocar a bateria.",
                Icon = "\uE7BA",
            });
        }
        return list;
    }

    /// <summary>Avisos pendentes (usado nos testes: quais tipos já dispararam).</summary>
    public static bool HasFired(string kind)
    {
        lock (_gate) return _lastFired.ContainsKey(kind);
    }
}
