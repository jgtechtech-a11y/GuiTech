using System.Windows;

namespace GuiTech;

/// <summary>
/// Tela de abertura: aparece em ~1 segundo, enquanto a extração do exe single-file,
/// o nascimento do WPF e a detecção de hardware preparam a janela principal (20–40 s
/// na primeira abertura). Sem ela, essa espera parece o programa travado.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        // Mesma fonte de versão do rodapé do menu: sempre a real do executável.
        VersionText.Text = "v" + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?");
    }
}
