using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuiTech;

/// <summary>Aba Gui_Tech OC: ajustes da GPU (NVML), verificador de estabilidade e guia de overclock na BIOS.</summary>
public partial class MainWindow
{
    public sealed record GuideStep(string Number, string Title, string Where, string Text);

    private uint? _ocClockCap;
    private uint? _ocPendingCap;
    private CancellationTokenSource? _ocCts;
    private bool _ocRunning;

    private nint OcGpu
    {
        get
        {
            if (_gpus.Count <= 0)
            {
                return IntPtr.Zero;
            }
            return _gpus[0].NvmlHandle;
        }
    }

    private void InitOc()
    {
        InitFan();
        InitGpuCompare();
        bool amd = InitAmdTuning();
        nint ocGpu = OcGpu;
        OcGpuTitle.Text = ((_gpus.Count > 0) ? ("GPU · " + GameCatalog.ShortGpuName(_gpus[0].Name).ToUpperInvariant()) : "GPU");
        if (amd)
        {
            // Placa AMD com clock/energia no Overdrive8: o cartão NVIDIA (NVML) sai de cena.
            OcNvCard.Visibility = Visibility.Collapsed;
        }
        else if (ocGpu == IntPtr.Zero)
        {
            OcGpuState.Text = "Energia e teto de clock por esta via: só placas NVIDIA (driver oficial). " +
                              "Em placas AMD, use o limite de temperatura da ventoinha logo abaixo.";
            Slider ocPower = OcPower;
            bool isEnabled = (BtnOcApply.IsEnabled = false);
            ocPower.IsEnabled = isEnabled;
        }
        else
        {
            (uint, uint)? tuple = Nvml.PowerConstraintsMw(ocGpu);
            if (tuple.HasValue)
            {
                (uint, uint) valueOrDefault = tuple.GetValueOrDefault();
                OcPower.Minimum = (double)valueOrDefault.Item1 / 1000.0;
                OcPower.Maximum = (double)valueOrDefault.Item2 / 1000.0;
            }
            uint? num = Nvml.PowerLimitMw(ocGpu);
            if (num.HasValue)
            {
                uint valueOrDefault2 = num.GetValueOrDefault();
                OcPower.Value = (double)valueOrDefault2 / 1000.0;
            }
            uint valueOrDefault3 = Nvml.MaxClock(ocGpu, 0).GetValueOrDefault(3000u);
            Chip("Sem teto", null, on: true);
            Chip($"Leve · {valueOrDefault3 - 300} MHz", valueOrDefault3 - 300, on: false);
            Chip($"Equilibrado · {valueOrDefault3 - 500} MHz", valueOrDefault3 - 500, on: false);
            Chip($"Econômico · {valueOrDefault3 - 800} MHz", valueOrDefault3 - 800, on: false);
            UpdateOcGpuState();
        }
        BuildOcGuide();
        UpdateOcHistory();
        UpdateRecentErrors();
        void Chip(string label, uint? cap, bool on)
        {
            RadioButton radioButton = new RadioButton
            {
                Content = label,
                GroupName = "OcClock",
                Style = (Style)FindResource("Chip"),
                IsChecked = on
            };
            radioButton.Checked += delegate
            {
                _ocPendingCap = cap;
            };
            OcClockPanel.Children.Add(radioButton);
        }
    }

    private void UpdateOcGpuState()
    {
        nint ocGpu = OcGpu;
        if (ocGpu != IntPtr.Zero)
        {
            uint? num = Nvml.PowerLimitMw(ocGpu);
            object obj;
            if (num.HasValue)
            {
                uint valueOrDefault = num.GetValueOrDefault();
                obj = $"{(double)valueOrDefault / 1000.0:0} W";
            }
            else
            {
                obj = "—";
            }
            string value = (string)obj;
            num = Nvml.PowerDefaultMw(ocGpu);
            object obj2;
            if (num.HasValue)
            {
                uint valueOrDefault2 = num.GetValueOrDefault();
                obj2 = $"{(double)valueOrDefault2 / 1000.0:0} W";
            }
            else
            {
                obj2 = "—";
            }
            string value2 = (string)obj2;
            TextBlock ocGpuState = OcGpuState;
            string obj3 = $"Limite de energia atual {value} (padrão {value2}) · teto de clock: ";
            num = _ocClockCap;
            object obj4;
            if (num.HasValue)
            {
                uint valueOrDefault3 = num.GetValueOrDefault();
                obj4 = $"{valueOrDefault3} MHz";
            }
            else
            {
                obj4 = "nenhum";
            }
            num = Nvml.Clock(ocGpu, 0);
            object obj5;
            if (num.HasValue)
            {
                uint valueOrDefault4 = num.GetValueOrDefault();
                obj5 = $"{valueOrDefault4} MHz";
            }
            else
            {
                obj5 = "—";
            }
            ocGpuState.Text = obj3 + (string?)obj4 + " · clock máximo atual: " + (string?)obj5;
        }
    }

