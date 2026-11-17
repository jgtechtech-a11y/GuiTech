using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.Win32;

namespace GuiTech;

/// <summary>Um ajuste do debloat: mostra o estado atual, aplica e desfaz (volta ao padrão do Windows).</summary>
public sealed class DebloatItem : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Level { get; init; } = "RECOMENDADO";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Note { get; set; } = "";

    /// <summary>As mudanças só aparecem na barra de tarefas/menu depois de reiniciar o Explorer.</summary>
    public bool NeedsExplorer { get; set; }
    public bool CanUndo { get; init; } = true;

    internal Func<bool> Read { get; init; } = () => false;
    internal Func<string?> Apply { get; init; } = () => null;
    internal Action Undo { get; init; } = () => { };

    public bool IsChecked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Changed(nameof(IsChecked)); }
    }
    private bool _checked;

    public bool IsApplied
    {
        get => _applied;
        private set
        {
            if (_applied == value) return;
            _applied = value;
            Changed(nameof(IsApplied));
            Changed(nameof(StateText));
            Changed(nameof(StateBrush));
            Changed(nameof(CanSelect));
        }
    }
    private bool _applied;

    public bool CanSelect => !_applied;
    public string StateText => _applied ? "Aplicado" : "Não aplicado";
    public Brush StateBrush => _applied ? DebloatPalette.Good : DebloatPalette.Faint;
    public Brush LevelBrush => Level switch
    {
        "RECOMENDADO" => DebloatPalette.Accent,
        "AVANÇADO" => DebloatPalette.Warn,
        _ => DebloatPalette.Muted,
    };
    public Brush LevelBg => Level switch
    {
        "RECOMENDADO" => DebloatPalette.AccentSoft,
        "AVANÇADO" => DebloatPalette.WarnSoft,
        _ => DebloatPalette.Neutral,
    };

    public bool HasNote => Note.Length > 0;
    public string UndoTip => CanUndo ? "Volta este ajuste ao padrão do Windows" : "Não dá para desfazer: os apps voltam pela Microsoft Store";

    public void Refresh()
    {
        bool now;
        try { now = Read(); } catch { now = false; }
        IsApplied = now;
        IsChecked = now;
    }

    internal string? ApplyNow() => Apply();
    internal void UndoNow() => Undo();

    private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Cores fixas usadas pelo código (o XAML usa DynamicResource; aqui os pincéis são congelados).</summary>
internal static class DebloatPalette
{
    public static readonly Brush Good = Solid(0x4A, 0xA1, 0x6D);
    public static readonly Brush Warn = Solid(0xCA, 0x9C, 0x48);
    public static readonly Brush WarnSoft = Solid(0x2B, 0x24, 0x17);
    public static readonly Brush Accent = Solid(0x4F, 0xB3, 0xA9);
    public static readonly Brush AccentSoft = Solid(0x16, 0x33, 0x2F);
    public static readonly Brush Muted = Solid(0xA0, 0xA6, 0xA3);
    public static readonly Brush Neutral = Solid(0x13, 0x18, 0x16);
    public static readonly Brush Faint = Solid(0x7E, 0x86, 0x83);

