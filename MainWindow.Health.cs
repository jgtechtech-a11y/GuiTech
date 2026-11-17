using System.Windows;

namespace GuiTech;

/// <summary>Tela inicial · VERIFICAR PC: roda a verificação completa e mostra o resultado na própria página.</summary>
public partial class MainWindow
{
    private bool _pcCheckBusy;
    private bool _pcFixBusy;
    private PcCheckReport? _pcLastReport;

    /// <summary>VERIFICAR PC é só leitura: nada é apagado nem alterado sem o usuário pedir.</summary>
    private async void BtnPcCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_pcCheckBusy || _pcFixBusy) return;
        _pcCheckBusy = true;
        BtnPcCheck.IsEnabled = false;
        BtnPcFix.IsEnabled = false;
        BtnPcCheck.Content = "Verificando…";
        PcCheckProgress.ScaleX = 0;
        PcCheckCard.Visibility = Visibility.Visible;
        PcCheckSummary.Text = "";
        PcCheckList.ItemsSource = null;
        PcCheckStatus.Text = "Começando a verificação…";
        PcFixStatus.Text = "";

        var progress = new Progress<(string Text, double Fraction)>(p =>
        {
            PcCheckStatus.Text = p.Text;
            PcCheckProgress.ScaleX = Math.Clamp(p.Fraction, 0, 1);
        });

        try
        {
            var report = await Task.Run(() => PcCheck.Run((text, fraction) =>
                ((IProgress<(string, double)>)progress).Report((text, fraction))));

            PcCheckList.ItemsSource = report.Items;
            PcCheckSummary.Text = report.Summary;
            PcCheckStatus.Text = report.Headline +
                (report.SuggestionCount > 0 || report.WarningCount > 0 ? " Clique em CORRIGIR para aplicar o que é seguro." : "");
            PcCheckProgress.ScaleX = 1;
            _pcLastReport = report;
            PcCheckCard.BringIntoView();
        }
        catch (Exception ex)
        {
            PcCheckStatus.Text = "A verificação falhou: " + ex.Message;
        }
        finally
        {
            try
            {
                BtnPcCheck.IsEnabled = true;
                BtnPcFix.IsEnabled = true;
                BtnPcCheck.Content = "Verificar PC";
            }
            catch { }
            _pcCheckBusy = false;
        }
    }

    /// <summary>
    /// CORRIGIR: aplica todas as correções seguras de uma vez e depois confere o resultado.
    /// A barra vai de 0 a 55% aplicando e de 55% a 100% conferindo — nunca anda para trás.
    /// </summary>
    private async void BtnPcFix_Click(object sender, RoutedEventArgs e)
    {
        if (_pcFixBusy || _pcCheckBusy) return;
        _pcFixBusy = true;
        BtnPcFix.IsEnabled = false;
        BtnPcCheck.IsEnabled = false;
        BtnPcFix.Content = "Corrigindo…";
        PcFixStatus.Text = "Aplicando as correções seguras…";
        PcCheckProgress.ScaleX = 0;

        var progress = new Progress<(string Text, double Fraction)>(p =>
        {
            PcFixStatus.Text = p.Text;
            PcCheckProgress.ScaleX = Math.Clamp(p.Fraction, 0, 1);
        });
        void Send(string text, double fraction) => ((IProgress<(string, double)>)progress).Report((text, fraction));

        PcFixReport? fix = null;
        try
        {
            fix = await Task.Run(() => PcCheck.Fix(_settings, (text, fraction) => Send(text, fraction * 0.55)));
        }
        catch (Exception ex)
        {
            PcFixStatus.Text = "Não foi possível aplicar as correções: " + ex.Message;
        }

        if (fix != null)
        {
            // Confere o estado depois das correções. Sem CHKDSK: a correção não mexe em sistema de
            // arquivos e a verificação lenta era o motivo de o botão parecer que nunca terminava.
            try
            {
                Send("Conferindo o resultado…", 0.56);
                var applied = fix;
                var report = await Task.Run(() =>
                {
                    var r = PcCheck.Run(
                        (text, fraction) => Send(text, 0.55 + fraction * 0.45), includeFileSystem: false);
                    // Marca CORRIGIDO o que realmente mudou, com o antes → depois medido.
                    PcCheck.ApplyFixResult(r, _pcLastReport, applied);
                    return r;
                });

                PcCheckList.ItemsSource = null;
                PcCheckList.ItemsSource = report.Items;
                PcCheckSummary.Text = report.Summary;
                PcCheckStatus.Text = report.FixedCount > 0
                    ? $"Corrigi {report.FixedCount} ponto(s) agora — o antes e o depois está na lista." +
                      (report.WarningCount > 0 ? $" {report.WarningCount} ponto(s) de atenção precisam de você."
                       : report.SuggestionCount > 0 ? $" Ainda dá para melhorar em {report.SuggestionCount} ponto(s)."
                       : " Nada urgente sobrou.")
                    : "Não havia nada seguro para corrigir automaticamente agora.";
                PcFixStatus.Text = BuildFixStatus(fix, report);
                _pcLastReport = report;
                PcCheckCard.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // As correções já foram aplicadas: avisa isso e o erro de conferência separadamente.
                PcFixStatus.Text = BuildFixStatus(fix, null) +
                                   " Não foi possível conferir o resultado agora: " + ex.Message;
            }
        }

        PcCheckProgress.ScaleX = 1;
        try
        {
            BtnPcFix.IsEnabled = true;
            BtnPcCheck.IsEnabled = true;
            BtnPcFix.Content = "Corrigir";
        }
        catch { }
        _pcFixBusy = false;
    }

    /// <summary>Resumo do que foi corrigido e do que ainda depende do usuário.</summary>
    private static string BuildFixStatus(PcFixReport fix, PcCheckReport? report)
    {
        var lines = new List<string>();
        if (fix.Done.Count > 0) lines.Add("Aplicado agora: " + string.Join("; ", fix.Done) + ".");
        else lines.Add(fix.Summary);
        if (fix.Skipped.Count > 0) lines.Add("Ficou de fora: " + string.Join("; ", fix.Skipped).TrimEnd('.') + ".");
        if (report != null)
        {
            var manual = PcCheck.ManualOnly(report);
            if (manual.Count > 0) lines.Add("Precisa de você: " + string.Join("; ", manual) + ".");
        }
        return string.Join("\n", lines);
    }
}
