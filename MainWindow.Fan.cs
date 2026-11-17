using System.Windows;
using System.Windows.Threading;

namespace GuiTech;

/// <summary>
/// Ventoinha da GPU: o usuário escolhe uma temperatura máxima e o Gui_Tech monta a curva de
/// 5 pontos a partir dela. Enquanto o limite estiver ativo, um vigia a cada 3 s aumenta a
/// velocidade se a placa passar do limite e alivia quando ela esfria.
/// Ao fechar o programa a ventoinha volta ao controle do driver.
/// </summary>
public partial class MainWindow
{
    private int _fanTarget;                  // limite aplicado; 0 = automático do driver
    private bool _fanApplied;                // a curva própria está ativa
    private int _fanBoost;                   // passos extras aplicados por aquecimento (0..3)
    private DispatcherTimer? _fanTimer;
    private readonly List<(int Temp, int Speed)> _fanPoints = new();

    private void InitFan()
    {
        if (!AmdGpuFan.Available)
        {
            FanIntro.Text = AmdGpuFan.Error;
            FanTarget.IsEnabled = false;
            BtnFanApply.IsEnabled = false;
            BtnFanAuto.IsEnabled = false;
            FanTargetHint.Text = "";
            FanLive.Text = "";
            FanStatus.Text = "";
            return;
        }

        var (tempMin, tempMax) = AmdGpuFan.TempRange;
        FanTarget.Minimum = Math.Max(60, tempMin);
        FanTarget.Maximum = Math.Min(95, tempMax);
        FanTarget.TickFrequency = 1;

        int saved = _settings.GpuFanTarget;
        if (saved > 0)
            FanTarget.Value = Math.Clamp(saved, (int)FanTarget.Minimum, (int)FanTarget.Maximum);
        else
            FanTarget.Value = Math.Clamp(80, (int)FanTarget.Minimum, (int)FanTarget.Maximum);

        UpdateFanTargetText();

        _fanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _fanTimer.Tick += (_, _) => FanTick();
        _fanTimer.Start();
        FanTick();

        // O limite salvo é reaplicado ao abrir; ao fechar, a ventoinha volta ao driver.
        if (saved > 0) ApplyFanTarget((int)FanTarget.Value, saveSetting: false);
        else FanStatus.Text = "A ventoinha está no automático do driver. Escolha um limite e clique em APLICAR LIMITE.";
    }

    /// <summary>Curva de 5 pontos derivada do limite escolhido.</summary>
    private static List<(int Temp, int Speed)> FanCurveFor(int target, int boost)
    {
        var (tempMin, tempMax) = AmdGpuFan.TempRange;
        int[] offsets = { -45, -30, -18, -8, 0 };
        int[] speeds = { 25, 35, 50, 75, 100 };

        var temps = new int[5];
        for (int i = 0; i < 5; i++)
            temps[i] = Math.Clamp(target + offsets[i], tempMin, tempMax);

        // Garante temperaturas estritamente crescentes dentro da faixa da placa.
        for (int i = 1; i < 5; i++)
            if (temps[i] <= temps[i - 1]) temps[i] = temps[i - 1] + 1;
        if (temps[4] > tempMax)
        {
            temps[4] = tempMax;
            for (int i = 4; i > 0; i--)
                if (temps[i - 1] >= temps[i]) temps[i - 1] = temps[i] - 1;
        }

        var points = new List<(int, int)>(5);
        for (int i = 0; i < 5; i++)
            points.Add((temps[i], Math.Min(100, speeds[i] + boost * 8)));
        return points;
    }

    private static int FanSpeedAt(IReadOnlyList<(int Temp, int Speed)> points, double temp)
    {
        if (points.Count == 0) return 0;
        if (temp <= points[0].Temp) return points[0].Speed;
        for (int i = 1; i < points.Count; i++)
        {
            var (t0, s0) = points[i - 1];
            var (t1, s1) = points[i];
            if (temp <= t1)
                return (int)Math.Round(s0 + (s1 - s0) * (temp - t0) / Math.Max(1, t1 - t0));
        }
        return points[^1].Speed;
    }

