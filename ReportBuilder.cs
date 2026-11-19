using System.Net;
using System.Text;
using System.Text.Json;

namespace GuiTech;

public sealed record ReportNode(string Name, string? Value, List<ReportNode>? Children);
public sealed record ReportSensor(string Hardware, string Category, string Name, string Value, string Min, string Max);
public sealed record ReportBench(DateTime Date, string Test, string Summary, double Score = 0);
public sealed record ReportDisk(string Name, string Details, string Verdict, string Reasons, List<KeyValuePair<string, string>> Metrics);
public sealed record ReportGpu(string Name, string Subtitle, List<ReportNode> Sections);

/// <summary>Tudo que entra no relatório exportado.</summary>
public sealed class ReportData
{
    public DateTime Date { get; init; } = DateTime.Now;
    public string Machine { get; init; } = Environment.MachineName;
    public string AppVersion { get; init; } = "";
    public string? LogoBase64 { get; init; }
    public List<ReportNode> System { get; init; } = new();
    public List<ReportGpu> Gpus { get; init; } = new();
    public List<ReportDisk> Disks { get; init; } = new();
    public List<ReportDisk> Power { get; init; } = new();
    public List<ReportDisk> Network { get; init; } = new();
    public List<ReportSensor> Sensors { get; init; } = new();
    public List<ReportBench> Benchmarks { get; init; } = new();
}

/// <summary>Gera o relatório em HTML (arquivo único, abre em qualquer navegador) ou JSON. Números de série ficam de fora.</summary>
public static class ReportBuilder
{
    public static ReportNode? ToNode(InfoNode n)
    {
        string h = n.Header ?? "";
        // privacidade: o relatório costuma ser enviado a terceiros
        if (h.Contains("série", StringComparison.OrdinalIgnoreCase) || h.Contains("serial", StringComparison.OrdinalIgnoreCase)) return null;
        var kids = n.Children.Select(ToNode).Where(k => k != null).Cast<ReportNode>().ToList();
        return new ReportNode(h, string.IsNullOrEmpty(n.Value) ? null : n.Value, kids.Count > 0 ? kids : null);
    }

