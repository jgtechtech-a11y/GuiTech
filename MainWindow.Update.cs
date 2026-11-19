using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GuiTech;

/// <summary>
/// Aviso de versão nova no app: verifica nas Releases do GitHub (uma vez por dia, em background)
/// e, se existir versão mais nova, mostra o banner no topo da Visão geral + um toast.
/// Na versão instalada, o botão baixa o Setup, confere o SHA-256 e executa o instalador
/// (atualização em um clique); na versão portátil, abre a página da release no navegador.
/// </summary>
public partial class MainWindow
{
    private UpdateChecker.Info? _update;
    private bool _updating;
    private bool _installed;

    private async void CheckUpdate()
    {
        var info = await UpdateChecker.CheckAsync();
        if (info == null || !info.HasUpdate) return;
        await Dispatcher.InvokeAsync(() => ShowUpdateBanner(info));
    }

    private void ShowUpdateBanner(UpdateChecker.Info info)
    {
        _update = info;
        _installed = UpdateInstaller.IsInstalled();

        string version = info.Version.TrimStart('v', 'V');
        UpdateTitle.Text = $"Versão {version} disponível";
        UpdateNotes.Text = string.IsNullOrWhiteSpace(info.Notes) ? "" : info.Notes;

        // Sem instalação (rodando o exe solto), baixar o Setup instalaria uma cópia nova em
        // Program Files — nesse caso o botão continua abrindo a release no navegador.
        if (!_installed)
        {
            BtnUpdate.Content = "Baixar no navegador";
            BtnUpdateBrowser.Visibility = Visibility.Collapsed;
        }

        UpdateBanner.Visibility = Visibility.Visible;
        Toast("Atualização disponível", $"A versão {version} do Gui_Tech já está disponível.", "\uE895");
    }

    private void BtnUpdateBrowser_Click(object sender, RoutedEventArgs e) => OpenReleasePage();

    private void OpenReleasePage()
    {
        if (string.IsNullOrEmpty(_update?.Url)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _update.Url, UseShellExecute = true });
        }
        catch { }
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_update == null || _updating) return;

        if (!_installed)
        {
            OpenReleasePage();
            return;
        }

        _updating = true;
        BtnUpdate.IsEnabled = false;
        BtnUpdateBrowser.IsEnabled = false;
        BtnUpdate.Content = "Baixando...";
        UpdateStatus.Visibility = Visibility.Visible;
        UpdateProgressTrack.Visibility = Visibility.Visible;
        UpdateProgress.ScaleX = 0;
        UpdateStatus.Text = "Baixando o instalador...";

        var progress = new Progress<double>(p =>
        {
            double value = Math.Clamp(p, 0, 1);
            UpdateProgress.ScaleX = value;
            UpdateStatus.Text = $"Baixando o instalador... {value * 100:0}%";
        });

        try
        {
            string setup = await UpdateInstaller.DownloadSetupAsync(_update.Version, progress);

            UpdateProgress.ScaleX = 1;
            UpdateStatus.Text = "Baixado e conferido. Abrindo o instalador...";

            // /AUTO: o instalador reabre o Gui_Tech no fim (ver [Code] do instalador.iss).
            // /CLOSEAPPLICATIONS + /NORESTART: fecha o app (que sai logo abaixo) e não reinicia o PC.
            var start = new ProcessStartInfo
            {
                FileName = setup,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /NORESTART /AUTO",
                UseShellExecute = true,
            };

            Process.Start(start);
            await Task.Delay(400);   // dá tempo de o instalador iniciar antes de fechar o app
            Application.Current.Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // UAC cancelado: não é falha do download, então basta liberar para tentar de novo.
            RestoreUpdateButton();
            UpdateStatus.Text = "Atualização cancelada. Nada foi alterado.";
        }
        catch (Exception ex)
        {
            RestoreUpdateButton();
            UpdateTitle.Text = "Não foi possível atualizar agora";
            UpdateProgressTrack.Visibility = Visibility.Collapsed;
            UpdateStatus.Text = ex.Message + " Você pode baixar pelo navegador.";
        }
    }

    private void RestoreUpdateButton()
    {
        _updating = false;
        BtnUpdate.IsEnabled = true;
        BtnUpdateBrowser.IsEnabled = true;
        BtnUpdate.Content = "Tentar de novo";
    }
}