    private static Brush Solid(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Catálogo do debloat, baseado no que o Win11Debloat faz (github.com/Raphire/Win11Debloat), reescrito em C#:
/// cada item sabe se está aplicado, como aplicar e como voltar ao padrão do Windows. Nada de segurança é mexido
/// (Defender, Firewall, UAC, Windows Update em si, VBS/HVCI).
/// </summary>
public static class DebloatCatalog
{
    private const string Rec = "RECOMENDADO";
    private const string Opt = "OPCIONAL";
    private const string Adv = "AVANÇADO";

    public const string Privacy = "Privacidade e telemetria";
    public const string Ai = "Inteligência artificial";
    public const string Ui = "Interface";
    public const string Sys = "Sistema";

    // ------------------------------------------------------------------ catálogo

    public static List<DebloatItem> All()
    {
        var items = new List<DebloatItem>
        {
        // ---------------- privacidade ----------------
        new DebloatItem
        {
            Id = "telemetry", Category = Privacy, Level = Rec,
            Title = "Telemetria e dados de diagnóstico",
            Description = "Impede o envio de dados de uso, ID de publicidade, reconhecimento de fala/escrita e histórico de " +
                          "atividades para a Microsoft. O serviço DiagTrack é parado e desativado.",
            Note = "Em PCs Insider isso pode segurar atualizações de prévia até reativar a telemetria.",
            Read = () => TelemetryOps.All(OpOk),
            Apply = () =>
            {
                var err = ApplyOps(TelemetryOps);
                Service("stop", "DiagTrack");
                return err;
            },
            Undo = () =>
            {
                UndoOps(TelemetryOps);
                Service("start", "DiagTrack");
            },
        },
        Reg("suggestions", Privacy, Rec, "Sugestões, anúncios e dicas do Windows",
            "Desliga os cartões patrocinados do Iniciar e das Configurações, as dicas de \"sugestão\" e o rastreio de " +
            "experiências personalizadas.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0, 1),
            new RegOp(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications", "EnableAccountNotifications", 0, 1)),
        Reg("lockscreen", Privacy, Rec, "Dicas e \"curiosidades\" na tela de bloqueio",
            "Mantém a sua imagem na tela de bloqueio, sem os textos e sobreposições promocionais da Microsoft.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0, 1)),
        Reg("searchbing", Privacy, Rec, "Pesquisa sem Bing e sem Cortana",
            "A Pesquisa do Windows passa a procurar só no PC: sem resultados da web, sem anúncios e sem o assistente Cortana.",
            new RegOp(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, 1),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "CortanaConsent", 0, 1)),
        Reg("deliveryopt", Privacy, Rec, "Otimização de Entrega sem envio para a internet",
            "O Windows deixa de usar a sua internet para baixar/enviar atualizações a outros PCs. Continua baixando da própria Microsoft.",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0, 1)),
        Reg("edgeads", Privacy, Rec, "Anúncios e sugestões dentro do Edge",
            "Tira do Edge a barra de compras, as recomendações de conteúdo, os sites patrocinados e as pesquisas sugeridas.",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "NewTabPageContentEnabled", 0, 1),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "NewTabPageHideDefaultTopSites", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled", 0, 1),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "ShowRecommendationsEnabled", 0, 1),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "SpotlightExperiencesAndRecommendationsEnabled", 0, 1),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "UserFeedbackAllowed", 0, 1)),
        Reg("location", Privacy, Opt, "Serviços de localização",
            "Desliga o GPS/posição do Windows para todos os apps.",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1, 0)),
        Reg("findmydevice", Privacy, Opt, "Localizar meu dispositivo",
            "Desliga o rastreio do PC pela sua conta Microsoft (\"Localizar meu dispositivo\").",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0, 1)),
        Reg("settings365", Privacy, Opt, "Anúncios de assinatura nas Configurações",
            "Esconde os cartões que oferecem Microsoft 365 e outros serviços dentro das Configurações.",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", 1, 0)),
        Reg("notifications", Privacy, Adv, "Notificações do sistema",
            "Desliga todos os balões de notificação do Windows.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0, 1)),

        // ---------------- IA ----------------
        Reg("copilot", Ai, Rec, "Copilot do Windows",
            "Remove o botão do Copilot e desliga o assistente no sistema.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, 0)),
        Reg("recall", Ai, Rec, "Recall (capturas automáticas da tela)",
            "Impede o Windows de tirar e guardar capturas do que você faz, para não serem indexadas pela IA.",
            new RegOp(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "AllowRecallEnablement", 0, 1),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "TurnOffSavingSnapshots", 1, 0)),
        Reg("clicktodo", Ai, Rec, "Click to Do (ações de IA no que está na tela)",
            "Desliga o recurso que analisa a tela e sugere ações de IA ao clicar com a tecla Windows.",
            new RegOp(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1, 0)),
        Reg("aisvc", Ai, Opt, "Serviço de IA do Windows sem início automático",
            "O WSAIFabricSvc (base dos recursos de IA) passa a iniciar só quando algum recurso realmente pedir.",
            new RegOp(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WSAIFabricSvc", "Start", 3, 2, true)),

        // ---------------- interface ----------------
        Reg("widgets", Ui, Rec, "Widgets da barra de tarefas",
            "Remove o botão de Widgets e o painel de notícias/clima que a Microsoft empurra na barra de tarefas.",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0, 1)),
        Reg("chat", Ui, Rec, "Ícone Chat / Meet Now",
            "Tira da barra de tarefas o ícone de conversas que o Windows instala por padrão.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0, 1),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1, 0)),
        Reg("startphone", Ui, Opt, "Celular no menu Iniciar",
            "Esconde o painel que mostra o celular pareado dentro do Iniciar.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe", "IsEnabled", 0, 1)),
        Reg("startrecommended", Ui, Opt, "Recomendações no menu Iniciar",
            "Esconde a seção \"Recomendado\" (apps e arquivos sugeridos) embaixo dos apps fixados.",
            new RegOp(Registry.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\Explorer", "HideRecommendedSection", 1, 0)),
        Reg("spotlight", Ui, Opt, "Destaques do Windows na área de trabalho",
            "Impede o Windows de trocar o seu papel de parede por imagens promocionais e tira o ícone \"Saiba mais\".",
            new RegOp(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\CloudContent", "DisableSpotlightCollectionOnDesktop", 1, 0),
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{2cc5ca98-6485-489a-920e-b3e88a6ccce3}", 1, 0)),
        new DebloatItem
        {
            Id = "settingshome", Category = Ui, Level = Opt,
            Title = "Página inicial das Configurações",
            Description = "Abre as Configurações direto na última página usada, sem a vitrine com recomendações e anúncios.",
            Read = () => GetStr(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility") == "hide:home",
            Apply = () => { SetStr(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility", "hide:home"); return null; },
            Undo = () => DelValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility"),
        },
        Reg("taskbarleft", Ui, Opt, "Barra de tarefas alinhada à esquerda",
            "Volta o menu Iniciar e os ícones para a esquerda, como no Windows 10.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", 0, 1)),
        Reg("combinenever", Ui, Opt, "Nunca combinar botões da barra de tarefas",
            "Mostra um botão por janela, com o título, em vez de agrupar tudo no ícone do programa.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarGlomLevel", 2, 0)),
        new DebloatItem
        {
            Id = "contextmenu", Category = Ui, Level = Opt,
            Title = "Menu de contexto clássico (Windows 10)",
            Description = "Volta o menu do botão direito completo, sem precisar clicar em \"Mostrar mais opções\".",
            NeedsExplorer = true,
            Read = () => GetStr(Registry.CurrentUser, ContextMenuKey, "") == "",
            Apply = () =>
            {
                using var k = Registry.CurrentUser.CreateSubKey(ContextMenuKey);
                k.SetValue("", "", RegistryValueKind.String);
                return null;
            },
            Undo = () =>
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(ContextMenuParent, false); } catch { }
            },
        },
        Reg("fileext", Ui, Opt, "Mostrar extensões de arquivo",
            "Mostra o tipo real de cada arquivo (.exe, .pdf, .jpg) no Explorador.",
            new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1)),

        // ---------------- sistema ----------------
        Reg("faststartup", Sys, Opt, "Desativar a Inicialização Rápida",
            "O Windows passa a desligar de verdade em vez de hibernar o núcleo. Boot pode ficar alguns segundos mais lento, " +
            "mas evita estados \"meio ligados\" e libera o disco ao desligar.",
            new RegOp(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1)),
        Reg("driverupdates", Sys, Adv, "Bloquear drivers pelo Windows Update",
            "O Windows Update deixa de instalar drivers de dispositivo; você instala os drivers pelo fabricante " +
            "(ou pela Central de drivers do Gui_Tech).",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 3, 0),
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\PolicyState", "ExcludeWUDrivers", 1, 0)),
        Reg("devicemetadata", Sys, Opt, "Apps automáticos de fabricantes",
            "Impede o Windows de baixar da internet os apps e metadados que o fabricante do PC cadastra.",
            new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Device Metadata", "PreventDeviceMetadataFromNetwork", 1, 0)),
        new DebloatItem
        {
            Id = "telemetrytasks", Category = Sys, Level = Rec,
            Title = "Tarefas agendadas de telemetria",
            Description = "Desativa as tarefas do Agendador que coletam e enviam dados de compatibilidade, uso e diagnóstico.",
            Note = "São tarefas do próprio Windows; o sistema continua funcionando normalmente.",
            Read = () => TelemetryTasks.All(t => TaskState(t.Path, t.Name) != true),
            Apply = () =>
            {
                int fails = 0;
                foreach (var (path, name) in TelemetryTasks)
                    if (!SetTask(path, name, false)) fails++;
                return fails == 0 ? null : $"{fails} tarefa(s) não puderam ser alteradas";
            },
            Undo = () =>
            {
                foreach (var (path, name) in TelemetryTasks) SetTask(path, name, true);
            },
        },
        new DebloatItem
        {
            Id = "stickykeys", Category = Sys, Level = Opt,
            Title = "Atalho das Teclas de Aderência",
            Description = "Desliga o atalho que ativa as Teclas de Aderência ao apertar Shift 5 vezes (aparece uma janela no meio do jogo).",
            Read = () => GetStr(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags") == "506",
            Apply = () => { SetStr(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "506"); return null; },
            Undo = () => SetStr(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "510"),
        },
        new DebloatItem
        {
            Id = "bloatapps", Category = Sys, Level = Adv, CanUndo = false,
            Title = "Remover apps pré-instalados",
            Description = "Desinstala os apps que o Windows instala sozinho e quase ninguém usa: Notícias, Clima, " +
                          "Solitaire, Clipchamp, Mapas, People, Skype, To Do, Feedback, Get Help, Office e Celular.",
            Note = "Não dá para desfazer por aqui: se quiser um deles de volta, instale pela Microsoft Store. " +
                   "Feche os apps antes de aplicar.",
            Read = () => BloatInstalled is int n && n == 0,
            Apply = () =>
            {
                string? err = RemoveBloatApps();
                RefreshBloatCount();
                return err;
            },
            Undo = () => { },
        },
        };

        // Avisos e itens que só aparecem depois de reiniciar o Explorer.
        Extras(items, "edgeads", note: "Vale para o Microsoft Edge. As páginas e a navegação continuam iguais.");
        Extras(items, "location", note: "Clima, mapas e \"perto de mim\" deixam de usar a sua posição.");
        Extras(items, "notifications", note: "Aplicativos não conseguem mais avisar nada (inclusive atualizações e lembretes).");
        Extras(items, "widgets", explorer: true);
        Extras(items, "chat", explorer: true);
        Extras(items, "startphone", explorer: true);
        Extras(items, "startrecommended", explorer: true);
        Extras(items, "spotlight", explorer: true, note: "Se você usa os Destaques do Windows de propósito, não marque este item.");
        Extras(items, "taskbarleft", explorer: true, note: "Questão de gosto: não mexe em desempenho.");
        Extras(items, "combinenever", explorer: true);
        Extras(items, "fileext", explorer: true, note: "Ajuda a evitar golpes com arquivos disfarçados.");
        Extras(items, "faststartup", note: "Precisa reiniciar o PC para valer. Não afeta a Hibernação, se você a usa.");
        Extras(items, "driverupdates", note: "Alguns notebooks dependem do Windows Update para drivers de toque, áudio ou Wi-Fi.");

        return items;
    }

    // ------------------------------------------------------------------ privacidade: valores

    private static readonly RegOp[] TelemetryOps =
    {
        new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1),
        new RegOp(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1),
        new RegOp(Registry.CurrentUser, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0, 1),
        new RegOp(Registry.CurrentUser, @"Software\Microsoft\Input\TIPC", "Enabled", 0, 1),
        new RegOp(Registry.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, 0),
        new RegOp(Registry.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, 0),
        new RegOp(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, 1),
        new RegOp(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, 2, true),
    };

    private const string ContextMenuParent = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    private const string ContextMenuKey = ContextMenuParent + @"\InprocServer32";

    // ------------------------------------------------------------------ itens de registro

    /// <summary>Item simples: aplicado quando todos os valores estão no estado desejado; desfazer devolve o padrão.</summary>
    private static DebloatItem Reg(string id, string category, string level, string title, string description, params RegOp[] ops)
        => new()
        {
            Id = id, Category = category, Level = level, Title = title, Description = description,
            Read = () => ops.All(OpOk),
            Apply = () => ApplyOps(ops),
            Undo = () => UndoOps(ops),
        };

    /// <summary>Acrescenta aviso e a marca de "precisa reiniciar o Explorer" a um item já montado.</summary>
    private static void Extras(List<DebloatItem> items, string id, bool explorer = false, string? note = null)
    {
        var item = items.FirstOrDefault(i => i.Id == id);
        if (item == null) return;
        item.NeedsExplorer = explorer;
        if (note != null) item.Note = note;
    }

    private readonly record struct RegOp(RegistryKey Hive, string Path, string Name, int On, int? Off, bool OnlyIfExists = false);

    private static bool OpOk(RegOp op)
    {
        try
        {
            if (op.OnlyIfExists && !KeyExists(op.Hive, op.Path)) return true; // recurso não existe neste PC
            return GetInt(op.Hive, op.Path, op.Name) == op.On;
        }
        catch { return false; }
    }

    private static string? ApplyOps(IEnumerable<RegOp> ops)
    {
        var fails = new List<string>();
        foreach (var op in ops)
        {
            try
            {
                if (op.OnlyIfExists && !KeyExists(op.Hive, op.Path)) continue;
                SetInt(op.Hive, op.Path, op.Name, op.On);
            }
            catch (Exception ex) { fails.Add($"{op.Name}: {ex.Message}"); }
        }
        return fails.Count == 0 ? null : $"{fails.Count} valor(es) recusados — {fails[0]}";
    }

    private static void UndoOps(IEnumerable<RegOp> ops)
    {
        foreach (var op in ops)
        {
            try
            {
                if (op.Off is int off) SetInt(op.Hive, op.Path, op.Name, off);
                else DelValue(op.Hive, op.Path, op.Name);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ registro: primitivas

    private static bool KeyExists(RegistryKey hive, string path)
    {
        using var k = hive.OpenSubKey(path);
        return k != null;
    }

    private static int? GetInt(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        object? v = k?.GetValue(name);
        return v switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out int n) => n,
            _ => null,
        };
    }

    private static string? GetStr(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        return k?.GetValue(name) as string;
    }

    private static void SetInt(RegistryKey hive, string path, string name, int value)
    {
        using var k = hive.CreateSubKey(path, true) ?? throw new InvalidOperationException("sem acesso ao registro");
        k.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void SetStr(RegistryKey hive, string path, string name, string value)
    {
        using var k = hive.CreateSubKey(path, true) ?? throw new InvalidOperationException("sem acesso ao registro");
        k.SetValue(name, value, RegistryValueKind.String);
    }

    private static void DelValue(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path, true);
        k?.DeleteValue(name, false);
    }

    /// <summary>Liga/para um serviço na hora (o registro sozinho só vale no próximo boot).</summary>
    private static void Service(string action, string name)
    {
        try
        {
            var psi = new ProcessStartInfo("sc.exe", $"{action} \"{name}\"")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit(8000);
        }
        catch { }
    }

    // ------------------------------------------------------------------ agendador de tarefas (COM)

    private static readonly (string Path, string Name)[] TelemetryTasks =
    {
        (@"\Microsoft\Windows\Application Experience", "Microsoft Compatibility Appraiser"),
        (@"\Microsoft\Windows\Application Experience", "Microsoft Compatibility Appraiser Exp"),
        (@"\Microsoft\Windows\Application Experience", "ProgramDataUpdater"),
        (@"\Microsoft\Windows\Application Experience", "StartupAppTask"),
        (@"\Microsoft\Windows\Customer Experience Improvement Program", "Consolidator"),
        (@"\Microsoft\Windows\Customer Experience Improvement Program", "UsbCeip"),
        (@"\Microsoft\Windows\DiskDiagnostic", "Microsoft-Windows-DiskDiagnosticDataCollector"),
        (@"\Microsoft\Windows\Autochk", "Proxy"),
    };

    /// <summary>Estado da tarefa: null quando ela não existe neste Windows.</summary>
    private static bool? TaskState(string folderPath, string name)
    {
        try
        {
            dynamic service = ConnectScheduler();
            dynamic folder = service.GetFolder(folderPath);
            dynamic task = folder.GetTask(name);
            return (bool)task.Enabled;
        }
        catch { return null; }
    }

    private static bool SetTask(string folderPath, string name, bool enabled)
    {
        try
        {
            dynamic service = ConnectScheduler();
            dynamic folder = service.GetFolder(folderPath);
            dynamic task = folder.GetTask(name);
            task.Enabled = enabled;
            return true;
        }
        catch (System.IO.FileNotFoundException) { return true; } // tarefa não existe neste Windows: nada a fazer
        catch { return false; }
    }

    private static dynamic ConnectScheduler()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
                   ?? throw new InvalidOperationException("Agendador de Tarefas indisponível.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    // ------------------------------------------------------------------ apps pré-instalados

    private static readonly string[] BloatApps =
    {
        "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.Clipchamp", "Microsoft.GetHelp",
        "Microsoft.Getstarted", "Microsoft.MicrosoftOfficeHub", "Microsoft.MicrosoftSolitaireCollection",
        "Microsoft.People", "Microsoft.SkypeApp", "Microsoft.Todos", "Microsoft.WindowsFeedbackHub",
        "Microsoft.WindowsMaps", "Microsoft.YourPhone",
    };

    private static int? _bloatInstalled;

    /// <summary>Quantos apps da lista continuam instalados; null até a contagem terminar.</summary>
    public static int? BloatInstalled => _bloatInstalled;

    private static bool IsBloat(string name) =>
        BloatApps.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pacotes AppX da lista instalados para o usuário atual. Usa a API de implantação do próprio
    /// Windows: nada de abrir PowerShell, o que era (com razão) lido como comportamento suspeito
    /// por antivírus.
    /// </summary>
    private static List<Windows.ApplicationModel.Package> FindBloatPackages()
    {
        var found = new List<Windows.ApplicationModel.Package>();
        try
        {
            var manager = new Windows.Management.Deployment.PackageManager();
            foreach (var pkg in manager.FindPackagesForUser(string.Empty))
            {
                if (IsBloat(pkg.Id.Name)) found.Add(pkg);
            }
        }
        catch { /* sem permissão ou API indisponível: devolve o que já achou */ }
        return found;
    }

    /// <summary>Conta os apps instalados (roda fora da thread da janela: é uma chamada lenta).</summary>
    public static void RefreshBloatCount()
    {
        try
        {
            _bloatInstalled = FindBloatPackages().Select(p => p.Id.Name)
                                                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
        catch { _bloatInstalled = null; }
    }

    private static string? RemoveBloatApps()
    {
        int failed = 0;
        bool any = false;

        foreach (var pkg in FindBloatPackages())
        {
            any = true;
            try
            {
                var result = new Windows.Management.Deployment.PackageManager()
                    .RemovePackageAsync(pkg.Id.FullName)
                    .AsTask().GetAwaiter().GetResult();
                if (result.ExtendedErrorCode != null) failed++;
            }
            catch { failed++; }
        }

        // A cópia "provisionada" (que o Windows reinstala em contas novas) só sai com administrador.
        try
        {
            var manager = new Windows.Management.Deployment.PackageManager();
            foreach (var pkg in manager.FindProvisionedPackages())
            {
                if (!IsBloat(pkg.Id.Name)) continue;
                try
                {
                    manager.DeprovisionPackageForAllUsersAsync(pkg.Id.FamilyName).AsTask().GetAwaiter().GetResult();
                }
                catch { }
            }
        }
        catch { /* sem administrador: os apps do usuário já foram removidos */ }

        if (!any) return null;
        if (failed == 0) return null;
        return "Alguns apps não puderam ser removidos (feche-os e abra o Gui_Tech como administrador).";
    }
}
