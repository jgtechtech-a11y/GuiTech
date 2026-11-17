using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GuiTech;

/// <summary>
/// Aba CENTRAL DE DRIVERS: inventário, NVIDIA/AMD (consulta de versão e link oficial), placa-mãe,
/// Windows Update, backup e ponto de restauração. Nada é baixado automaticamente pelo app.
/// </summary>
public partial class MainWindow
{
    private bool _drvInit, _drvBusy, _drvDownloading;
    private List<GpuDriverStatus> _drvGpus = new();
    private BoardInfo _drvBoardInfo = new();
    private List<WuDriver> _drvWu = new();
    private CancellationTokenSource? _drvCts;
    /// <summary>Instaladores já baixados nesta sessão, por link (sobrevive a um novo VERIFICAR TUDO).</summary>
    private readonly Dictionary<string, string> _drvInstallers = new(StringComparer.OrdinalIgnoreCase);

    private void InitDriver()
    {
        if (_drvInit) return;
        _drvInit = true;
        BtnDrvScan_Click(this, new RoutedEventArgs());
    }

    private void DrvSet(string text, double? frac = null)
    {
        DrvStatus.Text = text;
        if (frac.HasValue) DrvProgress.ScaleX = Math.Clamp(frac.Value, 0, 1);
    }

    private void DrvBusy(bool busy)
    {
        _drvBusy = busy;
        BtnDrvScan.IsEnabled = !busy;
        BtnDrvUpdate.IsEnabled = !busy;
        BtnDrvBackup.IsEnabled = !busy;
        BtnDrvRestore.IsEnabled = !busy;
        BtnDrvWuSearch.IsEnabled = !busy;
        BtnDrvWuInstall.IsEnabled = !busy && _drvWu.Count > 0;
        // Enquanto baixa, o próprio botão vira CANCELAR (fica habilitado).
        BtnDrvDownload.IsEnabled = _drvDownloading || (!busy && _drvGpus.Any(g => g.DownloadUrl.Length > 0));
        BtnDrvInstall.IsEnabled = !busy && _drvGpus.Any(g => g.DownloadedPath.Length > 0);
    }

    private void RefreshGpus()
    {
        DrvGpus.ItemsSource = null;
        DrvGpus.ItemsSource = _drvGpus;
        BtnDrvInstall.IsEnabled = !_drvBusy && _drvGpus.Any(g => g.DownloadedPath.Length > 0);
    }

    /// <summary>Reata instaladores já baixados depois de uma nova leitura da placa.</summary>
    private void AttachDownloaded()
    {
        foreach (var g in _drvGpus)
        {
            if (g.DownloadUrl.Length == 0) continue;
            if (_drvInstallers.TryGetValue(g.DownloadUrl, out var path) && File.Exists(path))
                g.DownloadedPath = path;
            else if (DriverManager.ExistingInstaller(g) is string found)
            {
                g.DownloadedPath = found;
                _drvInstallers[g.DownloadUrl] = found;
            }
        }
    }

