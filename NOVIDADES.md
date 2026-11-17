## Novidades da v0.25.12 · Gui_Tech

**Abertura com rosto novo**
- Tela de abertura: na primeira abertura (que demora 20-40 segundos: extração do exe
  e detecção do hardware) aparece uma janelinha com o logo e "Preparando o ambiente...",
  em vez de nada na tela parecendo que travou. Ela fecha sozinha quando o app abre.

**Aviso de versão nova dentro do programa**
- O Gui_Tech agora descobre sozinho se existe versão mais nova (consulta as Releases
  do GitHub uma vez por dia, em segundo plano). Se houver, aparece um aviso no topo
  da Visão geral com botão para baixar. Silencioso quando não há internet.
  (Funciona a partir do momento em que o projeto tem repositório no GitHub.)

**Por dentro**
- 21 testes automáticos novos (GuiTech.Tests) cobrindo a lógica que não depende de
  hardware: parsing de versão, formatação de tamanhos, relatório e selos do Verificar PC.
- Todos os avisos de "valor possivelmente nulo" do compilador foram resolvidos —
  código do benchmark mais vigiado, base em zero.
- Workflow do GitHub atualizado: build no SDK 10 e release automática a cada tag.

## Novidades da v0.25.11 · Gui_Tech

**Migração para .NET 10**
- O programa agora roda no .NET 10 (suporte garantido até novembro de 2028). O .NET 8,
  que era usado antes, saiu de suporte em 10/11/2026.
- Nenhuma função mudou — é a mesma tela, o mesmo funcionamento. As melhorias do novo
  runtime (JIT, memória, bibliotecas) vêm de graça.
- Limpeza: pacote que virou parte do próprio .NET 10 foi removido da lista de
  dependências; verificação de assinatura de drivers atualizada para o padrão novo.

## Novidades da v0.25.10 · Gui_Tech

**Biblioteca de jogos sem capa em tons frios**
- Os cartões de jogos que não têm capa usavam um fundo em azul/violeta/roxo (resto
  do tema antigo). Agora o fundo é ciano/azul frio, combinando com o tema teal —
  e a cor continua estável (o mesmo jogo sempre tem a mesma cor).

## Novidades da v0.25.9 · Gui_Tech

**Últimos restos de roxo trocados por teal**
- Ícone do aplicativo (e os logos do menu, do relatório e do atalho) agora em teal,
  no mesmo tom do programa.
- Overlay de jogos: o gráfico de frametimes estava com o roxo antigo — agora teal.
- Janela do Debloat e tela de resultados com selos em teal.
- Relatório exportado (HTML) com a paleta nova, no tema claro e no escuro.
- Ajuste nos tons dos textos de aviso (toast e overlay), que tinham um fundo
  levemente arroxeado.

## Novidades da v0.25.8 · Gui_Tech

**Ajustes finos de acabamento visual**
- Leitura mais confortável: os textos de descrição ganharam espaçamento entre linhas
  e limite de largura, para não virar "parede de texto" em telas largas.
- Espaçamentos internos normalizados em uma grade fixa de 4/8 px em todas as telas
  (antes havia valores fora do padrão, como 9, 7, 5 e 11 px).
- Botão primário e campos de texto com respiro interno levemente maior (mais fáceis de clicar).
- Foco de teclado no campo de senha agora usa a cor de destaque (antes era quase invisível).
- Itens do menu lateral com altura levemente menor (36 px), alinhados à grade.

## Novidades da v0.25.7 · Gui_Tech

