using System.Diagnostics;
using System.Windows;

namespace GuiTech;

/// <summary>
/// Aviso de versão nova no app: verifica nas Releases do GitHub (uma vez por dia, em background)
/// e, se existir versão mais nova, mostra o banner no topo da Visão geral + um toast.
/// </summary>
public partial class MainWindow
{
    private UpdateChecker.Info? _update;

    private async void CheckUpdate()
    {
        var info = await UpdateChecker.CheckAsync();
        if (info == null || !info.HasUpdate) return;
        await Dispatcher.InvokeAsync(() => ShowUpdateBanner(info));
    }

    private void ShowUpdateBanner(UpdateChecker.Info info)
    {
        _update = info;
        UpdateTitle.Text = $"Versão {info.Version.TrimStart('v')} disponível";
        UpdateNotes.Text = string.IsNullOrWhiteSpace(info.Notes) ? "" : info.Notes;
        UpdateBanner.Visibility = Visibility.Visible;
        Toast("Atualização disponível", $"A versão {info.Version.TrimStart('v')} do Gui_Tech já está disponível.", "\uE895");
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_update?.Url)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _update.Url, UseShellExecute = true });
        }
        catch { }
    }
}