    private async void BtnDrvScan_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy) return;
        DrvBusy(true);
        _drvCts?.Dispose();
        _drvCts = new CancellationTokenSource();
        var ct = _drvCts.Token;
        try
        {
            DriverManager.ReadErrors.Clear();

            DrvSet("Lendo drivers instalados…", 0.1);
            var inv = await Task.Run(DriverManager.Inventory, ct);
            DrvList.ItemsSource = inv;

            DrvSet("Lendo placa de vídeo e placa-mãe…", 0.3);
            _drvGpus = await Task.Run(DriverManager.ReadGpus, ct);
            _drvBoardInfo = await Task.Run(DriverManager.ReadBoard, ct);
            ShowBoard();

            bool studio = DrvStudio.IsChecked == true;
            int i = 0;
            foreach (var g in _drvGpus)
            {
                DrvSet($"Consultando {g.Vendor.ToUpperInvariant()} para {g.GpuName}…", 0.4 + 0.5 * i / Math.Max(1, _drvGpus.Count));
                if (g.Vendor == "NVIDIA") await DriverManager.CheckNvidiaAsync(g, studio, ct);
                else if (g.Vendor == "AMD") await DriverManager.CheckAmdAsync(g, ct);
                else if (g.Vendor == "Intel") g.Message = $"{g.Vendor}: use a página oficial para ver a versão mais recente (instalado: {g.Installed}).";
                else g.Message = "Fabricante não reconhecido.";
                i++;
            }
            DrvGpus.ItemsSource = null;
            DrvGpus.ItemsSource = _drvGpus;
            AttachDownloaded();
            RefreshGpus();

            string extra = DriverManager.ReadErrors.Count > 0
                ? " Não foi possível ler: " + string.Join("; ", DriverManager.ReadErrors) + "."
                : "";
            DrvSet($"Pronto às {DateTime.Now:HH:mm:ss}.{extra}", 1);
        }
        catch (OperationCanceledException) { DrvSet("Verificação cancelada.", 0); }
        catch (Exception ex) { DrvSet("Falhou: " + ex.Message, 0); }
        finally
        {
            _drvCts?.Dispose();
            _drvCts = null;
            DrvBusy(false);
        }
    }

    /// <summary>Trocar Game Ready/Studio só reconsulta a NVIDIA, sem refazer o inventário inteiro.</summary>
    private async void DrvStudio_Changed(object sender, RoutedEventArgs e)
    {
        if (_drvBusy || _drvGpus.Count == 0) return;
        DrvBusy(true);
        try
        {
            bool studio = DrvStudio.IsChecked == true;
            DriverManager.ReadErrors.Clear();
            foreach (var g in _drvGpus.Where(g => g.Vendor == "NVIDIA"))
            {
                DrvSet($"Consultando a versão {(studio ? "Studio" : "Game Ready")} na NVIDIA…", 0.5);
                await DriverManager.CheckNvidiaAsync(g, studio, CancellationToken.None);
            }
            DrvGpus.ItemsSource = null;
            DrvGpus.ItemsSource = _drvGpus;
            RefreshGpus();
            DrvSet(studio ? "Mostrando a versão Studio (criação)." : "Mostrando a versão Game Ready.", 1);
        }
        catch (Exception ex) { DrvSet("Falhou ao consultar a NVIDIA: " + ex.Message, 0); }
        finally { DrvBusy(false); }
    }

    private void ShowBoard()
    {
        var b = _drvBoardInfo;
        DrvBoard.Text = $"{b.Maker} {b.Model}\nBIOS: {(b.Bios.Length > 0 ? b.Bios : "—")}" +
                        (b.Brand.Length > 0 ? $"\nFabricante reconhecido: {b.Brand}" : "\nFabricante não reconhecido");
        BtnDrvBoardPage.IsEnabled = b.SupportUrl.Length > 0;
    }

    /// <summary>Abre um link no navegador; se falhar, avisa na tela em vez de não fazer nada.</summary>
    private bool Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) { DrvSet("Este link não está disponível agora."); return false; }
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            DrvSet("Não foi possível abrir o link: " + ex.Message);
            return false;
        }
    }

    private void BtnDrvGpuPage_Click(object sender, RoutedEventArgs e)
    {
        var g = _drvGpus.FirstOrDefault(x => x.OfficialPage.Length > 0 && x.Vendor != "Intel") ?? _drvGpus.FirstOrDefault(x => x.OfficialPage.Length > 0);
        Open(g?.OfficialPage ?? "https://www.nvidia.com/Download/index.aspx");
    }

    /// <summary>
    /// Baixa o instalador oficial do fabricante (com cancelamento), confere a assinatura digital e
    /// deixa pronto para INSTALAR AGORA. Nada é executado sem o usuário mandar.
    /// </summary>
    private async void BtnDrvDownload_Click(object sender, RoutedEventArgs e)
    {
        // Segundo clique durante o download = cancelar.
        if (_drvDownloading) { _drvCts?.Cancel(); DrvSet("Cancelando o download…"); return; }
        if (_drvBusy) return;

        var g = _drvGpus.FirstOrDefault(x => x.DownloadUrl.Length > 0);
        if (g == null)
        {
            DrvSet("Esta placa não tem instalador automático disponível. Use ABRIR PÁGINA OFICIAL.");
            return;
        }

        DrvBusy(true);
        _drvDownloading = true;
        _drvCts?.Dispose();
        _drvCts = new CancellationTokenSource();
        var ct = _drvCts.Token;

        var progress = new Progress<(long Received, long? Total)>(p =>
        {
            double frac = p.Total is long t && t > 0 ? (double)p.Received / t : 0;
            DrvProgress.ScaleX = Math.Clamp(frac, 0, 1);
            string size = $"{p.Received / 1048576.0:N0} MB" + (p.Total is long tt ? $" de {tt / 1048576.0:N0} MB" : "");
            DrvSet($"Baixando o instalador oficial ({size})… clique em CANCELAR DOWNLOAD para parar.", frac);
        });

        try
        {
            DrvSet("Consultando o instalador no site do fabricante…", 0);
            var probe = await DriverManager.ProbeInstallerAsync(g, ct);
            if (!probe.Ok)
            {
                DrvSet(probe.Error ?? "Não foi possível consultar o instalador.", 0);
                return;
            }

            string sizeText = probe.Size is long size ? $"{size / 1048576.0:N0} MB" : "tamanho desconhecido";
            string what = $"{g.Vendor} {g.Latest}".Trim();
            var answer = MessageBox.Show(this,
                    $"Baixar o instalador oficial {what}?\n\n" +
                    $"Tamanho: {sizeText}.\n" +
                    "Vai para Documentos\\GuiTech-Drivers\\Instaladores.\n\n" +
                    "O download pode levar vários minutos e consome internet. Continuar?",
                    "CENTRAL DE DRIVERS", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                DrvSet("Download cancelado. Nada foi baixado.", 0);
                return;
            }

            DrvSet($"Baixando o instalador oficial ({sizeText})… clique em CANCELAR DOWNLOAD para parar.", 0);
            BtnDrvDownload.Content = "Cancelar download";
            BtnDrvDownload.IsEnabled = true;
            var r = await DriverManager.DownloadInstallerAsync(g, progress, ct);
            if (r.Ok)
            {
                g.DownloadedPath = r.Path;
                _drvInstallers[g.DownloadUrl] = r.Path;
                DrvSet(r.Message + " Clique em INSTALAR AGORA quando quiser.", 1);
                DrvWuList.Text = $"Instalador oficial: {r.Path}\nSHA-256: {r.Sha256}\nAssinado por: {r.Signer}";
            }
            else
            {
                DrvSet(r.Message, r.Canceled ? 0 : 1);
            }
            RefreshGpus();
        }
        catch (Exception ex) { DrvSet("O download falhou: " + ex.Message, 0); }
        finally
        {
            _drvDownloading = false;
            BtnDrvDownload.Content = "Baixar instalador oficial";
            _drvCts?.Dispose();
            _drvCts = null;
            DrvBusy(false);
        }
    }

    /// <summary>Executa o instalador baixado — interativo ou silencioso, conforme a caixa marcada.</summary>
    private void BtnDrvInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy || !RequireAdmin()) return;
        var g = _drvGpus.FirstOrDefault(x => x.DownloadedPath.Length > 0);
        if (g == null) { DrvSet("Nenhum instalador baixado nesta sessão. Use BAIXAR INSTALADOR OFICIAL."); return; }

        bool quiet = DrvQuiet.IsChecked == true;
        string what = $"{g.Vendor} {g.Latest}".Trim();
        bool silent = quiet && g.Vendor is "NVIDIA" or "AMD";
        string text = silent
            ? $"Instalar {what} em modo silencioso?\n\nA tela pode piscar e o PC pode REINICIAR sozinho no fim. " +
              "Salve seus arquivos e feche jogos antes de continuar."
            : $"Abrir o instalador oficial de {what}?\n\nSiga as telas do fabricante. Salve seus arquivos e feche " +
              "jogos antes; o PC pode reiniciar no fim da instalação.";

        if (MessageBox.Show(this, text, "CENTRAL DE DRIVERS", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DrvSet(DriverManager.RunInstaller(g.DownloadedPath, g.Vendor, silent), 1);
    }

    private void BtnDrvBoardPage_Click(object sender, RoutedEventArgs e) => Open(_drvBoardInfo.SupportUrl);

    private void BtnDrvLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string url) Open(url);
    }

    private async void BtnDrvWuSearch_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy) return;
        DrvBusy(true);
        DrvSet("Procurando drivers no Windows Update (pode levar 1 minuto)…", 0.3);
        try
        {
            var found = await DriverManager.SearchWindowsUpdateAsync();
            _drvWu = found.Drivers;
            if (_drvWu.Count == 0)
            {
                DrvWuList.Text = found.Ignored > 0
                    ? "O catálogo só tinha pacote(s) de extensão — isso não é driver de dispositivo:\n" +
                      $"  • {found.IgnoredSample}\nNada para instalar aqui."
                    : "O Windows Update não tem driver novo nem driver faltando para este PC.";
                DrvSet("Nenhum driver de dispositivo no Windows Update.", 1);
            }
            else
            {
                DrvWuList.Text = string.Join("\n", _drvWu.Select(w =>
                    $"• {w.Title} ({w.SizeText})" + (w.Manufacturer.Length > 0 ? $" — {w.Manufacturer}" : "")));
                DrvSet($"{_drvWu.Count} driver(s) encontrados no Windows Update.", 1);
            }
        }
        catch (Exception ex) { DrvWuList.Text = "Falhou: " + ex.Message; DrvSet("Falhou: " + ex.Message, 0); }
        finally { DrvBusy(false); }
    }

    private async void BtnDrvWuInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy || _drvWu.Count == 0 || !RequireAdmin()) return;
        if (MessageBox.Show(this, $"Baixar e instalar {_drvWu.Count} driver(s) do Windows Update? Pode ser preciso reiniciar.", "CENTRAL DE DRIVERS",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DrvBusy(true);
        try
        {
            DrvWuList.Text = "Baixando e instalando…";
            string r = await DriverManager.InstallWindowsUpdateAsync(_drvWu, t => Dispatcher.BeginInvoke(new Action(() =>
                DrvSet(t, t.StartsWith("Instalando", StringComparison.OrdinalIgnoreCase) ? 0.7 : 0.35))));
            DrvSet(r, 1);
            DrvWuList.Text = r;
            _drvWu = new();
        }
        catch (Exception ex) { DrvWuList.Text = "Falhou: " + ex.Message; DrvSet("Falhou: " + ex.Message, 0); }
        finally { DrvBusy(false); }
    }

    private async void BtnDrvUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy || !RequireAdmin()) return;
        var answer = MessageBox.Show(this,
            "O ATUALIZAR DRIVERS faz tudo de uma vez:\n\n" +
            "1. Procura no Windows Update os drivers certificados novos e os que faltam.\n" +
            "2. Baixa e instala o que encontrar, conferindo o resultado de cada pacote.\n" +
            "3. Pede ao Windows para procurar driver nos dispositivos conectados.\n\n" +
            "No fim, o relatório diz exatamente o que foi instalado — e, se não havia driver novo, diz isso também.\n" +
            "Pode levar de 5 a 20 minutos e talvez seja preciso reiniciar.\n" +
            "O driver de vídeo NVIDIA/AMD/Intel é baixado e instalado pelo botão BAIXAR INSTALADOR OFICIAL, no cartão PLACA DE VÍDEO.\n\n" +
            "Continuar?", "CENTRAL DE DRIVERS", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        DrvBusy(true);
        var lines = new List<string>();
        string conclusion = "Atualização concluída.";
        try
        {
            DrvSet("Procurando drivers no Windows Update…", 0.08);
            var found = await DriverManager.SearchWindowsUpdateAsync();

            if (found.Drivers.Count == 0)
            {
                lines.Add(found.Ignored > 0
                    ? $"Windows Update: o catálogo só tinha pacote de extensão ({found.Ignored}), que não é driver de " +
                      "dispositivo — por isso não havia o que instalar."
                    : "Windows Update: nenhum driver novo ou faltando para este PC.");
                conclusion = "Nenhum driver novo no Windows Update.";

                var gpu = _drvGpus.FirstOrDefault(g => g.UpdateAvailable && g.DownloadUrl.Length > 0);
                if (gpu != null)
                    lines.Add($"Para o driver de vídeo ({gpu.GpuName}) existe versão mais nova no site do fabricante: " +
                              "use BAIXAR INSTALADOR OFICIAL no cartão PLACA DE VÍDEO.");
            }
            else
            {
                lines.Add($"Windows Update: {found.Drivers.Count} driver(s) para instalar:");
                foreach (var d in found.Drivers.Take(8))
                    lines.Add($"  • {d.Title} ({d.SizeText})" + (d.Manufacturer.Length > 0 ? $" — {d.Manufacturer}" : ""));
                if (found.Drivers.Count > 8) lines.Add($"  … e mais {found.Drivers.Count - 8}");

                DrvSet($"Baixando e instalando {found.Drivers.Count} driver(s)…", 0.35);
                string result = await DriverManager.InstallWindowsUpdateAsync(found.Drivers,
                    t => Dispatcher.BeginInvoke(new Action(() =>
                        DrvSet(t, t.StartsWith("Instalando", StringComparison.OrdinalIgnoreCase) ? 0.7 : 0.35))));
                lines.Add("Windows Update: " + result);
                conclusion = result;
            }

            DrvSet("Procurando driver para os dispositivos conectados…", 0.85);
            lines.Add(await DriverManager.ScanDevicesAsync());

            string? problemError = null;
            var missing = await Task.Run(() => DriverManager.ProblemDevices(out problemError));
            if (problemError != null)
                lines.Add("Não foi possível conferir os dispositivos: " + problemError +
                          ". Confirme no Gerenciador de Dispositivos do Windows.");
            else if (missing.Count == 0)
                lines.Add("Depois disso, nenhum dispositivo está com problema de driver.");
            else
            {
                lines.Add($"{missing.Count} dispositivo(s) ainda com problema:");
                foreach (var d in missing.Take(8))
                    lines.Add($"  • {d.Name}{(d.DeviceClass.Length > 0 ? $" ({d.DeviceClass})" : "")} — {d.Reason}");
                if (missing.Count > 8) lines.Add($"  … e mais {missing.Count - 8}");
                lines.Add("Para esses, reinicie o PC e tente de novo; se continuar, use o suporte do fabricante (placa-mãe).");
            }

            DrvWuList.Text = string.Join("\n", lines);
            DrvSet(conclusion + " Relatório abaixo.", 1);
            _drvWu = new();
            try { DrvList.ItemsSource = await Task.Run(DriverManager.Inventory); }
            catch (Exception ex) { DrvWuList.Text += "\nNão foi possível atualizar a lista de drivers instalados: " + ex.Message; }
        }
        catch (Exception ex)
        {
            DrvWuList.Text = "Falhou: " + ex.Message;
            DrvSet("Falhou: " + ex.Message, 0);
        }
        finally { DrvBusy(false); }
    }

    private async void BtnDrvBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy || !RequireAdmin()) return;
        DrvBusy(true);
        try
        {
            string r = await DriverManager.BackupAsync(
                t => Dispatcher.BeginInvoke(new Action(() => DrvSet(t, 0.5))),
                CancellationToken.None);
            DrvSet(r, 1);
        }
        catch (Exception ex) { DrvSet("Falhou: " + ex.Message, 0); }
        finally { DrvBusy(false); }
    }

    private async void BtnDrvRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy || !RequireAdmin()) return;
        DrvBusy(true);
        DrvSet("Criando ponto de restauração (pode levar até 1 minuto)…", 0.5);
        try { DrvSet(await DriverManager.CreateRestorePointAsync(), 1); }
        catch (Exception ex) { DrvSet("Falhou: " + ex.Message, 0); }
        finally { DrvBusy(false); }
    }
}
