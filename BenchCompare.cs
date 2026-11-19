using System;
using System.Collections.Generic;
using System.Linq;

namespace GuiTech;

/// <summary>Uma linha da comparação do histórico de benchmarks.</summary>
public sealed record BenchCompareRow(
    string Test,
    string Name,
    double Last,
    double Best,
    double Average,
    /// <summary>Variação (%) entre as duas últimas medições; null quando só existe uma.</summary>
    double? DeltaPercent,
    /// <summary>Quanto a última medição está abaixo/acima do melhor resultado (%).</summary>
    double? VsBestPercent);

/// <summary>
/// Compara os resultados de benchmark guardados no histórico (por teste): última medição,
/// melhor, média e variação em relação à anterior. Não realiza nenhum teste — só lê os dados.
/// </summary>
public static class BenchCompare
{
    /// <summary>Nome amigável do teste gravado em <see cref="BenchRecord.Test"/>.</summary>
    public static string TestName(string test)
    {
        if (test.StartsWith("rt:", StringComparison.OrdinalIgnoreCase))
            return "Ray tracing (" + test[3..] + ")";
        return test switch
        {
            "gpu" => "Cena de jogo (GPU)",
            "sim" => "Simulação de jogo (CPU)",
            "mem" => "Memória e carregamento",
            "disk" => "Disco",
            "oc" => "Gui_Tech OC (estabilidade)",
            _ => test,
        };
    }

    /// <summary>
    /// Uma linha por teste que tenha pontuação válida (maior que zero). A ordem é a do teste
    /// mais recente para o mais antigo, para o que o usuário acabou de rodar aparecer primeiro.
    /// </summary>
    public static List<BenchCompareRow> Build(IEnumerable<BenchRecord> records)
    {
        var rows = new List<BenchCompareRow>();
        foreach (var g in records.Where(r => r.Score > 0).GroupBy(r => r.Test))
        {
            var runs = g.OrderByDescending(r => r.Date).ToList();   // mais nova primeiro
            if (runs.Count == 0) continue;
            double last = runs[0].Score;
            double best = runs.Max(r => r.Score);
            double avg = runs.Average(r => r.Score);
            double? delta = null, vsBest = null;
            if (runs.Count >= 2 && runs[1].Score > 0)
                delta = (last - runs[1].Score) / runs[1].Score * 100.0;
            if (best > 0)
                vsBest = (last - best) / best * 100.0;
            rows.Add(new BenchCompareRow(g.Key, TestName(g.Key), last, best, avg, delta, vsBest));
        }
        return rows
            .OrderByDescending(r => records.Where(x => x.Test == r.Test).Max(x => x.Date))
            .ToList();
    }
}
