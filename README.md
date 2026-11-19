# Gui_Tech

> **Diagnóstico e otimização de PC para Windows** — sensores em tempo real, drivers, benchmarks,
> limpeza, perfis para jogos e verificação de saúde do hardware, tudo em um só programa.

**Download (grátis):** baixe o instalador oficial na página de
[**Releases**](https://github.com/jgtechtech-a11y/GuiTech/releases) — sempre a versão mais nova.
O app roda como administrador (necessário para sensores de baixo nível); não use versões baixadas
de outros lugares.

O app avisa sozinho quando existe versão nova: consulta as Releases do GitHub uma vez por dia e
mostra um aviso no topo da Visão geral. Na versão instalada, o botão **Atualizar agora** baixa o
instalador, confere o SHA-256 publicado e roda o Setup sozinho — no fim o programa reabre já na
versão nova. Rodando o exe solto (portátil), o botão abre a página da versão no navegador.
Silencioso quando não há internet ou quando você já está na versão mais recente.

---

## Destaques

- **Verificar PC**: escaneia memória, cache, discos, dispositivos, inicialização, segundo plano e
  sistema de arquivos; corrige sozinho o que é seguro (cache/temporários e RAM acima de 75%) e
  lista o que ainda pode ser otimizado. O botão **CORRIGIR** aplica depois, de uma vez, as
  otimizações seguras (cache, memória em espera e inicialização quebrada).
- **Central de drivers**: inventário completo, consulta de versão NVIDIA/AMD, links oficiais,
  **ATUALIZAR DRIVERS** (procura, baixa e instala pelo Windows Update, incluindo os ausentes),
  backup por `pnputil` e ponto de restauração — **sem download** de instalador de site de
  terceiros.
- **Gui_Tech OTIMIZA**: libera RAM, fecha programas em segundo plano/inativos, limpeza de cache do
  sistema (temporários, miniaturas, DirectX, Windows Update, Lixeira), limpeza do cache de
  arquivos do sistema (Empty System Working Set), manutenção do Windows (SFC, DISM e CHKDSK com
  log ao vivo), **MODO JOGO** e **MODO TRABALHO**.
- **Biblioteca de jogos**: grade com capas, perfil de CPU por jogo com **afinidade** de núcleos e
  **prioridade de threads** (reaplicadas enquanto o jogo roda), além de prioridade do processo,
  plano de energia, GPU dedicada e tela cheia.
- **Gui_Tech BENCH**: benchmarks próprios de CPU, GPU (incluindo **ray tracing**), simulação de
  jogo, memória e disco, com histórico.
- **Gui_Tech OC**: ajuste de limite de energia e travamento de clock da GPU NVIDIA (via NVML) com
  restauração, e **verificação de estabilidade** de overclock (CPU, GPU ou ambos) com limites de
  segurança de temperatura e leitura de erros de hardware (WHEA).
- **Curva de ventoinha da GPU** (Overclock): curva de 5 pontos derivada do limite escolhido,
  aplicada pela API oficial da AMD (Overdrive8), com reforço automático se a placa esquentar e
  volta ao automático ao fechar o programa. Em NVIDIA a seção não aparece.
- **Iniciar com o Windows** por tarefa agendada no logon com privilégios máximos (sem UAC a cada
  logon), minimizado na bandeja do sistema; **botão minimizar vai para a bandeja**.
- **Avisos de dispositivo**: ao conectar/remover pendrive ou HD externo, aparece um aviso no canto
  da tela e a lista de unidades se atualiza (via `WM_DEVICECHANGE`).

## Recursos

App WPF (.NET 10) com:

- **Informações do sistema** (categorias + detalhes): SO, placa-mãe/BIOS, CPU, RAM (por módulo),
  GPU, discos — via WMI. Em Armazenamento, cartões por unidade (C:, pendrive, HD externo, rede)
  com anel de cheio/vazio, tipo e disco físico (`DriveMap.cs`).
- **Sensores em tempo real**: temperatura, clock, carga, voltagem, ventoinhas, consumo etc. — via
  LibreHardwareMonitorLib. Atualiza a cada 1 s, com filtro por tipo.
- **Placa de vídeo** (estilo GPU-Z): chip, arquitetura, processo, die, transistores, shaders/SMs/
  Tensor/RT, ROPs/TMUs, fillrates, TFLOPS, memória (tamanho, tipo, barramento, clock, banda,
  Resizable BAR), clocks, PCIe (máximo e em uso), limites de energia, VBIOS, driver, assinatura
  WHQL, CUDA, DirectX (feature level), APIs e recursos da arquitetura. Sensores ao vivo da GPU +
  P-state, PerfCap, NVENC/NVDEC. Fontes: NVML (NVIDIA), DXGI/Direct3D 12, WMI/PnP, registro e
  banco de specs em `GpuSpecDb.cs`.
- **Stress test**: carga de CPU em todos os threads (FPU + inteiros) e/ou RAM (grava e verifica
  padrões em 40–80% da memória livre). Para sozinho quando a temperatura da CPU atinge o limite
  configurado, quando o tempo acaba ou se aparecer erro de memória. Mostra temperatura atual/
  máxima, carga, consumo, gráfico dos últimos 5 min e um log dos eventos.
- **Stress de GPU** (`GpuBurn.cs`): torus com pelos renderizado por *shells* em Direct3D 11
  (Vortice), com overdraw massivo, cadeia de FMAs no pixel shader e alpha blending, sem limite de
  FPS, numa janela própria. Opções: resolução (720p/1080p/1440p/tela cheia), MSAA (off/4x/8x),
  intensidade (Normal/Alta/Extrema) e limite de temperatura da GPU. Mostra FPS, temperatura,
  consumo e PerfCap; ao final registra frames e FPS médio. ESC ou fechar a janela encerra o teste.
- **Jogos**: FPS esperado em 32 jogos populares para a sua GPU + CPU, por resolução
  (1080p/1440p/4K), gráficos e upscaling (DLSS/FSR). Com chave da
  [HowManyFPS API](https://howmanyfps.com/developer-api) (pedida em info@howmanyfps.com) mostra
  FPS médio e 1% low medidos em sessões reais; sem chave, usa uma estimativa local aproximada
  (`GameCatalog.cs`). A chave fica cifrada com DPAPI em `%AppData%\GuiTech` e as respostas em
  cache por 7 dias.
- **Detector de gargalo**: equilíbrio teórico GPU × CPU; leitura ao vivo (carga da GPU, CPU total,
  núcleo mais ocupado, VRAM, RAM, temperaturas, limitador térmico) com veredito; e análise de
  sessão (1/3/5 min) com % do tempo limitado por GPU, CPU, VRAM, RAM, temperatura ou trava de FPS,
  mais recomendações (`BottleneckAnalyzer.cs`).
- **Overlay de jogos** (estilo RTSS/Afterburner, `OverlayWindow.xaml`): FPS, frametime, 1% low,
  gráfico de frametime, GPU/CPU/RAM/VRAM. Janela transparente, sempre no topo e que ignora cliques;
  atalho global **Ctrl+Insert**. **Itens escolhidos pelo usuário** (`OverlayMetrics.cs`,
  `MainWindow.Overlay.cs`): FPS/frametime/1% low/gráfico, GPU (uso, temperatura, hotspot, clocks do
  núcleo e da memória, consumo, tensão, ventoinha, PerfCap), VRAM, CPU (uso, núcleo mais ocupado,
  temperatura, clock médio e máximo, consumo), RAM, nome do jogo e hora. Volta ao topo a cada
  troca de janela ativa e fica no monitor do jogo. Não aparece em tela cheia exclusiva. O FPS de
  qualquer jogo é medido via ETW (eventos Present de DXGI/D3D9, como o PresentMon,
  `FrameMonitor.cs`), sem injetar nada no jogo.
- **Gui_Tech BENCH** (`BenchRenderer.cs`): benchmark de CPU no princípio do Cinebench, com
  renderizador próprio. Path tracer monocromático (iluminação global, sombras suaves, metal e
  vidro com refração), 960×540 a 1024 amostras/pixel, imagem em blocos de 32 px distribuídos entre
  threads do centro para fora. Multi-core (todas as threads) e single-core (1 thread), 1 passada
  ou mínimo de 10 min. Nota = amostras/s ÷ 10.000, determinística (sementes fixas por pixel).
  Histórico dos últimos 10 resultados. Referência: Ryzen 7 7700X ≈ 4.500 multi / 530 single.
- **Gui_Tech BENCH · benchmarks de jogo**:
  - *Cena de jogo (GPU)* (`GpuGameBench.cs`): vale em Direct3D 11 com terreno fractal, 40 mil
    árvores por instancing, sombras 4096² com PCF, água, neblina e pós HDR; câmera em trajeto fixo
    por tempo, 1080p/1440p/4K, 30/60 s. Nota = FPS médio × 10. Os sensores ficam pausados durante
    o teste (consultas ao driver geravam engasgos no 1% low).
  - *Ray tracing (GPU)* (`GpuRtBench.cs`, `MainWindow.Rt.cs`): Direct3D 12 com **DXR 1.1** (raios
    inline num compute shader), usa os núcleos de RT (RTX 20+, Radeon RX 6000+, Intel Arc). 4 mil
    objetos (fosco, metal, espelho, luzes), path tracing com sombra suave, oclusão de ambiente,
    reflexos e iluminação indireta; TLAS refeita a cada frame. Presets Normal/Alta/Extrema (2/4/8
    amostras, 2/3/4 rebatidas). Nota pela mediana (FPS × 10), variação entre passadas e tempo de
    frame pelos timestamps da GPU.
  - *Simulação de jogo (CPU)* (`GameSimBench.cs`): 30 mil unidades em frente de batalha; thread
    principal (grade, lógica, preparo do desenho) + workers (física, colisão, IA). Nota = FPS da
    simulação.
  - *Memória e carregamento* (`MemoryBench.cs`): latência e banda da RAM; carregamento de mapa
    (pacote de 512 MB + mil arquivos de 64 KB) lido sem cache do Windows, com passada prévia para
    o antivírus e 8 threads. Arquivos temporários apagados no final.
  - *Disco* (`DiskBench.cs`): leitura e escrita sequencial (1 MB) e aleatória 4K (QD1 e QD32), sem
    cache do Windows (`FILE_FLAG_NO_BUFFERING`), em arquivo temporário de 512 MB / 1 GB / 4 GB na
    unidade escolhida; apagado no final. Histórico dos últimos resultados.
- **Gui_Tech OC** (`MainWindow.Oc.cs`, `OcCheck.cs`): ajuste de limite de energia e travamento de
  clock da GPU NVIDIA (via NVML) com botão de restaurar, e **verificação de estabilidade** de
  overclock (CPU, GPU ou ambos; 5/15/30 min) com limites de segurança de temperatura, leitura de
  erros de hardware (WHEA, reset de driver de vídeo), veredito, histórico e guia de BIOS conforme
  a placa-mãe e a CPU.
- **Gui_Tech OTIMIZA** (`Optimizer.cs`, `WindowsTweaks.cs`): um botão que limpa a memória em
  espera (como o ISLC) e enxuga a RAM dos processos, e fecha os programas marcados (pré-marca só
  os seguros: OneDrive, Teams, Widgets, Adobe...; programas com janela são fechados sem forçar).
  Ajustes do Windows com interruptor e "Restaurar originais": plano Alto desempenho, Modo de Jogo,
  desativar Game DVR, HAGS, otimizações para jogos em janela, aceleração do mouse. Nada que reduza
  a segurança (Defender, VBS, UAC, Update).
- **Gui_Tech MAKER** (Arduino, ESP32 e conversores USB-serial):
  - *Portas COM* (`UsbCleaner.cs`): lista e remove registros de dispositivos USB/seriais ausentes
    ("fantasmas" de Arduino, ESP32, CH340, FTDI, CP210x...) e libera os números COM presos no
    ComDB, com backup para desfazer. Resolve a IDE do Arduino não achar a placa ou a porta ir para
    COM20+.
  - *Drivers seriais* (`MakerDrivers.cs`, `MainWindow.Maker.cs`): diagnóstico das placas
    conectadas com **CH340/CH341, CP2102/CP210x, FTDI, Prolific PL2303 e Arduino** pelo código do
    Gerenciador de Dispositivos (sem driver, código 10, 43, FTDI falsificado com PID 0000), com
    orientação para cada caso; **REINSTALAR** (remove e detecta de novo); lista das versões de
    driver guardadas no Windows com **REMOVER** (backup antes) e "Restaurar removidos";
    **drivers oficiais incluídos no app** (CH340 4.0, CP210x 11.6, FTDI 2.12.36.20 e Arduino, em
    `Drivers`, veja `Drivers/LEIA-ME.md`) que instalam sem internet; e link para a página oficial
    de cada fabricante. **Arduino IDE**: instalar/atualizar pelo winget, fechar programas que
    prendem a porta e limpar o cache de índices quando a IDE trava em "Downloading index".
- **Inicialização** (`StartupManager.cs`, `MainWindow.Startup.cs`): lista os programas que abrem
  com o Windows (chaves Run do usuário e de todos os usuários, 32 e 64 bits, e pastas Inicializar)
  com descrição e fabricante. Interruptor para ativar/desativar igual ao Gerenciador de Tarefas
  (chaves StartupApproved) e botão REMOVER, com cópia em `%AppData%\GuiTech` para "Restaurar
  removidos".
- **Instalar apps** (`AppInstaller.cs`, `MainWindow.Apps.cs`): instala os essenciais
  pós-formatação (Steam, Google Chrome, Discord, Lightshot, 7-Zip, WinRAR, VLC) pelo **winget**,
  em silêncio e um de cada vez, mostrando o resultado de cada um. Detecta o que já está instalado.
- **Saúde dos discos (S.M.A.R.T.)** (`DiskHealth.cs`, `MainWindow.Disks.cs`): página própria com
  um cartão por disco físico — veredito (Bom/Atenção/Ruim) com os motivos, temperatura, horas
  ligado, ciclos, desgaste do SSD, setores realocados, erros de leitura/escrita e previsão de
  falha. Fontes: Windows Storage (MSFT_PhysicalDisk, MSFT_StorageReliabilityCounter) e
  MSStorageDriver_FailurePredictStatus.
- **Relatório exportável** (`ReportBuilder.cs`): botão **EXPORTAR RELATÓRIO** em Sistema. Gera um
  arquivo HTML (com a marca Gui_Tech) ou JSON com as informações do sistema, RAM por módulo,
  discos e saúde dos discos.
- **RAM detalhada**: resumo (capacidade, canais, tipo DDR4/DDR5/LPDDR5, velocidade) e, por módulo,
  fabricante, part number, formato, voltagem, clock configurado e localização do slot.
- **Energia** (`BatteryInfo.cs`, `MainWindow.Net.cs`): bateria de notebook com desgaste
  (capacidade atual × de projeto), ciclos de carga, estado, autonomia, tensão e potência, com
  veredito; fonte de energia (tomada/bateria), plano de energia do Windows e consumo dos sensores
  de CPU e GPU.
- **Rede** (`NetworkTest.cs`): diagnóstico de lag em ~30 s — latência, jitter e perda de pacotes
  até o roteador e até a internet (1.1.1.1 e 8.8.8.8), tempo de DNS, velocidade de download/upload
  (servidor público speed.cloudflare.com) e latência sob carga (bufferbloat). O veredito aponta se
  o problema está na rede local (Wi-Fi/cabo/roteador), no provedor ou se a conexão está estável.
- **Gui_Tech INTEGRAÇÃO** (`LightBackends.cs`, `CorsairSdk.cs`, `CorsairSdkInstaller.cs`,
  `MainWindow.Integ.cs`): controle de iluminação e efeitos de todo o PC numa tela só. Pontes:
  **Corsair iCUE / iCUE LINK** (iCUE SDK v4; a DLL é baixada pelo programa, com confirmação e
  verificação de assinatura), **OpenRGB** (servidor SDK local; é o caminho para **MSI Mystic
  Light, Gigabyte RGB Fusion, ASUS Aura**, RAM, Razer, Logitech e outras, já que essas marcas não
  têm SDK público) e **Razer Chroma** (API REST do Synapse). Efeitos: cor fixa, respiração,
  arco-íris em onda, e cores que seguem a **temperatura da CPU/GPU** ou a **carga da CPU**, com
  brilho e velocidade. Ao devolver ou fechar, cada fabricante reassume as luzes.
- **Gui_Tech JOGOS** (`GameLibrary.cs`, `GameLauncher.cs`, `Vdf.cs`, `MainWindow.Launcher.cs`):
  inicializador que **detecta os jogos instalados** (Steam, Epic Games, GOG, Ubisoft Connect e
  `.exe` adicionados à mão) e guarda um **perfil por jogo**: prioridade do processo, plano de
  energia Alto desempenho só durante o jogo, GPU dedicada e sem otimizações de tela cheia (por
  `.exe`, no registro do usuário), **menu de gráficos** (modo de janela, resolução, taxa de
  atualização e extras convertidos em argumentos de inicialização no estilo Source/CS2, Unity ou
  Unreal) e **programas auxiliares** (ferramentas de terceiros que abrem antes do jogo e fecham
  junto). Em jogos da Steam, um botão grava os argumentos nas **opções de inicialização**
  (`localconfig.vdf`, com backup, fechando a Steam antes). O botão JOGAR aplica o perfil, inicia e
  desfaz o que for temporário quando o jogo fecha.
- **Capas dos jogos** (`GameArt.cs`): a biblioteca é uma **grade de capas 2:3**. A arte vem do
  próprio PC (capa vertical que a Steam baixou, inclusive as personalizadas em
  `userdata\<id>\config\grid`); quando só existe o cabeçalho largo, o app baixa a capa oficial do
  CDN da Steam e guarda em `%LOCALAPPDATA%\GuiTech\capas`. Epic/GOG/manual usam o ícone do `.exe`
  (via shell, 256 px) e, sem nada, um gradiente estável com as iniciais.
- **Gui_Tech DEBLOAT** (`Debloat.cs`, `MainWindow.Debloat.cs`): catálogo de 30 ajustes no estilo
  Win11Debloat, reescrito em C# (registro, serviço DiagTrack, tarefas agendadas via COM
  `Schedule.Service` e remoção de apps por `Remove-AppxPackage`). Cada item lê o estado real,
  aplica e **desfaz**; aplicação em lote com ponto de restauração e reinício do Explorer no fim.
  Nada de segurança é alterado (Defender, firewall, UAC, Windows Update).

## Interface

- Visual **grafite** minimalista em **blocos** (cartões arredondados, inspirado em painéis de
  dashboard), com **modo escuro e claro**. Um único menu lateral com *Visão geral* no topo e
  categorias recolhíveis:
  - *Visão geral* (`MainWindow.Dash.cs`): gráfico de área CPU × GPU (temperatura, carga ou
    consumo; 1/5/15 min), desempenho do Gui_Tech BENCH, tabela de componentes, anéis de
    RAM/VRAM/disco, eventos recentes e saúde do PC.
  - **SISTEMA** · Sistema, Sensores, Placa de vídeo, Discos, Energia, Rede.
  - **DESEMPENHO** · Benchmark, Stress test, Overclock.
  - **FERRAMENTAS** · Otimização, Debloat, Inicialização, Instalar apps, Drivers, Integração.
  - **JOGOS** · Biblioteca, FPS estimado, Gargalo, Overlay.
  - **MAKER** · Portas COM, Drivers seriais.
- Botão de **modo escuro/claro** no canto superior direito (`ThemeManager.cs`); a escolha fica
  salva. O overlay de jogos e os avisos de dispositivo ficam sempre escuros.
- Leitura ao vivo de temperatura e carga da CPU e da GPU sempre visível no rodapé do menu.
- Sensores com busca e filtros por tipo (chips); tema em `Theme.xaml`.

## Base utilizada

- Interface, stress test e gráficos: código próprio.
- Sensores: [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
  (MPL 2.0) — mantenha o aviso de licença ao distribuir.
- Informações do sistema: WMI do Windows.
- Medição de FPS: [TraceEvent](https://github.com/microsoft/perfview) (MIT), da Microsoft, para
  ler eventos ETW.
- Renderização do stress de GPU:
  [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) (MIT), bindings de Direct3D 11
  para .NET. Direct3D 12 e DXC (compilador de shaders da Microsoft) também via Vortice, no teste
  de ray tracing.

## Requisitos

- Windows 10/11
- Para **usar**: só baixar o instalador na página de
  [Releases](https://github.com/jgtechtech-a11y/GuiTech/releases).
- Para **compilar**: .NET 10 SDK (https://dotnet.microsoft.com/download)
- Driver **PawnIO** (temperatura/consumo da CPU): `winget install namazso.PawnIO`
  A LibreHardwareMonitorLib 0.9.6+ usa o PawnIO no lugar do antigo WinRing0, que o Windows
  Defender bloqueia.

## Como rodar (a partir do código)

```
cd GuiTech
dotnet run
```

O app pede privilégio de administrador (necessário para sensores de baixo nível).

Para levar a outro PC: dê dois cliques em `publicar.bat`. Ele gera em `..\GuiTech-dist`:
`GuiTech-Setup-<versao>.exe` (instalador, **é este que deve ser distribuído**), `GuiTech\` +
`GuiTech.zip` (versão em pasta), `GuiTech.exe` (arquivo único, só para uso local — se autoextrai
em `%TEMP%`) e `LEIA-ME.txt`. O instalador é feito com Inno Setup 6 (`instalador.iss`); se o
ISCC não estiver instalado, esse passo é pulado e o resto continua.

### Versão nova publicada no GitHub

A cada **tag** (ex.: `git tag v0.25.16` + `git push origin v0.25.16`), o GitHub Actions
(`.github/workflows/publicar.yml`) compila o app, roda os testes e cria uma release com o
**Setup** (`GuiTech-Setup-<versão>.exe`), o `GuiTech.exe`, o `.zip` e o `SHA256SUMS.txt`,
marcando como a mais recente. O verificador de atualização dentro do app lê essa página
(`UpdateChecker.cs`) e o botão **Atualizar agora** usa o Setup + os hashes (`UpdateInstaller.cs`)
para se atualizar em um clique.

## Observações

- Se algum sensor não aparecer, depende do suporte da biblioteca à sua placa-mãe.

## Já feito

- ✔ Informações do sistema, sensores, placa de vídeo, jogos, gargalo, overlay
- ✔ Stress test com limite de temperatura (CPU, RAM, GPU)
- ✔ Benchmarks: CPU, GPU, simulação, memória e **disco**
- ✔ Saúde dos discos (S.M.A.R.T.)
- ✔ Relatório exportável (HTML/JSON)
- ✔ Gui_Tech OC (limite de energia, clock e verificação de estabilidade)
- ✔ Energia (bateria e fonte) e Rede (latência, perda e velocidade)
- ✔ Gui_Tech INTEGRAÇÃO: iluminação Corsair iCUE, OpenRGB (MSI/Gigabyte/ASUS) e Razer Chroma
  (primeira versão, em teste)
- ✔ Central de drivers: inventário, NVIDIA, AMD/ASUS/MSI/Gigabyte, Windows Update e backup
  (`DriverManager.cs`)
- ✔ Gui_Tech JOGOS: inicializador com detecção de jogos, perfil por jogo e opções de
  inicialização da Steam
- ✔ Inicialização do Windows: ativar, desativar e remover programas, com desfazer
- ✔ Instalar apps essenciais pós-formatação pelo winget
- ✔ Gui_Tech MAKER: portas COM e drivers seriais CH340/CP210x/FTDI/Prolific/Arduino
- ✔ Biblioteca de jogos em grade com capas da Steam, ícone do .exe e cache local
- ✔ Gui_Tech DEBLOAT: 30 ajustes de telemetria/IA/interface/apps com estado real e desfazer
- ✔ Tela de abertura (splash) e aviso de versão nova dentro do app
- ✔ Testes automáticos (`GuiTech.Tests`, rodando no build do GitHub) e versão publicada por tag

## Próximas ideias

- Comparar resultados de benchmark entre PCs (trocar relatórios entre máquinas)
- Leitura direta de SPD / Super I/O
- Gui_Tech INTEGRAÇÃO: efeitos por jogo e SDKs nativos da MSI/ASUS/Gigabyte se forem liberados

## Licença

MIT — veja o arquivo [LICENSE](LICENSE).
