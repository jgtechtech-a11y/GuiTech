using System.Windows;

namespace GuiTech;

/// <summary>
/// Gui_Tech OTIMIZA · MODO JOGO e MODO TRABALHO: perfis de uma tecla que combinam energia,
/// ajustes do Windows, programas em segundo plano e limpeza de memória.
/// </summary>
public partial class MainWindow
{
    private bool _modeBusy;

    // ------------------------------------------------------------ MODO JOGO

    private void BtnGameMode_Click(object sender, RoutedEventArgs e)
    {
        if (_modeBusy) return;

        // Fecha apenas o que está sem janela: nada de derrubar um documento aberto sem salvar.
        var background = _apps.Where(a => a.IsBackground).ToList();
        string list = background.Count == 0
            ? "nenhum programa em segundo plano agora"
            : string.Join("\n", background.Take(10).Select(a => $"  • {a.Title} ({a.MemoryText})"))
              + (background.Count > 10 ? $"\n  … e mais {background.Count - 10}" : "");

        var answer = MessageBox.Show(this,
            "O MODO JOGO vai:\n\n" +
            "1. Ligar o plano de energia Alto desempenho.\n" +
            "2. Ligar o Modo de Jogo do Windows e desligar a gravação em segundo plano (Game DVR).\n" +
            "3. Fechar os programas que estão rodando em segundo plano, sem janela.\n" +
            "4. Limpar a memória em espera e enxugar a RAM de todos os programas.\n\n" +
            $"Programas que serão fechados:\n{list}\n\n" +
            "O que tem janela aberta não é tocado. Continuar?",
            "Gui_Tech · MODO JOGO", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        RunGameMode(background);
    }

    private async void RunGameMode(List<BackgroundApp> background)
    {
        _modeBusy = true;
        BtnGameMode.IsEnabled = false;
        BtnWorkMode.IsEnabled = false;
        BtnOptimize.IsEnabled = false;
        BtnGameMode.Content = "Aplicando…";
        OptResult.Text = "Aplicando o MODO JOGO…";

        var notes = new List<string>();
        try
        {
            // Ajustes do Windows (mesmos da lista de interruptores, com backup do valor original).
            var applied = await Task.Run(() =>
            {
                var ok = new List<string>();
                foreach (string id in new[] { "power", "gamemode", "gamedvr" })
                {
                    var t = _tweaks.FirstOrDefault(x => x.Id == id);
                    if (t == null) continue;
                    try
                    {
                        t.Write(true, _settings);
                        t.Refresh();
                        if (t.IsOn) ok.Add(t.Title);
                    }
                    catch { }
                }
                return ok;
            });
            _settings.Save();
            notes.Add(applied.Count > 0 ? "ligado: " + string.Join(", ", applied)
                                        : "não foi possível mudar os ajustes do Windows");

            if (background.Count > 0)
            {
                var closed = await Task.Run(() => Optimizer.CloseApps(background));
                if (closed.Closed.Count > 0) notes.Add($"fechados: {string.Join(", ", closed.Closed)}");
                if (closed.NotClosed.Count > 0) notes.Add($"não fecharam: {string.Join(", ", closed.NotClosed)}");
            }
            else notes.Add("nada em segundo plano para fechar");

            var mem = await Task.Run(Optimizer.CleanMemory);
            notes.Add($"memória liberada: {mem.FreedBytes / 1048576.0:N0} MB" +
                      (mem.StandbyPurged ? "" : $" (memória em espera não limpa: {mem.StandbyError})") +
                      (mem.FileCacheCleared ? " · cache de arquivos do sistema limpo" : ""));
            notes.Add("abra o jogo agora");

            OptResult.Text = "MODO JOGO aplicado · " + string.Join(" · ", notes) + ".";
        }
        catch (Exception ex)
        {
            OptResult.Text = "O MODO JOGO falhou: " + ex.Message;
        }
        finally
        {
            BtnGameMode.Content = "Modo jogo";
            BtnGameMode.IsEnabled = true;
            BtnWorkMode.IsEnabled = true;
            BtnOptimize.IsEnabled = true;
            _modeBusy = false;
            RefreshApps();
            UpdateRamStatus();
        }
    }

    // ------------------------------------------------------------ MODO TRABALHO

    private async void BtnWorkMode_Click(object sender, RoutedEventArgs e)
    {
        if (_modeBusy) return;
        var answer = MessageBox.Show(this,
            "O MODO TRABALHO vai:\n\n" +
            "1. Voltar o plano de energia para Equilibrado (menos calor e mais bateria, sem perder resposta).\n" +
            "2. Limpar a memória em espera e enxugar a RAM dos programas abertos — bom para quem tem\n" +
            "   muitas abas do navegador e vários programas abertos ao mesmo tempo.\n\n" +
            "Nada é fechado: suas abas e programas continuam abertos. Continuar?",
            "Gui_Tech · MODO TRABALHO", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        _modeBusy = true;
        BtnGameMode.IsEnabled = false;
        BtnWorkMode.IsEnabled = false;
        BtnOptimize.IsEnabled = false;
        BtnWorkMode.Content = "Aplicando…";
        OptResult.Text = "Aplicando o MODO TRABALHO…";

        try
        {
            var plan = await Task.Run(() =>
            {
                try
                {
                    // Usa o mesmo interruptor do plano de energia: guarda o plano atual para "Restaurar originais".
                    var t = _tweaks.FirstOrDefault(x => x.Id == "power");
                    if (t != null) t.Write(false, _settings);
                    else WindowsTweaks.UseBalanced();
                    return true;
                }
                catch { return false; }
            });
            _settings.Save();
            _tweaks.FirstOrDefault(t => t.Id == "power")?.Refresh();

            var mem = await Task.Run(Optimizer.CleanMemory);
            var (total, avail) = Optimizer.MemoryStatus();
            double freeGb = avail / 1073741824.0, totalGb = total / 1073741824.0;

            OptResult.Text = "MODO TRABALHO aplicado · " +
                             (plan ? "plano de energia Equilibrado" : "não foi possível trocar o plano de energia") +
                             $" · memória liberada: {mem.FreedBytes / 1048576.0:N0} MB ({mem.Trimmed} programas enxugados)" +
                             (total > 0 ? $" · {freeGb:0.0} GB livres de {totalGb:0.0} GB" : "") + ".";
        }
        catch (Exception ex)
        {
            OptResult.Text = "O MODO TRABALHO falhou: " + ex.Message;
        }
        finally
        {
            BtnWorkMode.Content = "Modo trabalho";
            BtnGameMode.IsEnabled = true;
            BtnWorkMode.IsEnabled = true;
            BtnOptimize.IsEnabled = true;
            _modeBusy = false;
            UpdateRamStatus();
        }
    }
}
