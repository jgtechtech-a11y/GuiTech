using System;
using System.Collections.Generic;
using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Comparação do histórico de benchmarks (última, melhor, média e variação).</summary>
public class BenchCompareTests
{
    private static BenchRecord R(double score, int dia, string test = "gpu") =>
        new(new DateTime(2026, 11, dia, 10, 0, 0), test, score, $"{score:N0} pts");

    [Fact]
    public void Historico_vazio_nao_gera_linhas()
    {
        Assert.Empty(BenchCompare.Build(Array.Empty<BenchRecord>()));
    }

    [Fact]
    public void Uma_medicao_nao_tem_variacao()
    {
        var rows = BenchCompare.Build(new List<BenchRecord> { R(500, 10) });
        var r = Assert.Single(rows);
        Assert.Equal(500, r.Last);
        Assert.Equal(500, r.Best);
        Assert.Null(r.DeltaPercent);
        Assert.Equal(0, r.VsBestPercent);
    }

    [Fact]
    public void Variacao_usa_as_duas_ultimas_medicoes()
    {
        var rows = BenchCompare.Build(new List<BenchRecord> { R(100, 10), R(110, 12) });
        var r = Assert.Single(rows);
        Assert.Equal(110, r.Last);        // a mais nova é "última"
        Assert.Equal(110, r.Best);
        Assert.Equal(105, r.Average);
        Assert.Equal(10, r.DeltaPercent!.Value, 3);
    }

    [Fact]
    public void Queda_em_relacao_ao_melhor_aparece_negativa()
    {
        var rows = BenchCompare.Build(new List<BenchRecord> { R(200, 10), R(150, 12) });
        var r = Assert.Single(rows);
        Assert.Equal(-25, r.VsBestPercent!.Value, 3);
        Assert.Equal(-25, r.DeltaPercent!.Value, 3);
    }

    [Fact]
    public void Testes_diferentes_ficam_em_linhas_separadas()
    {
        var rows = BenchCompare.Build(new List<BenchRecord>
        {
            R(500, 10, "gpu"), R(300, 10, "sim"), R(520, 12, "gpu"),
        });
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.NotEqual("", r.Name));
    }

    [Fact]
    public void Pontuacao_zero_e_ignorada()
    {
        var rows = BenchCompare.Build(new List<BenchRecord> { R(0, 10, "disk") });
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData("gpu", "Cena de jogo (GPU)")]
    [InlineData("sim", "Simulação de jogo (CPU)")]
    [InlineData("mem", "Memória e carregamento")]
    [InlineData("disk", "Disco")]
    [InlineData("oc", "Gui_Tech OC (estabilidade)")]
    [InlineData("rt:alta", "Ray tracing (alta)")]
    public void Nomes_amigaveis_dos_testes(string test, string esperado)
    {
        Assert.Equal(esperado, BenchCompare.TestName(test));
    }
}