    private void OcPower_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OcPowerValue != null)
        {
            OcPowerValue.Text = $"{OcPower.Value:0} W";
        }
    }

    private void BtnOcApply_Click(object sender, RoutedEventArgs e)
    {
        nint ocGpu = OcGpu;
        if (ocGpu == IntPtr.Zero || !RequireAdmin())
        {
            return;
        }
        List<string> list = new List<string>();
        int code = Nvml.SetPowerLimitMw(ocGpu, (uint)(OcPower.Value * 1000.0));
        string? text = Nvml.Explain(code);
        list.Add((text != null) ? ("Energia: não aplicado (" + text + ").") : $"Energia: {OcPower.Value:0} W aplicado.");
        uint? ocPendingCap = _ocPendingCap;
        int code2;
        if (ocPendingCap.HasValue)
        {
            uint valueOrDefault = ocPendingCap.GetValueOrDefault();
            code2 = Nvml.LockGpuClocks(ocGpu, 210u, valueOrDefault);
        }
        else
        {
            code2 = Nvml.ResetGpuClocks(ocGpu);
        }
        string? text2 = Nvml.Explain(code2);
        if (text2 != null)
        {
            list.Add("Teto de clock: não aplicado (" + text2 + ").");
        }
        else
        {
            _ocClockCap = _ocPendingCap;
            ocPendingCap = _ocClockCap;
            object item;
            if (ocPendingCap.HasValue)
            {
                uint valueOrDefault2 = ocPendingCap.GetValueOrDefault();
                item = $"Teto de clock: {valueOrDefault2} MHz aplicado.";
            }
            else
            {
                item = "Teto de clock: removido.";
            }
            list.Add((string)item);
        }
        OcGpuResult.Text = string.Join(" ", list) + " Rode o verificador para confirmar a estabilidade.";
        UpdateOcGpuState();
    }

    private void BtnOcReset_Click(object sender, RoutedEventArgs e)
    {
        nint ocGpu = OcGpu;
        if (ocGpu == IntPtr.Zero || !RequireAdmin())
        {
            return;
        }
        uint? num = Nvml.PowerDefaultMw(ocGpu);
        int num2;
        if (num.HasValue)
        {
            uint valueOrDefault = num.GetValueOrDefault();
            num2 = Nvml.SetPowerLimitMw(ocGpu, valueOrDefault);
        }
        else
        {
            num2 = 3;
        }
        int code = num2;
        int num3 = Nvml.ResetGpuClocks(ocGpu);
        num = Nvml.PowerDefaultMw(ocGpu);
        if (num.HasValue)
        {
            uint valueOrDefault2 = num.GetValueOrDefault();
            OcPower.Value = (double)valueOrDefault2 / 1000.0;
        }
        foreach (RadioButton item in OcClockPanel.Children.OfType<RadioButton>())
        {
            item.IsChecked = item.Content as string == "Sem teto";
        }
        if (num3 == 0)
        {
            _ocClockCap = null;
        }
        TextBlock ocGpuResult = OcGpuResult;
        string? text = Nvml.Explain(code) ?? Nvml.Explain(num3);
        ocGpuResult.Text = ((text != null) ? ("Restauração parcial: " + text + ".") : "GPU de volta ao padrão de fábrica.");
        UpdateOcGpuState();
    }

    // ================= Ajustes AMD (Overdrive8) =================

    private bool _amdTuning;

    /// <summary>Monta o cartão de clock/energia da AMD. Retorna true quando ele assume o lugar do NVIDIA.</summary>
    private bool InitAmdTuning()
    {
        AmdTuningCard.Visibility = Visibility.Collapsed;
        _amdTuning = false;
        if (!AmdGpuFan.TuningAvailable) return false;

        var core = AmdGpuFan.Core();
        var mem = AmdGpuFan.Memory();
        var power = AmdGpuFan.Power();
        if (core == null || mem == null || power == null) return false;

        string gpu = _gpus.Count > 0 ? GameCatalog.ShortGpuName(_gpus[0].Name) : "AMD";
        AmdTuningTitle.Text = "AJUSTES AMD (OVERDRIVE8) · " + gpu.ToUpperInvariant();
        AmdTuningIntro.Text =
            $"O driver informa para esta placa: clock da GPU {core.Value.Min}–{core.Value.Max} MHz " +
            $"(padrão {core.Value.Default}), memória {mem.Value.Min}–{mem.Value.Max} MHz " +
            $"(padrão {mem.Value.Default}) e energia de {power.Value.Min}% a {power.Value.Max}% " +
            $"(padrão {power.Value.Default}%).";

        ApplyReading(AmdCore, core.Value);
        ApplyReading(AmdMem, mem.Value);
        ApplyReading(AmdPower, power.Value);
        UpdateAmdLabels();

        // Nesta placa o clock já pode estar no teto exposto pelo driver: avisa em vez de deixar
        // o usuário descobrir só depois de aplicar (o driver recusa reduzir o clock máximo).
        AmdCoreHint.Text = core.Value.Current >= core.Value.Max
            ? $"Esta placa já está no clock máximo que o driver expõe ({core.Value.Max} MHz). " +
              "Não há folga para subir; baixar não é aceito pelo driver desta placa."
            : "Teto de clock da GPU. Subir aumenta o FPS e o calor; o limite de energia abaixo " +
              "costuma ser o que segura o ganho. O driver pode recusar valores abaixo do atual.";

        AmdTuningResult.Text = "";
        AmdTuningCard.Visibility = Visibility.Visible;
        _amdTuning = true;

        RefreshOcProfiles();
        // Perfil marcado como "aplicar ao abrir": entra já aplicado, sem o usuário clicar em nada.
        if (OcProfileStore.Find(_settings.OcProfiles, _settings.OcProfileOnStart) is { } auto)
            ApplyOcProfile(auto, startup: true);

        return true;
    }

    private static void ApplyReading(Slider slider, AmdGpuFan.Reading r)
    {
        slider.Minimum = r.Min;
        slider.Maximum = r.Max;
        slider.Value = Od8Tuning.Fit(r.Current, r.Min, r.Max);
    }

    /// <summary>Relê os valores da placa e reposiciona os controles (após aplicar ou restaurar).</summary>
    private void RefreshAmdTuning()
    {
        var core = AmdGpuFan.Core();
        var mem = AmdGpuFan.Memory();
        var power = AmdGpuFan.Power();
        if (core != null) ApplyReading(AmdCore, core.Value);
        if (mem != null) ApplyReading(AmdMem, mem.Value);
        if (power != null) ApplyReading(AmdPower, power.Value);
        UpdateAmdLabels();
    }

    private void UpdateAmdLabels()
    {
        if (AmdCoreValue != null) AmdCoreValue.Text = Od8Tuning.Describe((int)Math.Round(AmdCore.Value), "MHz");
        if (AmdMemValue != null) AmdMemValue.Text = Od8Tuning.Describe((int)Math.Round(AmdMem.Value), "MHz");
        if (AmdPowerValue != null) AmdPowerValue.Text = Od8Tuning.Describe((int)Math.Round(AmdPower.Value), "%", signed: true);
    }

    private void AmdTuning_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // ValueChanged também dispara durante o carregamento do XAML, antes dos campos existirem.
        if (!_amdTuning) return;
        UpdateAmdLabels();
    }

    /// <summary>
    /// A roda do mouse rola a página em vez de mudar o slider. Sem isso, rolar a tela com o
    /// ponteiro sobre um controle muda o valor sem o usuário perceber.
    /// </summary>
    private void Slider_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (sender is not DependencyObject d) return;
        for (DependencyObject? p = d; p != null; p = VisualTreeHelper.GetParent(p))
        {
            if (p is ScrollViewer scroll)
            {
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta);
                return;
            }
        }
    }

    private void BtnAmdApply_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        int core = (int)Math.Round(AmdCore.Value);
        int mem = (int)Math.Round(AmdMem.Value);
        int power = (int)Math.Round(AmdPower.Value);
        if (!AmdGpuFan.ApplyTuning(core, mem, power, out string error))
        {
            AmdTuningResult.Text = "Não apliquei: " + error;
            return;
        }

        RefreshAmdTuning();
        AmdTuningResult.Text =
            $"Aplicado e conferido na placa: GPU {core} MHz · memória {mem} MHz · energia " +
            $"{Od8Tuning.Describe(power, "%", signed: true)}. Rode o verificador de estabilidade abaixo.";
    }

    private void BtnAmdReset_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (!AmdGpuFan.RestoreTuning(out string error))
        {
            AmdTuningResult.Text = "Não consegui voltar ao padrão: " + error;
            return;
        }

        RefreshAmdTuning();
        var coreNow = AmdGpuFan.Core();
        string extra = coreNow is { } c && c.Current != c.Default
            ? $" O clock da GPU ficou em {c.Current} MHz (o driver desta placa não aceita reduzir)."
            : "";
        AmdTuningResult.Text = "VRAM e energia de volta ao padrão de fábrica do driver." + extra;
    }

    // ================= Perfis de overclock =================

    /// <summary>Recarrega a lista de perfis e seleciona um (por padrão, o que abre junto com o app).</summary>
    private void RefreshOcProfiles(string? select = null)
    {
        var names = _settings.OcProfiles.Select(p => p.Name).ToList();
        OcProfileBox.ItemsSource = names;
        if (BtnOcProfileApply != null)
            BtnOcProfileApply.IsEnabled = BtnOcProfileDelete.IsEnabled = names.Count > 0;
        if (OcProfileAuto != null) OcProfileAuto.IsEnabled = names.Count > 0;

        string? want = select ?? _settings.OcProfileOnStart;
        int at = want == null
            ? -1
            : names.FindIndex(n => string.Equals(n, want, StringComparison.OrdinalIgnoreCase));
        OcProfileBox.SelectedIndex = at >= 0 ? at : (names.Count > 0 ? 0 : -1);
        UpdateOcProfileAuto();
    }

    /// <summary>Marca o "aplicar ao abrir" só quando o perfil escolhido é o que está salvo para a abertura.</summary>
    private void UpdateOcProfileAuto()
    {
        if (OcProfileAuto == null) return;
        bool on = OcProfileBox.SelectedItem is string name
                  && string.Equals(_settings.OcProfileOnStart, name, StringComparison.OrdinalIgnoreCase);
        OcProfileAuto.IsChecked = on;
    }

    private void OcProfile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (OcProfileAuto == null) return;
        if (OcProfileBox.SelectedItem is string name) OcProfileName.Text = name;
        UpdateOcProfileAuto();
    }

    private void BtnOcProfileSave_Click(object sender, RoutedEventArgs e)
    {
        string name = OcProfileName.Text.Trim();
        if (name.Length == 0) name = OcProfileStore.UniqueName(_settings.OcProfiles, "Perfil");

        var profile = new OcProfile
        {
            Name = name,
            Core = (int)Math.Round(AmdCore.Value),
            Mem = (int)Math.Round(AmdMem.Value),
            Power = (int)Math.Round(AmdPower.Value),
        };

        bool replaced = OcProfileStore.IndexOf(_settings.OcProfiles, name) >= 0;
        OcProfileStore.Save(_settings.OcProfiles, profile);
        _settings.Save();

        OcProfileName.Text = "";
        RefreshOcProfiles(name);
        OcProfileStatus.Text = (replaced ? $"Perfil \"{name}\" atualizado: " : $"Perfil \"{name}\" salvo: ")
                               + OcProfileStore.Describe(profile) + ".";
    }

    private void BtnOcProfileApply_Click(object sender, RoutedEventArgs e)
    {
        if (OcProfileBox.SelectedItem is not string name) return;
        if (OcProfileStore.Find(_settings.OcProfiles, name) is { } profile) ApplyOcProfile(profile);
    }

    private void BtnOcProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        if (OcProfileBox.SelectedItem is not string name) return;
        int at = OcProfileStore.IndexOf(_settings.OcProfiles, name);
        if (at < 0) return;

        _settings.OcProfiles.RemoveAt(at);
        if (string.Equals(_settings.OcProfileOnStart, name, StringComparison.OrdinalIgnoreCase))
            _settings.OcProfileOnStart = null;
        _settings.Save();

        RefreshOcProfiles();
        OcProfileStatus.Text = $"Perfil \"{name}\" apagado.";
    }

    private void OcProfileAuto_Click(object sender, RoutedEventArgs e)
    {
        if (OcProfileBox.SelectedItem is not string name)
        {
            if (OcProfileAuto != null) OcProfileAuto.IsChecked = false;
            OcProfileStatus.Text = "Salve um perfil antes de escolher um para abrir junto com o Gui_Tech.";
            return;
        }

        bool on = OcProfileAuto.IsChecked == true;
        _settings.OcProfileOnStart = on ? name : null;
        _settings.Save();
        OcProfileStatus.Text = on
            ? $"Ao abrir o Gui_Tech, o perfil \"{name}\" será aplicado automaticamente."
            : "Nenhum perfil é aplicado automaticamente ao abrir.";
    }

    /// <summary>
    /// Põe os valores do perfil nos controles e aplica na placa. Usado pelo botão Aplicar e pelo
    /// "aplicar ao abrir"; quando a placa já está com esses valores, só confirma (sem escrever).
    /// </summary>
    private bool ApplyOcProfile(OcProfile profile, bool startup = false)
    {
        if (!startup && !RequireAdmin()) return false;
        if (!AmdGpuFan.TuningAvailable)
        {
            OcProfileStatus.Text = "Esta placa não expõe clock/energia pela API da AMD: o perfil não pode ser aplicado.";
            return false;
        }

        int core = Od8Tuning.Fit(profile.Core, (int)AmdCore.Minimum, (int)AmdCore.Maximum);
        int mem = Od8Tuning.Fit(profile.Mem, (int)AmdMem.Minimum, (int)AmdMem.Maximum);
        int power = Od8Tuning.Fit(profile.Power, (int)AmdPower.Minimum, (int)AmdPower.Maximum);

        AmdCore.Value = core;
        AmdMem.Value = mem;
        AmdPower.Value = power;
        UpdateAmdLabels();

        string label = $"Perfil \"{profile.Name}\"";
        string when = startup ? " ao abrir o Gui_Tech" : "";

        // Já está na placa (caso típico de abrir o app de novo no mesmo dia): não escreve de novo.
        bool same = AmdGpuFan.Core() is { } c0 && c0.Current == core
                    && AmdGpuFan.Memory() is { } m0 && m0.Current == mem
                    && AmdGpuFan.Power() is { } p0 && p0.Current == power;
        if (same)
        {
            RefreshAmdTuning();
            OcProfileStatus.Text = $"{label} já estava aplicado{when}: {OcProfileStore.Describe(profile)}.";
            return true;
        }

        if (!AmdGpuFan.ApplyTuning(core, mem, power, out string error))
        {
            OcProfileStatus.Text = $"{label} não entrou{when}: {error}";
            return false;
        }

        RefreshAmdTuning();
        OcProfileStatus.Text = $"{label} aplicado{when}: {OcProfileStore.Describe(profile)}.";
        return true;
    }

    private void UpdateRecentErrors()
    {
        HardwareErrors hardwareErrors = OcCheck.ErrorsSince(DateTime.Now.AddDays(-7.0));
        OcRecentErrors.Text = ((hardwareErrors.Error != null) ? ("Não foi possível ler o registro: " + hardwareErrors.Error) : ((hardwareErrors.Total == 0) ? "Nenhum erro de hardware nem reinício do driver de vídeo nos últimos 7 dias." : $"Últimos 7 dias: {hardwareErrors.Whea} erros WHEA · {hardwareErrors.GpuResets} reinícios do driver de vídeo.\nÚltimo: {hardwareErrors.LastDetail}"));
    }

    private void UpdateOcHistory()
    {
        List<BenchRecord> source = _settings.BenchRecords.Where((BenchRecord r) => r.Test == "oc").ToList();
        OcHistory.ItemsSource = source.Select((BenchRecord r) => new MeterItem($"{r.Date:dd/MM HH:mm}", r.Summary, r.Score)).ToList();
    }

    private async void BtnOcCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_ocRunning)
        {
            _ocCts?.Cancel();
            return;
        }
        if (TestRunning || AnyBenchRunning)
        {
            OcStatus.Text = "Termine o outro teste antes.";
            return;
        }
        string text = CheckedTag(OcTargetPanel) ?? "cpu";
        bool flag = ((text == "cpu" || text == "both") ? true : false);
        bool cpu = flag;
        flag = ((text == "gpu" || text == "both") ? true : false);
        bool gpu = flag;
        TimeSpan duration = TimeSpan.FromMinutes(int.Parse(CheckedTag(OcDurPanel) ?? "15"));
        string label = ((OcLabel.Text.Trim().Length > 0) ? OcLabel.Text.Trim() : "sem descrição");
        _ocCts = new CancellationTokenSource();
        CancellationToken ct = _ocCts.Token;
        _ocRunning = true;
        BtnOcCheck.Content = "Cancelar";
        BtnOcCheck.Style = (Style)FindResource("OutlineButton");
        OcVerdictCard.Visibility = Visibility.Collapsed;
        StackPanel ocTargetPanel = OcTargetPanel;
        flag = (OcDurPanel.IsEnabled = false);
        ocTargetPanel.IsEnabled = flag;
        DateTime start = DateTime.Now;
        double? cpuScore = null;
        double? gpuScore = null;
        float maxCpu = 0f;
        float maxGpu = 0f;
        double sumCpuW = 0.0;
        double sumGpuW = 0.0;
        int nCpuW = 0;
        int nGpuW = 0;
        string? fail = null;
        StressTester stress = new StressTester();
        GpuBurn burn = new GpuBurn();
        try
        {
            if (cpu)
            {
                OcStatus.Text = "Medindo a nota da CPU (render multi-core)…";
                BenchResult benchResult = await Task.Run(() => _bench.Run(Environment.ProcessorCount, TimeSpan.Zero, ct), ct);
                if (!ct.IsCancellationRequested)
                {
                    cpuScore = benchResult.Score;
                }
            }
            if (gpu && !ct.IsCancellationRequested)
            {
                OcStatus.Text = "Medindo a nota da GPU (cena de jogo, 30 s)…";
                GpuGameBench gb = new GpuGameBench
                {
                    Duration = TimeSpan.FromSeconds(30.0),
                    AdapterName = ((_gpus.Count > 0) ? _gpus[0].Name : "")
                };
                _gpuBenchActive = true;
                using (ct.Register(gb.Cancel))
                {
                    GpuGameResult? gpuGameResult = await Task.Factory.StartNew(() => gb.Run(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    if (gpuGameResult != null)
                    {
                        gpuScore = gpuGameResult.Score;
                    }
                    else if (!gb.Cancelled)
                    {
                        fail = "a cena de GPU falhou (" + gb.Error + ")";
                    }
                }
                _gpuBenchActive = false;
            }
            if (fail == null && !ct.IsCancellationRequested)
            {
                if (cpu)
                {
                    stress.Start(cpu: true, ram: true);
                }
                if (gpu)
                {
                    burn.Start(new GpuBurnSettings
                    {
                        Width = 1920,
                        Height = 1080,
                        Msaa = 4,
                        Shells = 128,
                        Iterations = 48,
                        AdapterName = ((_gpus.Count > 0) ? _gpus[0].Name : "")
                    });
                }
                Stopwatch sw = Stopwatch.StartNew();
                float cw = default(float);
                float gw = default(float);
                while (sw.Elapsed < duration && !ct.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                    float? cpuTemperature = _monitor.CpuTemperature;
                    float? num = MainGpuTemp();
                    if (cpuTemperature.HasValue)
                    {
                        float valueOrDefault = cpuTemperature.GetValueOrDefault();
                        maxCpu = Math.Max(maxCpu, valueOrDefault);
                    }
                    if (num.HasValue)
                    {
                        float valueOrDefault2 = num.GetValueOrDefault();
                        maxGpu = Math.Max(maxGpu, valueOrDefault2);
                    }
                    float? cpuPower = _monitor.CpuPower;
                    int num2;
                    if (cpuPower.HasValue)
                    {
                        cw = cpuPower.GetValueOrDefault();
                        num2 = 1;
                    }
                    else
                    {
                        num2 = 0;
                    }
                    if (((uint)num2 & (cpu ? 1u : 0u)) != 0)
                    {
                        sumCpuW += (double)cw;
                        nCpuW++;
                    }
                    cpuPower = MainGpuPower();
                    int num3;
                    if (cpuPower.HasValue)
                    {
                        gw = cpuPower.GetValueOrDefault();
                        num3 = 1;
                    }
                    else
                    {
                        num3 = 0;
                    }
                    if (((uint)num3 & (gpu ? 1u : 0u)) != 0)
                    {
                        sumGpuW += (double)gw;
                        nGpuW++;
                    }
                    OcProgress.ScaleX = sw.Elapsed / duration;
                    OcStatus.Text = $"Stress {((cpu && gpu) ? "CPU + RAM + GPU" : (cpu ? "CPU + RAM" : "GPU"))} · {sw.Elapsed:mm\\:ss} de {duration:mm\\:ss} · máx. CPU {((maxCpu > 0f) ? $"{maxCpu:0} °C" : "—")} · máx. GPU {((maxGpu > 0f) ? $"{maxGpu:0} °C" : "—")}";
                    if (cpu && cpuTemperature >= 95f)
                    {
                        fail = $"CPU chegou a {cpuTemperature:0} °C (limite de segurança 95 °C)";
                        break;
                    }
                    if (gpu && num >= 88f)
                    {
                        fail = $"GPU chegou a {num:0} °C (limite de segurança 88 °C)";
                        break;
                    }
                    if (cpu && stress.MemoryErrors > 0)
                    {
                        fail = $"{stress.MemoryErrors} erros de memória (RAM/EXPO instável)";
                        break;
                    }
                    if (gpu && !burn.Running)
                    {
                        fail = ((burn.Error != null) ? ("a GPU falhou (" + burn.Error + ")") : "a janela do teste de GPU foi fechada");
                        break;
                    }
                    if ((int)sw.Elapsed.TotalSeconds % 5 == 0)
                    {
                        HardwareErrors hardwareErrors = OcCheck.ErrorsSince(start);
                        if (hardwareErrors.Total > 0)
                        {
                            fail = $"o Windows registrou {hardwareErrors.Whea} erros WHEA e {hardwareErrors.GpuResets} reinícios do driver de vídeo";
                            break;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            stress.Stop();
            burn.Stop();
            _gpuBenchActive = false;
        }
        _ocRunning = false;
        BtnOcCheck.Content = "Iniciar verificação";
        BtnOcCheck.Style = (Style)FindResource("PrimaryButton");
        StackPanel ocTargetPanel2 = OcTargetPanel;
        flag = (OcDurPanel.IsEnabled = true);
        ocTargetPanel2.IsEnabled = flag;
        OcProgress.ScaleX = 0.0;
        if (ct.IsCancellationRequested && fail == null)
        {
            OcStatus.Text = "Verificação cancelada.";
            return;
        }
        HardwareErrors hardwareErrors2 = OcCheck.ErrorsSince(start);
        if (fail == null && hardwareErrors2.Total > 0)
        {
            fail = $"o Windows registrou {hardwareErrors2.Whea} erros WHEA e {hardwareErrors2.GpuResets} reinícios do driver de vídeo";
        }
        bool flag4 = fail == null;
        OcStatus.Text = $"Concluído em {DateTime.Now - start:mm\\:ss}.";
        OcVerdict.Text = (flag4 ? "ESTÁVEL" : "INSTÁVEL");
        OcVerdictWhy.Text = (flag4 ? $"Nenhum erro em {duration.TotalMinutes:0} min de stress. Para Curve Optimizer, use o PC normalmente por um dia e confira os erros recentes." : ("Motivo: " + fail + ". Volte um passo no ajuste e teste de novo."));
        OcVerdictCard.Background = (flag4 ? Card : Fg);
        TextBlock ocVerdict = OcVerdict;
        Brush foreground = (OcVerdictWhy.Foreground = (flag4 ? Fg : Ink));
        ocVerdict.Foreground = foreground;
        List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Configuração", label)
        };
        if (cpuScore.HasValue)
        {
            double valueOrDefault3 = cpuScore.GetValueOrDefault();
            list.Add(new KeyValuePair<string, string>("Nota CPU (render)", $"{valueOrDefault3:N0} pts"));
        }
        if (gpuScore.HasValue)
        {
            double valueOrDefault4 = gpuScore.GetValueOrDefault();
            list.Add(new KeyValuePair<string, string>("Nota GPU (cena)", $"{valueOrDefault4:N0} pts"));
        }
        if (maxCpu > 0f)
        {
            list.Add(new KeyValuePair<string, string>("Temp. máx. CPU", $"{maxCpu:0} °C"));
        }
        if (maxGpu > 0f)
        {
            list.Add(new KeyValuePair<string, string>("Temp. máx. GPU", $"{maxGpu:0} °C"));
        }
        if (nCpuW > 0)
        {
            list.Add(new KeyValuePair<string, string>("Consumo médio CPU", $"{sumCpuW / (double)nCpuW:0} W"));
        }
        if (nGpuW > 0)
        {
            list.Add(new KeyValuePair<string, string>("Consumo médio GPU", $"{sumGpuW / (double)nGpuW:0} W"));
        }
        list.Add(new KeyValuePair<string, string>("Erros WHEA / resets de vídeo", $"{hardwareErrors2.Whea} / {hardwareErrors2.GpuResets}"));
        OcResult.ItemsSource = list;
        OcResult.Foreground = (flag4 ? Fg : Ink);
        OcVerdictCard.Visibility = Visibility.Visible;
        string[] obj = new string[5]
        {
            flag4 ? "estável" : "INSTÁVEL",
            " · ",
            label,
            "",
            ""
        };
        object obj2;
        if (cpuScore.HasValue)
        {
            double valueOrDefault5 = cpuScore.GetValueOrDefault();
            obj2 = $" · CPU {valueOrDefault5:N0}";
        }
        else
        {
            obj2 = "";
        }
        obj[3] = (string)obj2;
        object obj3;
        if (gpuScore.HasValue)
        {
            double valueOrDefault6 = gpuScore.GetValueOrDefault();
            obj3 = $" · GPU {valueOrDefault6:N0}";
        }
        else
        {
            obj3 = "";
        }
        obj[4] = (string)obj3;
        string summary = string.Concat(obj);
        _settings.BenchRecords.Insert(0, new BenchRecord(DateTime.Now, "oc", flag4 ? 1.0 : 0.15, summary));
        _settings.Save();
        UpdateOcHistory();
        UpdateRecentErrors();
    }

    private void BuildOcGuide()
    {
        (string Maker, string Model) tuple = OcCheck.Motherboard();
        string item = tuple.Maker;
        string item2 = tuple.Model;
        bool flag = item.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase);
        bool flag2 = item.Contains("ASUS", StringComparison.OrdinalIgnoreCase);
        bool flag3 = item.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || item.Contains("MSI", StringComparison.OrdinalIgnoreCase);
        string text = GameCatalog.ShortCpuName(_cpuName);
        bool num = _cpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase);
        string text2 = ((flag ? "Gigabyte" : (item.Split(' ').FirstOrDefault() ?? "")) + " " + item2).Trim();
        OcGuideTitle.Text = "GUIA: OVERCLOCK NA BIOS · " + text2.ToUpperInvariant() + " · " + text.ToUpperInvariant();
        OcGuideIntro.Text = "Faça uma mudança por vez e rode o verificador depois de cada uma. Os menus podem mudar um pouco com a versão da BIOS. Valores são pontos de partida seguros, não garantia: cada chip é diferente.";
        string text3 = (flag ? "Tweaker → Advanced CPU Settings → Precision Boost Overdrive  (ou Settings → AMD Overclocking)" : (flag2 ? "Advanced → AMD Overclocking → Precision Boost Overdrive  (ou AI Tweaker → PBO)" : (flag3 ? "OC → Advanced CPU Configuration → AMD Overclocking → Precision Boost Overdrive" : "Advanced → AMD Overclocking → Precision Boost Overdrive")));
        string where = (flag ? "Tweaker → Extreme Memory Profile (X.M.P.) → EXPO1" : (flag2 ? "AI Tweaker → Ai Overclock Tuner → EXPO I" : (flag3 ? "OC → A-XMP/EXPO → Profile 1" : "procure EXPO / XMP no menu de memória")));
        List<GuideStep> list = new List<GuideStep>
        {
            new GuideStep("0", "Antes de começar", flag ? "Save & Exit → Save Profiles" : "Save & Exit → Save Profile", "Salve o perfil atual da BIOS. Se o PC não ligar depois de um ajuste, espere 3 tentativas (muitas placas voltam sozinhas) ou limpe a CMOS (jumper CLR_CMOS na placa ou tirando a bateria por 1 minuto)."),
            new GuideStep("1", "Ative o perfil da memória (EXPO)", where, "É o maior ganho em jogos para a maioria dos PCs: a RAM sai do padrão lento e vai para a velocidade da etiqueta. Teste: verificador \"CPU + RAM\" por 15 min (o teste de RAM acusa erro se o perfil não for estável).")
        };
        if (num)
        {
            list.Add(new GuideStep("2", "Precision Boost Overdrive (PBO)", text3, "Precision Boost Overdrive → Advanced. PBO Limits → Motherboard. Isso libera os limites de energia para o boost durar mais. Teste com o verificador."));
            list.Add(new GuideStep("3", "Curve Optimizer (undervolt da CPU)", text3 + " → Curve Optimizer", "All Cores → Negative → 10. Teste. Se passar, tente 15, 20, 25… até falhar; volte 5 a partir de onde falhou. No " + text + " costuma ficar entre -15 e -30. Instabilidade de Curve Optimizer aparece muito em uso leve: use o PC normalmente por um dia e veja \"Erros de hardware recentes\" aqui — erro WHEA = volte um passo."));
            list.Add(new GuideStep("4", "Max CPU Boost Clock Override (opcional)", text3, "Depois do Curve Optimizer estável: +100 MHz, teste; depois +200 MHz. Ganho pequeno; pode exigir reduzir o Curve Optimizer."));
            list.Add(new GuideStep("5", "Alternativa fria: Eco Mode", text3 + " → Eco Mode", "Em vez de mais desempenho: limita a CPU a 65 W (ou 105 W). Perde ~5–10% em tarefas pesadas multi-core, quase nada em jogos, e roda bem mais frio e silencioso."));
        }
        else
        {
            list.Add(new GuideStep("2", "Processador Intel", "Tweaker / Ai Tweaker / OC → CPU", "Para Intel, prefira undervolt (offset negativo de tensão) e limites de energia (PL1/PL2) em vez de aumentar multiplicadores. Teste cada passo com o verificador."));
        }
        OcGuide.ItemsSource = list;
    }

    // ================= Comparar placas de vídeo =================

    private List<GpuSpec>? _gpuSpecs;

    private void InitGpuCompare()
    {
        _gpuSpecs = GpuSpecDb.All().Select(x => x.Spec).ToList();
        var user = _gpus.Count > 0 ? _gpus[0].Name : null;
        int userIndex = user == null ? -1 : _gpuSpecs.FindIndex(s => user.Contains(s.DisplayName, StringComparison.OrdinalIgnoreCase));
        GpuCompareA.ItemsSource = _gpuSpecs;
        GpuCompareB.ItemsSource = _gpuSpecs;
        GpuCompareA.DisplayMemberPath = "DisplayName";
        GpuCompareB.DisplayMemberPath = "DisplayName";
        if (userIndex >= 0) GpuCompareA.SelectedIndex = userIndex;
    }

    private void GpuCompare_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_gpuSpecs == null) return;
        if (GpuCompareA.SelectedItem is not GpuSpec a || GpuCompareB.SelectedItem is not GpuSpec b) return;
        if (ReferenceEquals(a, b))
        {
            GpuComparePanel.Children.Clear();
            GpuComparePanel.Children.Add(new TextBlock
            {
                Text = "Escolha duas placas diferentes para comparar.",
                Foreground = (System.Windows.Media.Brush)FindResource("Muted"),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        BuildGpuCompare(a, b);
    }

    private void BuildGpuCompare(GpuSpec a, GpuSpec b)
    {
        string Mem(GpuSpec g) => g.VramGb > 0
            ? $"{g.VramGb} GB {g.MemType} · {g.MemBusBit} bits · {g.MemSpeedGbps:0.#} Gbps"
            : g.MemType;
        string Band(GpuSpec g) => g.BandwidthGBs > 0 ? $"{g.BandwidthGBs:0} GB/s" : "—";
        string Tech(GpuSpec g) => GpuSpecDb.ArchFeatures.TryGetValue(g.Arch, out var t) ? t : "—";
        int Winner(double x, double y) => x > y ? 1 : y > x ? 2 : 0;

        var rows = new (string Label, string A, string B, int Winner)[]
        {
            ("Chip", a.Chip, b.Chip, 0),
            ("Arquitetura", $"{a.Arch} · {a.Process}", $"{b.Arch} · {b.Process}", 0),
            ("Núcleos (shaders)", $"{a.Shaders:N0}", $"{b.Shaders:N0}", Winner(a.Shaders, b.Shaders)),
            ("ROPs / TMUs", $"{a.Rops} / {a.Tmus}", $"{b.Rops} / {b.Tmus}", Winner(a.Rops, b.Rops)),
            ("Clock (base → boost)", $"{a.BaseMHz} → {a.BoostMHz} MHz", $"{b.BaseMHz} → {b.BoostMHz} MHz", Winner(a.BoostMHz, b.BoostMHz)),
            ("Memória", Mem(a), Mem(b), Winner(a.BandwidthGBs, b.BandwidthGBs)),
            ("Larg. de banda (teórica)", Band(a), Band(b), Winner(a.BandwidthGBs, b.BandwidthGBs)),
            ("FP32 teórico", $"{a.Tflops:0.#} TFLOPS", $"{b.Tflops:0.#} TFLOPS", Winner(a.Tflops, b.Tflops)),
            ("Consumo (TDP)", a.TdpW > 0 ? $"{a.TdpW} W" : "integrada", b.TdpW > 0 ? $"{b.TdpW} W" : "integrada", 0),
            ("Lançamento", a.Release, b.Release, 0),
            ("Tecnologias", Tech(a), Tech(b), 0),
        };

        var muted = (System.Windows.Media.Brush)FindResource("Muted");
        var accent = (System.Windows.Media.Brush)FindResource("Accent");
        var faint = (System.Windows.Media.Brush)FindResource("Faint");
        var mono = (System.Windows.Media.FontFamily)FindResource("Mono");
        var panel = GpuComparePanel;
        panel.Children.Clear();

        // Cabeçalho: nomes das duas placas
        var head = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.Children.Add(HeadCell("", muted));
        head.Children.Add(HeadCell(a.DisplayName, accent, 1));
        head.Children.Add(HeadCell(b.DisplayName, accent, 2));
        panel.Children.Add(head);

        foreach (var (label, av, bv, winner) in rows)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(RowCell(label, faint, 0, null));
            g.Children.Add(RowCell(av, winner == 1 ? accent : muted, 1, winner == 1 ? mono : null, winner == 1));
            g.Children.Add(RowCell(bv, winner == 2 ? accent : muted, 2, winner == 2 ? mono : null, winner == 2));
            var border = new Border { BorderBrush = (System.Windows.Media.Brush)FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 8) };
            border.Child = g;
            panel.Children.Add(border);
        }
    }

    private static TextBlock HeadCell(string text, System.Windows.Media.Brush brush, int column = 0)
    {
        var t = new TextBlock
        {
            Text = text, Foreground = brush, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        System.Windows.Controls.Grid.SetColumn(t, column);
        return t;
    }

    private static TextBlock RowCell(string text, System.Windows.Media.Brush brush, int column,
        System.Windows.Media.FontFamily? font = null, bool bold = false)
    {
        var t = new TextBlock
        {
            Text = text, Foreground = brush, FontSize = 12, TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        };
        if (font != null) t.FontFamily = font;
        System.Windows.Controls.Grid.SetColumn(t, column);
        return t;
    }
}