    public static string Json(ReportData d) => JsonSerializer.Serialize(new
    {
        gerado_em = d.Date.ToString("s"),
        computador = d.Machine,
        versao_do_programa = d.AppVersion,
        sistema = d.System,
        placas_de_video = d.Gpus,
        discos = d.Disks.Select(x => new
        {
            x.Name, x.Details, saude = x.Verdict, x.Reasons,
            metricas = x.Metrics.ToDictionary(m => m.Key, m => m.Value)
        }),
        energia = d.Power.Select(x => new { x.Name, x.Details, estado = x.Verdict, x.Reasons, metricas = x.Metrics.ToDictionary(m => m.Key, m => m.Value) }),
        rede = d.Network.Select(x => new { x.Name, x.Details, estado = x.Verdict, x.Reasons, metricas = x.Metrics.ToDictionary(m => m.Key, m => m.Value) }),
        sensores = d.Sensors,
        benchmarks = d.Benchmarks,
    }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static void Section(StringBuilder sb, string title, List<ReportDisk> items)
    {
        if (items.Count == 0) return;
        sb.Append($"<section><h2>{E(title)}</h2>");
        foreach (var disk in items)
        {
            string cls = disk.Verdict switch { "SAUDÁVEL" or "ESTÁVEL" or "EM USO" => "ok", "SEM DADOS" => "na", _ => "bad" };
            sb.Append($"<div class=\"card\"><div class=\"row\"><b>{E(disk.Name)}</b><span class=\"badge {cls}\">{E(disk.Verdict)}</span></div>");
            if (disk.Details.Length > 0) sb.Append($"<p class=\"muted\">{E(disk.Details)}</p>");
            sb.Append($"<p>{E(disk.Reasons)}</p>");
            if (disk.Metrics.Count > 0)
            {
                sb.Append("<table>");
                foreach (var m in disk.Metrics) sb.Append($"<tr><td class=\"k\">{E(m.Key)}</td><td>{E(m.Value)}</td></tr>");
                sb.Append("</table>");
            }
            sb.Append("</div>");
        }
        sb.Append("</section>");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>
    /// Relatório em CSV (abre no Excel; separador ';' é o padrão do Excel em português).
    /// Uma linha por dado, agrupado por seção — bom para comparar entre PCs e arquivar.
    /// </summary>
    public static string Csv(ReportData d)
    {
        var sb = new StringBuilder(16 * 1024);
        sb.AppendLine("secao;item;valor");

        void Row(string section, string item, string value) =>
            sb.AppendLine($"{Q(section)};{Q(item)};{Q(value)}");

        void Nodes(string section, string prefix, IEnumerable<ReportNode> nodes)
        {
            foreach (var n in nodes)
            {
                string item = prefix.Length == 0 ? n.Name : prefix + " > " + n.Name;
                if (n.Children is { Count: > 0 }) Nodes(section, item, n.Children);
                else Row(section, item, n.Value ?? "");
            }
        }

        void Cards(string section, IEnumerable<ReportDisk> cards)
        {
            foreach (var c in cards)
            {
                Row(section, c.Name + " > situação", c.Verdict);
                if (c.Details.Length > 0) Row(section, c.Name + " > detalhes", c.Details);
                if (c.Reasons.Length > 0) Row(section, c.Name + " > motivos", c.Reasons);
                foreach (var m in c.Metrics) Row(section, c.Name + " > " + m.Key, m.Value);
            }
        }

        Row("Relatório", "Gerado em", d.Date.ToString("dd/MM/yyyy HH:mm"));
        Row("Relatório", "Computador", d.Machine);
        Row("Relatório", "Versão do programa", d.AppVersion);

        Nodes("Sistema", "", d.System);
        foreach (var g in d.Gpus) Nodes("Placa de vídeo: " + g.Name, "", g.Sections);
        foreach (var s in d.Sensors) Row("Sensores", $"{s.Hardware} > {s.Category} > {s.Name}", s.Value);
        foreach (var b in d.Benchmarks)
        {
            Row("Benchmarks", $"{b.Date:dd/MM/yyyy HH:mm} > {b.Test}", b.Summary);
            if (b.Score > 0) Row("Benchmarks", $"{b.Date:dd/MM/yyyy HH:mm} > {b.Test} > pontuação", b.Score.ToString("0.##"));
        }
        Cards("Discos", d.Disks);
        Cards("Bateria e energia", d.Power);
        Cards("Rede", d.Network);
        return sb.ToString();
    }

    /// <summary>Campo CSV: entre aspas quando tiver separador, aspas ou quebra de linha.</summary>
    private static string Q(string? s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }

    /// <summary>Gráfico de barras do histórico de benchmarks (por teste, até as 10 últimas medições).</summary>
    private static void BenchChart(StringBuilder sb, List<ReportBench> benchmarks)
    {
        var groups = benchmarks.Where(b => b.Score > 0)
            .GroupBy(b => b.Test)
            .Select(g => (Name: g.Key, Runs: g.OrderBy(b => b.Date).TakeLast(10).ToList()))
            .Where(g => g.Runs.Count > 0)
            .ToList();
        if (groups.Count == 0) return;

        sb.Append("<div class=\"card\"><h3>Evolução dos benchmarks</h3>");
        foreach (var (name, runs) in groups)
        {
            double max = runs.Max(r => r.Score);
            if (max <= 0) continue;
            sb.Append($"<div class=\"chart\"><p class=\"t\">{E(name)}</p>");
            foreach (var r in runs)
            {
                double pct = Math.Clamp(r.Score / max * 100.0, 1, 100);
                sb.Append($"<div class=\"row2\"><span class=\"lbl\">{r.Date:dd/MM HH:mm}</span>" +
                          $"<span class=\"bar\" style=\"width:{pct:0.#}%\"></span>" +
                          $"<span class=\"val\">{r.Score:N0}</span></div>");
            }
            sb.Append("</div>");
        }
        sb.Append("</div>");
    }

    public static string Html(ReportData d)
    {
        var sb = new StringBuilder(64 * 1024);
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append($"<title>Gui_Tech · Relatório · {E(d.Machine)}</title><style>{Css}</style></head><body><div class=\"wrap\">");

        sb.Append("<header>");
        if (!string.IsNullOrEmpty(d.LogoBase64)) sb.Append($"<img class=\"logo\" alt=\"\" src=\"data:image/png;base64,{d.LogoBase64}\">");
        sb.Append($"<div><h1>Gui_Tech</h1><p class=\"sub\">Relatório de hardware · {E(d.Machine)} · {d.Date:dd/MM/yyyy HH:mm}");
        if (d.AppVersion.Length > 0) sb.Append($" · v{E(d.AppVersion)}");
        sb.Append("</p></div></header>");
        sb.Append("<p class=\"note\">Números de série foram omitidos deste relatório.</p>");

        // Resumo dos discos no topo (o que mais interessa de imediato)
        Section(sb, "Saúde dos discos", d.Disks);
        Section(sb, "Bateria e energia", d.Power);
        Section(sb, "Teste de rede", d.Network);

        if (d.System.Count > 0)
        {
            sb.Append("<section><h2>Sistema</h2>");
            foreach (var n in d.System) { sb.Append($"<div class=\"card\"><h3>{E(n.Name)}</h3>"); Tree(sb, n.Children, 0); sb.Append("</div>"); }
            sb.Append("</section>");
        }

        foreach (var g in d.Gpus)
        {
            sb.Append($"<section><h2>Placa de vídeo · {E(g.Name)}</h2>");
            if (g.Subtitle.Length > 0) sb.Append($"<p class=\"muted\">{E(g.Subtitle)}</p>");
            foreach (var s in g.Sections) { sb.Append($"<div class=\"card\"><h3>{E(s.Name)}</h3>"); Tree(sb, s.Children, 0); sb.Append("</div>"); }
            sb.Append("</section>");
        }

        if (d.Sensors.Count > 0)
        {
            sb.Append("<section><h2>Sensores (leitura no momento da exportação)</h2>");
            foreach (var grp in d.Sensors.GroupBy(s => s.Hardware))
            {
                sb.Append($"<div class=\"card\"><h3>{E(grp.Key)}</h3><table><tr><th>Categoria</th><th>Sensor</th><th>Valor</th><th>Mín.</th><th>Máx.</th></tr>");
                foreach (var s in grp)
                    sb.Append($"<tr><td class=\"muted\">{E(s.Category)}</td><td>{E(s.Name)}</td><td class=\"num\">{E(s.Value)}</td><td class=\"num muted\">{E(s.Min)}</td><td class=\"num muted\">{E(s.Max)}</td></tr>");
                sb.Append("</table></div>");
            }
            sb.Append("</section>");
        }

        if (d.Benchmarks.Count > 0)
        {
            sb.Append("<section><h2>Benchmarks e testes recentes</h2><div class=\"card\"><table><tr><th>Data</th><th>Teste</th><th>Resultado</th></tr>");
            foreach (var b in d.Benchmarks)
                sb.Append($"<tr><td class=\"muted\">{b.Date:dd/MM/yyyy HH:mm}</td><td>{E(b.Test)}</td><td>{E(b.Summary)}</td></tr>");
            sb.Append("</table></div>");
            BenchChart(sb, d.Benchmarks);
            sb.Append("</section>");
        }

        sb.Append("<footer>Gerado pelo Gui_Tech. Dados lidos do Windows, do driver da placa de vídeo e dos sensores da placa-mãe; podem variar conforme o hardware.</footer>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Tree(StringBuilder sb, List<ReportNode>? nodes, int depth)
    {
        if (nodes == null || nodes.Count == 0) return;
        sb.Append(depth == 0 ? "<table>" : "<table class=\"nested\">");
        foreach (var n in nodes)
        {
            if (n.Children != null)
            {
                sb.Append($"<tr><td colspan=\"2\" class=\"grp\">{E(n.Name)}{(n.Value != null ? " · " + E(n.Value) : "")}</td></tr><tr><td colspan=\"2\" class=\"inner\">");
                Tree(sb, n.Children, depth + 1);
                sb.Append("</td></tr>");
            }
            else sb.Append($"<tr><td class=\"k\">{E(n.Name)}</td><td>{E(n.Value)}</td></tr>");
        }
        sb.Append("</table>");
    }

    private const string Css = @"
:root{--bg:#fafbfb;--fg:#101715;--muted:#44544f;--line:#d7e3e0;--card:#fff;--ok:#0f766e;--badbg:#128577;--badfg:#fff}
@media (prefers-color-scheme:dark){:root{--bg:#070b0a;--fg:#e0e5e3;--muted:#a0a6a3;--line:#1c211f;--card:#0e1311;--ok:#6ec8bf;--badbg:#4fb3a9;--badfg:#06231e}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.5 'Segoe UI',system-ui,sans-serif}
.wrap{max-width:980px;margin:0 auto;padding:32px 20px 60px}
header{display:flex;gap:16px;align-items:center;margin-bottom:6px}.logo{width:44px;height:44px}
h1{margin:0;font-size:26px;letter-spacing:.5px}h2{margin:34px 0 12px;font-size:17px;letter-spacing:.4px}h3{margin:0 0 8px;font-size:14px}
.sub,.muted{color:var(--muted)}.sub{margin:2px 0 0}.note{color:var(--muted);font-size:12px;margin:0 0 4px}
.card{background:var(--card);border:1px solid var(--line);border-radius:6px;padding:14px 16px;margin:0 0 12px}
.row{display:flex;justify-content:space-between;gap:12px;align-items:center}
.badge{font-size:11px;font-weight:600;letter-spacing:.6px;padding:3px 10px;border-radius:3px;border:1px solid var(--ok);color:var(--ok)}
.badge.bad,.badge.na{background:var(--badbg);color:var(--badfg);border-color:var(--badbg)}.badge.na{opacity:.6}
table{width:100%;border-collapse:collapse}td,th{padding:5px 8px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left}
th{color:var(--muted);font-weight:600;font-size:12px}.k{color:var(--muted);width:38%}.num{font-family:Consolas,monospace;text-align:right;white-space:nowrap}
.grp{font-weight:600;padding-top:12px}.inner{padding:0 0 0 14px;border-bottom:0}.nested td{font-size:13px}
.chart{margin:0 0 12px}.chart .t{font-weight:600;font-size:13px;margin:0 0 6px}.chart .row2{display:flex;align-items:center;gap:10px;margin:0 0 4px}
.chart .lbl{width:96px;color:var(--muted);font-size:11px;text-align:right;white-space:nowrap}
.chart .bar{height:12px;background:var(--ok);border-radius:2px;min-width:2px;flex:none}.chart .val{font-family:Consolas,monospace;font-size:11px;color:var(--muted)}
footer{margin-top:40px;color:var(--muted);font-size:12px}
@media print{body{background:#fff;color:#000}.card{break-inside:avoid}}
";
}