**Visual renovado (tema escuro e claro)**
- Cor de destaque nova: teal dessaturado (#4FB3A9) no lugar do roxo, em todo o programa —
  botões, gráficos, seleção, chips e o overlay de jogos. Os neutros (fundo, cartões, linhas)
  acompanharam o novo tom.
- Cartões com borda sutil de 1px e cantos de 8px (padrão do Windows 11); botões, chips e campos
  com cantos de 6px. O respiro interno dos cartões e as margens das páginas seguem uma grade
  fixa de 8px.
- Botões com transição suave de 120 ms ao passar o mouse e anel de foco visível ao navegar
  pelo teclado (botões, chips, itens do menu e campos).
- Títulos menores e com a fonte do Windows 11 (Segoe UI Variable Display): título de página
  26 → 22, números grandes 34 → 28.
- Botões e títulos de página em caixa normal ("Aplicar selecionados", "Central de drivers")
  em vez de tudo em MAIÚSCULAS. Siglas continuam maiúsculas (PC, CPU, GPU, SFC, DISM…),
  assim como os rótulos pequenos de seção e os selos ("CORRIGIDO", "OK").
- Tema claro atualizado junto: mesmo tom teal, ajustado para contraste com fundo branco.
- A versão no rodapé do menu lateral agora é lida do próprio executável (nada de texto fixo).

## Novidades da v0.25.6 · Gui_Tech

**Comparar placas de vídeo (Gui_Tech OC)**
- Novo bloco na aba OC: escolha duas placas do catálogo e veja lado a lado chip, arquitetura,
  núcleos, clocks, memória (GB, barramento e velocidade), largura de banda, FP32 teórico,
  consumo e tecnologias. A linha de cada dado destaca a placa que leva.
- A sua placa já vem pré-selecionada de um lado. Larg. de banda e TFLOPS são cálculos das
  especificações de fábrica (classe da placa), não substituem o benchmark — para FPS real, Gui_Tech BENCH.

**Rota até a internet no teste de rede**
- O diagnóstico de rede agora traça a rota completa até a internet, salto por salto (estilo tracert):
  endereço e nome de cada roteador no caminho, com latência e perda de cada salto.
- Ajuda a dizer se o problema está na sua rede, no seu provedor ou depois dele.

**Revisão semanal automática (FERRAMENTAS › Inicialização)**
- Interruptor novo ao lado de "Iniciar com o Windows": uma vez por semana (domingo, ~11:00,
  roda na próxima vez que ligar se o PC estiver desligado) o Gui_Tech faz sozinho o VERIFICAR PC
  + CORRIGIR — só correções seguras, em silêncio, sem abrir nada na tela.
- O resultado fica em revisao.txt na pasta de configurações (%AppData%\GuiTech).
- Se o Gui_Tech já estiver aberto, a revisão roda dentro dele e aparece como aviso no canto da tela.

**Instalar apps: mais de 30 programas**
- O catálogo pós-formatação passou de 7 para mais de 30 apps do winget: lojas de jogos (Epic, GOG,
  Ubisoft, Battle.net, EA), navegadores (Firefox, Brave), utilitários (Notepad++, Everything,
  PowerToys, qBittorrent, CPU-Z, GPU-Z, CrystalDiskInfo), mídia e criação (Spotify, OBS, Audacity,
  HandBrake, GIMP, Krita, Blender), programação (VS Code, Git, Obsidian) e o Visual C++ Redistributable.
- Nada é instalado sem você marcar: os novos vêm desmarcados por padrão.

## Novidades da v0.25.5 · Gui_Tech

**Distribuição em pasta (ZIP), sem o .exe autoextraível**
- O `GuiTech.exe` era um arquivo único que se descompactava sozinho em `%TEMP%\.net\GuiTech\`
  a cada execução e rodava DLLs de lá. Antivírus leem isso como "dropper" e é uma das
  principais razões de o programa ser apontado como suspeito em outros PCs.
- Agora o programa é distribuído em pasta (`GuiTech.zip`): extraia e rode o `GuiTech.exe`.
  Nada é descompactado em `%TEMP%`, e não sobra pasta nenhuma para trás.
- Se preferir levar só um arquivo, a versão em arquivo único continua disponível.

**Instalador (GuiTech-Setup.exe)**
- Agora dá para distribuir um único `.exe` de instalação (60 MB): instala em
  `C:\Program Files\GuiTech`, cria os atalhos no Menu Iniciar e na Área de Trabalho e
  aparece em "Aplicativos instalados" para desinstalar.
- Feito com Inno Setup (o mesmo motor de instalador de milhares de programas comerciais),
  justamente para não usar um autoextraível próprio — que é o formato que antivírus
  mais estranham.
- O instalador usa a pasta já pronta: nada é extraído em `%TEMP%`. Ele fecha o Gui_Tech
  sozinho se estiver aberto, e ao desinstalar remove a tarefa de inicialização.

**Debloat sem PowerShell escondido**
- A contagem e a remoção de apps pré-instalados usavam
  `powershell.exe -ExecutionPolicy Bypass -EncodedCommand <base64>` — a assinatura clássica
  de script malicioso para qualquer antivírus.
- Agora usa a API de implantação do próprio Windows (`PackageManager`), direto em C#, sem
  abrir nenhum shell. Também ficou bem mais rápido.

## Novidades da v0.25.4 · Gui_Tech

**Overlay: atalho Ctrl + Insert**
- O atalho global do overlay é agora **Ctrl + Insert** (antes era Insert sozinho): um toque
  abre, outro toque fecha — inclusive dentro do jogo. O Insert sozinho ficava capturado do
  sistema inteiro; com Ctrl, o teclado dos outros programas continua normal.

## Novidades da v0.25.3 · Gui_Tech

**Overlay com atalho Insert**
- O atalho global do overlay passou de **Ctrl + Shift + O** para **Insert**: um toque abre,
  outro toque fecha — inclusive dentro do jogo.
- O aviso na aba Overlay, o LEIA-ME e o README foram atualizados.

## Novidades da v0.25.2 · Gui_Tech

**Stress test de RAM devolve a memória ao parar**
- Antes, ao terminar (ou parar) o teste de RAM, o app continuava segurando a memória que tinha
  alocado — o Windows seguia mostrando 40/60/80% em uso até o processo fechar.
- Os blocos de teste são arrays grandes (LOH): o .NET liberava os objetos, mas as páginas
  continuavam na memória de trabalho do processo. Agora, ao parar, o app compacta o LOH e
  devolve as páginas livres ao Windows. No teste real: de ~6,7 GB para ~40 MB na hora.

## Novidades da v0.25.1 · Gui_Tech

**Correção no Debloat**
- O item **"Tarefas agendadas de telemetria"** mostrava "Aplicado" mesmo com as tarefas ainda
  ativas. O app consultava as pastas do Agendador com uma barra sobrando no fim do caminho, o
  que fazia a leitura falhar sempre. Agora o estado é lido de verdade e o DESFAZER funciona.
- Tarefas que não existem neste Windows passam a contar como "nada a fazer" em vez de erro.

## Novidades da v0.25 · Gui_Tech

**Gui_Tech JOGOS · biblioteca com capas**
- A lista de texto virou uma **grade de capas**: cada jogo aparece com a arte que a Steam já baixou
  no PC (inclusive as capas verticais personalizadas) e o nome/loja embaixo.
- Quando o PC só tem a imagem larga (header) da Steam, o app busca a **capa vertical oficial** no
  CDN da Steam e guarda em `%LOCALAPPDATA%\GuiTech\capas` — na próxima abertura é instantânea.
- Epic, GOG, Ubisoft e os jogos adicionados à mão usam o **ícone do .exe**; se não houver arte
  nenhuma, o cartão ganha um gradiente com as iniciais do jogo (mesma cor sempre).
- Busca e seleção continuam iguais; o painel de perfil do jogo ficou ao lado da grade.

**Gui_Tech DEBLOAT · novo em FERRAMENTAS**
- Nova aba com **30 ajustes** no estilo Win11Debloat: telemetria, sugestões/anúncios, tarefas
  agendadas de coleta, Copilot, Recall, Click to Do, Widgets, Chat, apps pré-instalados,
  Otimização de Entrega, drivers pelo Windows Update, barra de tarefas, menu clássico e mais.
- Cada linha mostra **Nível** (recomendado/opcional/avançado), **o que faz**, o **estado atual**
  ("Aplicado" / "Não aplicado") e um botão **DESFAZER** que volta ao padrão do Windows.
- **MARCAR TODOS / DESMARCAR TODOS**, contador "X de Y aplicados" e aplicação em lote com
  confirmação. No fim, oferece reiniciar o Explorer para valer na barra de tarefas.
- Botão de **ponto de restauração** antes de mexer e **DESFAZER APLICADOS** para reverter tudo.
- Nada de segurança é tocado: Defender, firewall, UAC e Windows Update continuam iguais.
  Sem administrador, o app avisa que os ajustes de política/serviço/apps não vão funcionar.

## Novidades da v0.24 · Gui_Tech

**Tema escuro refeito de verdade (só visual, nada de função mudou)**
- O fundo deixou de ser quase-preto puro e passou a ter uma tinta roxa discreta; os cartões
  agora se destacam **só por serem mais claros**, sem borda de 1 px e sem sombra em cada um.
  Antes eram 20+ contornos competindo com o conteúdo.
- O texto não é mais branco puro: é um off-white (menos "borrão" em tela escura, principalmente
  à noite). Títulos e valores continuam com o mesmo peso de antes.
- O roxo principal foi dessaturado: continua sendo a cor da marca, mas sem vibrar sobre o
  fundo escuro. O botão principal usa texto escuro sobre o roxo (o branco puro sobre roxo
  ficava pesado e com contraste pior).
- Menu lateral: o item aberto é indicado pela superfície mais clara, sem a pílula roxa nem a
  faixa na lateral.
- Chips/filtros (1 MIN, 5 MIN, Temperatura…) agora são preenchidos em vez de contornados.
- O mesmo conjunto de cores foi levado para o **aviso flutuante**, o **overlay do jogo** e o
  **relatório exportado** — nada ficou com a paleta antiga.
- Se você usa o modo claro, ele continua igual: as cores novas do modo escuro são trocadas
  do mesmo jeito que as antigas.

## Novidades da v0.23.1 · Gui_Tech

**CORRIGIR agora mostra o que mudou**
- A lista do resultado passou a marcar **CORRIGIDO** em cada ponto que o botão realmente mexeu,
  com o antes → depois medido: "218,5 MB liberados: eram 432,0 MB, agora são 213,5 MB" e
  "Era 31% de RAM em uso e agora está 28%: 451 MB devolvidos".
- O resumo conta as correções ("7 verificações · 2 corrigida(s) · 2 sugestão(ões)") e o texto
  do topo muda para "Corrigi 2 ponto(s) agora" — antes a tela repetia a mesma frase de antes
  e parecia que nada tinha acontecido.
- O que sobrou por estar **em uso por outros programas** deixou de aparecer como "PODE OTIMIZAR":
  vira OK com o motivo ("206 MB em 58 arquivo(s) abertos por outros programas — o Windows libera
  sozinho quando eles fecharem"). O app não promete mais o que não pode apagar.
- Pressionar CORRIGIR de novo não repete a mesma bronca: o que já foi corrigido aparece como
  corrigido, e o que depende de você é listado em "Precisa de você".
- O texto ficou em linhas separadas (aplicado / ficou de fora / precisa de você) em vez de um
  parágrafo corrido.
- A contagem de "temporários" não conta mais a pasta `%TEMP%\.net`, que é onde o próprio .NET
  extrai as bibliotecas do app aberto: eram arquivos travados que faziam o número nunca baixar.

## Novidades da v0.23 · Gui_Tech

**VERIFICAR PC / CORRIGIR · refeito**
- O **VERIFICAR PC** agora é só leitura: ele mede e lista, mas **não apaga nem altera nada**.
  Quem aplica as correções é o botão **CORRIGIR**.
- O **CORRIGIR** passou a relatar tudo o que fez e o que ficou de fora, em vez de terminar
  escondido: mostra os MB/GB liberados de verdade, os arquivos que estavam em uso e as
  permissões que faltaram (ex.: lista de memória em espera exige administrador).
- Terminou a sensação de "não terminou": depois de corrigir, a conferência é **rápida**
  (não roda mais a verificação de disco de 3 minutos) e a barra **não volta para trás**.
- A tela agora diz o que **precisa de você**: drivers, espaço em disco, programas em segundo
  plano, reinício pendente e reparo do sistema de arquivos.
- As etapas que falhavam sem avisar (espaço em disco, dispositivos, inicialização, segundo
  plano, reinício pendente) agora aparecem no resultado com o motivo da falha.
- Entradas de inicialização de outros usuários deixam de falhar em silêncio: o app avisa que
  precisa de administrador.
- Botões VERIFICAR e CORRIGIR não rodam mais ao mesmo tempo (antes dava para disparar os dois
  e a lista final ficava imprevisível).

**CENTRAL DE DRIVERS · ATUALIZAR DRIVERS de verdade**
- O relatório agora **confere cada pacote** do Windows Update (download e instalação) e diz
  quantos drivers foram instalados de verdade, com o código de erro quando algum falha.
- Acabou o **"Instalação concluída" falso**: o Windows Update também devolve *extensões* de
  aplicativo (no PC de teste, "Canon Extension Driver Update"), que não são driver de
  dispositivo. Elas ficam de fora e a tela explica o que havia no catálogo.
- Quando não há driver novo, o app diz **"nenhum driver novo para este PC"** em vez de fingir
  que atualizou.
- **BAIXAR INSTALADOR OFICIAL** (cartão PLACA DE VÍDEO): baixa o `.exe` do seu modelo direto do
  servidor da AMD/NVIDIA, com barra de progresso, tamanho mostrado antes e cancelamento.
- O arquivo é **conferido pela assinatura digital do fabricante** antes de ser aceito (se a
  assinatura não for válida, ele é apagado por segurança) e mostra o SHA-256 para conferência.
- **INSTALAR AGORA** roda o instalador oficial já baixado. A caixa "instalação silenciosa"
  instala sem janelas na AMD/NVIDIA; sem ela, aparece o instalador normal do fabricante.
- Downloads só de servidores oficiais (HTTPS), com checagem de espaço em disco e proteção
  contra o CDN devolver página de erro no lugar do instalador.
- Versão de vídeo da **AMD** agora é comparada no formato certo (Adrenalin 25.10.45.16 × 26.10.2),
  lida do próprio AMD Software — antes a comparação misturava formatos e nunca acusava versão nova.
- Faixas de progresso separadas (baixar × instalar) e mensagens de falha de leitura de WMI/registro
  aparecem na tela em vez de sumirem em silêncio.
- Ponto de restauração, backup e instalação pelo Windows Update pedem administrador de forma
  explícita, e uma exceção inesperada não fecha mais o aplicativo.

---

## Novidades da v0.22 · Gui_Tech

**Botão minimizar vai para a bandeja do sistema**
- Agora o botão **minimizar** não deixa o Gui_Tech na barra de tarefas: a janela some e só fica
  o ícone do Gui_Tech na bandeja, perto do relógio.
- **Clique no ícone** (uma vez) e a janela volta para a frente; clique de novo no botão
  minimizar, ela volta para a bandeja.
- Para sair de verdade: botão direito no ícone → **Sair**.
- O ícone pode ficar escondido atrás da seta `^` ao lado do relógio. Arraste-o para a barra
  de tarefas para deixá-lo sempre visível.

---

## Novidades da v0.21.1 · Gui_Tech

**Iniciar com o Windows · correção**
- O Gui_Tech precisa de administrador, e o Windows 11 não inicia apps elevados pela chave Run de
  forma confiável: o pedido do UAC aparece no logon e, se não for aceito, o programa não abre.
- Agora a opção cria uma **tarefa agendada no logon com privilégios máximos**. O Gui_Tech abre
  direto na bandeja, sem pedir confirmação, inclusive em notebook na bateria.
- Quem já tinha a opção ligada: na próxima abertura do Gui_Tech a chave antiga é convertida
  sozinha. Se o caminho do .exe mudar, o aviso na tela pede para desligar e ligar de novo.
- Se a bandeja do Windows ainda estiver carregando no logon, o ícone é criado de novo sozinho
  (por até 1 minuto), em vez de o programa ficar sem ícone.

---

## Novidades da v0.21 · Gui_Tech

**BIBLIOTECA · desempenho por jogo**
- Cada jogo agora tem ajustes de CPU no card DESEMPENHO, além da prioridade do processo.
- **Afinidade de núcleos**: marque em quais núcleos o jogo pode rodar (ou TODOS). Serve para
  isolar o jogo de programas pesados e para CPUs híbridas (núcleos P e E).
- **Prioridade das threads**: Padrão, Acima (+1) ou Máxima (+2) para as threads do jogo. O
  Gui_Tech reaplica o ajuste a cada 4 s, porque o jogo cria threads novas enquanto roda.
- Tudo vale apenas enquanto o jogo está aberto: ao fechar, o processo termina e nada fica
  alterado no Windows. O perfil fica salvo por jogo.
- O PERFIL PARA MAIS FPS agora também coloca as threads em +1.

**OTIMIZA · cache de arquivos do sistema**
- A limpeza de memória agora também esvazia o cache de arquivos do sistema (o "Empty System
  Working Set" do RAMMap), devolvendo ao Windows a RAM usada como cache de leitura e escrita.
- O resultado mostra "cache de arquivos do sistema limpo". Precisa de administrador (o Gui_Tech
  já abre assim); sem permissão, o restante da limpeza continua funcionando e o programa avisa
  que apenas o cache não foi limpo.

---

## Novidades da v0.20 · Gui_Tech

**VERIFICAR PC · botão CORRIGIR**
- Depois da verificação, o botão CORRIGIR aplica as otimizações seguras de uma vez.
- Limpa temporários e cache (inclusive restos do Windows Update e da entrega de atualizações),
  devolve a memória em espera e remove entradas de inicialização que apontam para arquivos
  que não existem mais — com backup, restaurável na página Inicialização.
- Roda a verificação de novo logo depois, para a lista já mostrar o estado corrigido.
- Continua sem tocar em documentos, na Lixeira ou em programas abertos.

**OVERCLOCK · temperatura máxima da GPU**
- Novo seletor de limite de temperatura (60–95 °C) na página Overclock.
- O Gui_Tech monta uma curva de ventoinha de 5 pontos a partir do limite escolhido, aplica
  pela API oficial de vídeo da AMD (Overdrive8) e confere na leitura de volta se o driver aceitou.
- Enquanto o limite está ativo, um vigia a cada 3 s aumenta a velocidade da ventoinha se a
  placa passar do limite e alivia quando ela esfria. Em calor extremo, devolve ao driver.
- VOLTAR AO AUTOMÁTICO restaura a curva de fábrica. Ao fechar o programa a ventoinha também
  volta sozinha ao controle do driver; o limite escolhido fica salvo para a próxima abertura.
- Requer placa AMD com driver recente (RDNA em diante). Em NVIDIA (ou sem suporte no driver)
  o seletor fica desativado, porque a curva não é exposta por uma API pública.

---

## Novidades da v0.19 · Gui_Tech

**Tela inicial · VERIFICAR PC**
- Botão novo no topo da Visão geral: escaneia o PC inteiro em um clique.
- Confere memória RAM, temporários e cache, saúde dos discos (S.M.A.R.T.), espaço livre,
  dispositivos com problema, programas de inicialização, o que roda em segundo plano,
  sistema de arquivos e reinício pendente de atualizações.
- Corrige sozinho o que é seguro: apaga temporários/cache acumulado e, só se a RAM estiver
  acima de 75% de uso, limpa a memória em espera. Nunca apaga documentos nem a Lixeira.
- No fim mostra a lista com selo por item: CORRIGIDO, OK, PODE OTIMIZAR e ATENÇÃO,
  com o que ainda dá para melhorar e onde resolver.
- Com administrador, também roda `chkdsk /scan` (só leitura) para procurar erros no disco.

---

## Novidades da v0.18 · Gui_Tech

**Iniciar com o Windows, minimizado na bandeja**
- Novo interruptor em Gui_Tech FERRAMENTAS › Inicialização: "Iniciar com o Windows".
- O programa abre junto com o login só com o ícone na bandeja do sistema, sem janela na tela.
- Duplo clique no ícone abre o Gui_Tech; botão direito mostra "Abrir Gui_Tech" e "Sair".
- O Windows pede a confirmação de administrador uma vez ao iniciar (o app precisa dela para os sensores).

**CENTRAL DE DRIVERS · ATUALIZAR DRIVERS**
- Um botão faz tudo: procura no Windows Update os drivers novos e os ausentes, baixa e instala.
- Depois pede ao Windows para procurar driver nos dispositivos conectados (pnputil).
- No fim, mostra o resultado e lista os dispositivos que continuaram com problema.
- Drivers de vídeo continuam pelo site do fabricante, para escolher Game Ready ou Studio.

**Gui_Tech OTIMIZA · MODO JOGO**
- Plano de energia Alto desempenho, Modo de Jogo ligado e gravação em segundo plano (Game DVR) desligada.
- Fecha os programas que estão rodando sem janela e limpa a memória em espera.
- Não toca no que tem janela aberta: nada de perder documento sem salvar.

**Gui_Tech OTIMIZA · MODO TRABALHO**
- Plano de energia Equilibrado e limpeza da RAM sem fechar nada.
- Pensado para muitas abas do navegador e vários programas abertos ao mesmo tempo.
- Mostra quanta memória foi liberada e quanto está livre depois.

---

## Novidades da v0.17 · Gui_Tech

**Gui_Tech OTIMIZA · programas em segundo plano e inativos**
- A lista agora inclui programas sem janela e mostra quem está parado (mede o uso de CPU por ~0,7 s).
- Botões SUGERIDOS, 2º PLANO + INATIVOS, LIMPAR e FECHAR 2º PLANO AGORA.
- Com janela o programa recebe o pedido de fechar (como clicar no X); sem janela o processo é encerrado.
- Componentes do Windows ficam fora da lista.

**Gui_Tech OTIMIZA · limpeza de cache do sistema**
- Temporários do usuário e do Windows, relatórios de erro, miniaturas e ícones, cache do DirectX,
  despejos de memória, cache do Windows Update, entrega de atualizações e Lixeira.
- ANALISAR mede cada item antes de apagar; arquivo em uso fica para a próxima.

**Gui_Tech OTIMIZA · manutenção do sistema**
- SFC (arquivos do Windows), DISM (verificar e reparar a imagem) e CHKDSK (verificar disco ou
  agendar reparo no reinício), com log ao vivo, progresso e botão CANCELAR.

**Novo nome e identidade**
- Programa, executável e configuração: Gui_Tech, `GuiTech.exe` e `%AppData%\GuiTech`.
- Tema grafite + roxo, central de drivers integrada ao Windows Update.

---

## Novidades da v0.16

**Nova página · RESET FERRAMENTAS › Inicialização**
- Lista os programas que abrem junto com o Windows: chaves Run do registro (usuário, todos os usuários, 32 e 64 bits)
  e pastas Inicializar, com descrição, fabricante e aviso quando o arquivo do programa não existe mais.
- Interruptor para ativar/desativar, igual ao Gerenciador de Tarefas: a entrada continua lá e pode ser religada.
- Botão **REMOVER** com confirmação; o programa continua instalado e tudo pode ser desfeito em **Restaurar removidos**.

**Nova página · RESET FERRAMENTAS › Instalar apps**
- Os essenciais depois de formatar o PC: Steam, Google Chrome, Discord, Lightshot, 7-Zip, WinRAR e VLC.
- Instala pelo winget (instalador de pacotes da Microsoft que já vem no Windows): versão oficial mais recente,
  sem janelas, um app de cada vez, com o resultado de cada um na lista.
- Mostra o que já está instalado; se o winget ainda não existir (PC recém-formatado), um botão abre a Microsoft Store
  para atualizar o "Instalador de Aplicativo".

**Nova categoria · RESET MAKER** (Arduino, ESP32 e conversores USB-serial)
- **Portas COM**: a limpeza de dispositivos fantasmas e de números COM presos saiu de Otimização e ganhou página própria.
- **Drivers seriais**: diagnóstico das placas com CH340/CH341, CP2102/CP210x, FTDI, Prolific PL2303 e Arduino
  (sem driver, código 10, código 43, FTDI falsificado travado em PID 0000), com a orientação de cada caso.
- **REINSTALAR** remove o dispositivo e faz o Windows detectar de novo com o melhor driver.
- Lista as versões de driver guardadas no Windows para cada chip, com **REMOVER** (backup antes): volta para uma versão
  anterior quando a nova não funciona com clones, ou desinstala de vez. **Restaurar removidos** desfaz.
- **Drivers incluídos no app**: CH340 4.0, CP210x 11.6, FTDI 2.12.36.20 e Arduino, oficiais e assinados, instalam sem internet
  (ideal logo depois de formatar), com a placa conectada ou não. Também abre a página oficial de cada fabricante.
- **Arduino IDE**: instalar/atualizar pelo winget, fechar programas que prendem a porta COM e limpar o cache de índices
  quando a IDE trava em "Downloading index".

**Novo benchmark · Ray tracing (GPU)**
- Teste em Direct3D 12 com DXR 1.1 que usa os núcleos de ray tracing (RTX 20+, Radeon RX 6000+, Intel Arc): 4 mil objetos,
  reflexos de reflexos, sombras suaves, oclusão de ambiente e iluminação indireta, com a cena refeita a cada frame.
- Feito para comparar com confiança: aquecimento sem medir, 3 passadas iguais, nota pela mediana e variação entre passadas
  (selo de "resultado confiável" abaixo de 3%); tempo de frame medido pelo relógio da própria GPU.
- Presets Normal, Alta e Extrema em 1080p, 1440p e 4K; histórico comparando só resultados do mesmo preset e resolução.
- Referência: RX 9070, Alta 1440p = 878 pts (87,8 FPS), variação 0,5%.

**Overlay personalizável**
- Na aba Overlay, escolha o que aparece: FPS, frametime, 1% low, gráfico; GPU (uso, temperatura, hotspot, clock do núcleo,
  clock da memória, consumo, tensão, ventoinha, limitador NVIDIA); VRAM (usada, %, temperatura); CPU (uso, núcleo mais
  ocupado, temperatura, clock médio, clock máximo, consumo); RAM (GB, %); nome do jogo e hora. Atalhos SÓ FPS, PADRÃO e TUDO.
- Cada componente vira uma linha só com os itens escolhidos; a escolha fica salva e vale na hora.

**RESET JOGOS**
- Novo atalho em Extras: **-allow_third_party_software** (CS2), que libera captura de jogo do OBS, ReShade e overlays
  bloqueados pelo modo confiável; o aviso sobre o Fator de Confiança aparece ao passar o mouse.

**Abertura mais rápida**
- A janela aparece em ~0,5 s (antes ~4,6 s). Informações do sistema, sensores e placa de vídeo carregam em paralelo,
  em segundo plano, e cada página é preenchida quando a sua parte fica pronta.
- O aviso "sem sensor da CPU" não aparece mais por engano enquanto o hardware ainda está sendo detectado.

**Correção · Overlay de jogos**
- O overlay agora fica por cima de jogos em janela sem borda que também se marcam como "sempre no topo"
  (ex.: GTA V e Cyberpunk 2077), inclusive depois de Alt+Tab.
- Aparece no monitor onde o jogo está, não só no monitor principal.
