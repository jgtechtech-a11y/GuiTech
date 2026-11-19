using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GuiTech;

/// <summary>
/// Avisos automáticos de hardware: o timer de 1 segundo alimenta as temperaturas e, em
/// intervalos maiores, a bateria e os discos são relidos em segundo plano. O que avisar fica
/// em <see cref="HardwareAlerts"/>; aqui só se mostra o toast e se guarda o estado de tempo.
/// </summary>
public partial class MainWindow
{
    private DateTime _alertsBatteryAt = DateTime.MinValue;
    private DateTime _alertsDiskAt = DateTime.MinValue;

    /// <summary>Chamado 1x por segundo pelo timer principal (já na thread da interface).</summary>
    private void CheckHardwareAlerts()
    {
        if (!_settings.HardwareAlerts || !_monitor.IsOpen) return;

        foreach (var a in HardwareAlerts.Sample(_monitor.CpuTemperature, MainGpuTemp(), DateTime.Now))
            Toast(a.Title, a.Text, a.Icon);

        // Bateria: lê o WMI a cada 5 min (muda devagar e a leitura não é gratuita).
        if (DateTime.Now - _alertsBatteryAt > TimeSpan.FromMinutes(5))
        {
            _alertsBatteryAt = DateTime.Now;
            var sensors = _monitor.Rows.ToList();
            _ = Task.Run(() => BatteryInfo.Read(sensors))
                .ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion) return;
                    var wear = t.Result.Where(c => c.Wear != null).Select(c => c.Wear).Max();
                    foreach (var a in HardwareAlerts.CheckBattery(wear, DateTime.Now))
                        Dispatcher.BeginInvoke(new Action(() => Toast(a.Title, a.Text, a.Icon)));
                });
        }

        // Discos: saúde e temperatura mudam devagar; relê a cada 30 min em segundo plano.
        if (DateTime.Now - _alertsDiskAt > TimeSpan.FromMinutes(30))
        {
            _alertsDiskAt = DateTime.Now;
            var sensors = _monitor.Rows.ToList();
            _ = Task.Run(() => DiskHealth.Read(sensors))
                .ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion) return;
                    var samples = t.Result.Select(c => new DiskSample(c.Title, c.Verdict, c.TempC, c.Wear)).ToList();
                    foreach (var a in HardwareAlerts.CheckDisks(samples, DateTime.Now))
                        Dispatcher.BeginInvoke(new Action(() => Toast(a.Title, a.Text, a.Icon)));
                });
        }
    }

    /// <summary>Liga/desliga os avisos pelo painel da Visão geral.</summary>
    private void AlertsToggle_Changed(object sender, RoutedEventArgs e)
    {
        _settings.HardwareAlerts = AlertsToggle.IsChecked == true;
        _settings.Save();
        AlertsHint.Text = _settings.HardwareAlerts
            ? "Avisos ligados: temperatura, disco e bateria."
            : "Avisos desligados.";
    }
}
