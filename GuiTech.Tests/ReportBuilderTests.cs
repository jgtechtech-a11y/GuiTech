using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Relatório exportado (HTML/JSON): estrutura, fuga de caracteres e paleta teal.</summary>
public class ReportBuilderTests
{
    private static ReportData Exemplo()
    {
        var n = new ReportNode("Processador", "Teste", null);
        return new ReportData
        {
            Date = new DateTime(2026, 11, 15, 10, 0, 0),
            Machine = "PC<de>teste",
            AppVersion = "0.25.11",
            System = { n },
        };
    }

    [Fact]
    public void Html_contem_cabecalho_e_dados()
    {
        var html = ReportBuilder.Html(Exemplo());
        Assert.Contains("Gui_Tech", html);
        Assert.Contains("Relatório de hardware", html);
        Assert.Contains("0.25.11", html);
        Assert.Contains("Processador", html);
    }

    [Fact]
    public void Html_escapa_caracteres_especiais_do_nome_da_maquina()
    {
        var html = ReportBuilder.Html(Exemplo());
        Assert.Contains("&lt;de&gt;", html);
        Assert.DoesNotContain("<de>", html);
    }

    [Fact]
    public void Html_usa_paleta_teal_e_sem_roxo()
    {
        var html = ReportBuilder.Html(Exemplo());
        Assert.Contains("--ok:#0f766e", html);   // acento teal do tema claro
        Assert.Contains("--ok:#6ec8bf", html);   // acento teal do tema escuro
        Assert.DoesNotContain("#6d28d9", html);  // roxo antigo
        Assert.DoesNotContain("#b798ee", html);  // roxo antigo escuro
    }

    [Fact]
    public void Json_gerado_e_valido_e_tem_os_campos_principais()
    {
        var json = ReportBuilder.Json(Exemplo());
        Assert.Contains("0.25.11", json);
        Assert.Contains("computador", json);
        Assert.Contains("versao_do_programa", json);
        Assert.Contains("Processador", json);
    }

    [Fact]
    public void Csv_tem_cabecalho_e_uma_linha_por_dado()
    {
        var csv = ReportBuilder.Csv(Exemplo());
        string[] linhas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("secao;item;valor", linhas[0].Trim());
        Assert.Contains("Relatório;Gerado em;15/11/2026 10:00", csv);
        Assert.Contains("Sistema;Processador;Teste", csv);
    }

    [Fact]
    public void Csv_escapa_campo_com_separador()
    {
        var dados = Exemplo();
        dados.System.Add(new ReportNode("Nome", "SSD; 1 TB", null));
        var csv = ReportBuilder.Csv(dados);
        Assert.Contains("\"SSD; 1 TB\"", csv);
    }

    [Fact]
    public void Csv_escapa_aspas_duplicando()
    {
        var dados = Exemplo();
        dados.System.Add(new ReportNode("Apelido", "PC \"do\" escritório", null));
        var csv = ReportBuilder.Csv(dados);
        Assert.Contains("\"PC \"\"do\"\" escritório\"", csv);
    }

    [Fact]
    public void Html_mostra_grafico_quando_ha_pontuacao()
    {
        var dados = Exemplo();
        dados.Benchmarks.Add(new ReportBench(new DateTime(2026, 11, 10, 9, 0, 0), "Cena de jogo (GPU)", "1.000 pts", 1000));
        dados.Benchmarks.Add(new ReportBench(new DateTime(2026, 11, 12, 9, 0, 0), "Cena de jogo (GPU)", "1.200 pts", 1200));
        var html = ReportBuilder.Html(dados);
        Assert.Contains("Evolução dos benchmarks", html);
        Assert.Contains("class=\"bar\"", html);
        Assert.Contains("width:100%", html);   // a maior medição ocupa a barra inteira
    }

    [Fact]
    public void Html_sem_pontuacao_nao_desfia_grafico()
    {
        var dados = Exemplo();
        dados.Benchmarks.Add(new ReportBench(new DateTime(2026, 11, 10, 9, 0, 0), "Disco", "450 MB/s"));
        Assert.DoesNotContain("Evolução dos benchmarks", ReportBuilder.Html(dados));
    }
}