    private void ApplyFanTarget(int target, bool saveSetting = true)
    {
        if (!AmdGpuFan.Available) { FanStatus.Text = AmdGpuFan.Error; return; }

        var points = FanCurveFor(target, 0);
        if (!AmdGpuFan.Apply(points, out string error))
        {
            FanStatus.Text = "Não apliquei a curva: " + error;
            return;
        }

        _fanTarget = target;
        _fanBoost = 0;
        _fanApplied = true;
        _fanPoints.Clear();
        _fanPoints.AddRange(points);

        if (saveSetting)
        {
            _settings.GpuFanTarget = target;
            _settings.Save();
        }

        FanStatus.Text = $"Limite de {target} °C ativo — {points[0].Speed}% a {points[0].Temp} °C, " +
                         $"{points[2].Speed}% a {points[2].Temp} °C e {points[4].Speed}% a {points[4].Temp} °C.";
        FanTick();
    }

    private void RestoreFanAuto(bool saveSetting)
    {
        if (_fanApplied && !AmdGpuFan.RestoreAuto(out string error))
        {
            FanStatus.Text = "Não consegui voltar ao automático: " + error;
            return;
        }

        _fanApplied = false;
        _fanBoost = 0;
        _fanPoints.Clear();

        if (saveSetting)
        {
            _settings.GpuFanTarget = 0;
            _settings.Save();
        }

        FanStatus.Text = "Ventoinha no automático do driver. Nada mais é alterado na placa.";
        FanTick();
    }

    /// <summary>Chamado a cada 3 s: mostra a temperatura ao vivo e ajusta a curva se necessário.</summary>
    private void FanTick()
    {
        if (!AmdGpuFan.Available || FanLive == null) return;

        float? temp = GpuTemperature();
        int speed = _fanApplied ? FanSpeedAt(_fanPoints, temp ?? 0) : -1;
        FanLive.Text = temp is > 0
            ? $"Agora: {temp:0} °C" + (_fanApplied
                ? $" · curva em ~{speed}% · limite {_fanTarget} °C"
                : " · ventoinha no automático do driver")
            : "Temperatura da GPU indisponível neste momento.";

        if (!_fanApplied || temp is not > 0) return;
        double t = temp.Value;

        // Calor muito acima do limite: devolve ao driver, que tem proteção própria.
        if (t >= Math.Min(95, _fanTarget + 12))
        {
            RestoreFanAuto(saveSetting: false);
            FanStatus.Text = "A placa passou bem do limite: a ventoinha voltou ao automático do driver. " +
                             "Reduza a carga ou melhore a ventilação do gabinete.";
            return;
        }

        int want = _fanBoost;
        if (t > _fanTarget + 1) want = Math.Min(3, _fanBoost + 1);
        else if (t <= _fanTarget - 8) want = 0;
        if (want == _fanBoost) return;

        var points = FanCurveFor(_fanTarget, want);
        if (!AmdGpuFan.Apply(points, out string error))
        {
            FanStatus.Text = "O driver recusou o ajuste automático: " + error;
            return;
        }

        _fanBoost = want;
        _fanPoints.Clear();
        _fanPoints.AddRange(points);
        FanStatus.Text = want == 0
            ? $"Temperatura voltou ao normal — curva original do limite de {_fanTarget} °C."
            : $"Acima do limite: ventoinha reforçada (+{want * 8}%) para segurar {_fanTarget} °C.";
    }

    /// <summary>Fecha o vigia e devolve a ventoinha ao driver (o limite fica salvo para a próxima abertura).</summary>
    private void StopFan()
    {
        _fanTimer?.Stop();
        _fanTimer = null;
        if (_fanApplied)
        {
            AmdGpuFan.RestoreAuto(out _);
            _fanApplied = false;
        }
    }

    private void UpdateFanTargetText()
    {
        if (FanTargetValue == null) return;
        int target = (int)Math.Round(FanTarget.Value);
        FanTargetValue.Text = $"{target} °C";
        if (FanTargetHint == null || !AmdGpuFan.Available) return;

        var points = FanCurveFor(target, 0);
        FanTargetHint.Text = $"Ventoinha: {points[0].Speed}% até {points[0].Temp} °C, " +
                             $"{points[2].Speed}% a {points[2].Temp} °C e {points[4].Speed}% a {points[4].Temp} °C. " +
                             "Se a placa passar do limite, o Gui_Tech reforça a curva automaticamente.";
    }

    private void FanTarget_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateFanTargetText();

    private void BtnFanApply_Click(object sender, RoutedEventArgs e) => ApplyFanTarget((int)Math.Round(FanTarget.Value));

    private void BtnFanAuto_Click(object sender, RoutedEventArgs e) => RestoreFanAuto(saveSetting: true);
}
