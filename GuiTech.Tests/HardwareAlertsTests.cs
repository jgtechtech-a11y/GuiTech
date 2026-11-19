using System;
using System.Collections.Generic;
using GuiTech;
using Xunit;

namespace GuiTech.Tests;

/// <summary>Avisos automáticos de hardware: limiares, leituras seguidas e intervalo entre avisos.</summary>
public class HardwareAlertsTests
{
    private static readonly DateTime T0 = new(2026, 11, 18, 12, 0, 0);

    public HardwareAlertsTests() => HardwareAlerts.Reset();

    [Fact]
    public void Temperatura_normal_nao_avisa()
    {
        for (int i = 0; i < 10; i++)
            Assert.Empty(HardwareAlerts.Sample(60f, 70f, T0.AddSeconds(i)));
    }

    [Fact]
    public void Avisa_so_depois_de_leituras_seguidas_acima_do_limite()
    {
        Assert.Empty(HardwareAlerts.Sample(95f, 50f, T0));
        Assert.Empty(HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(1)));
        var lista = HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(2));

        var a = Assert.Single(lista);
        Assert.Equal("cpu", a.Kind);
        Assert.Contains("95", a.Text);
    }

    [Fact]
    public void Pico_isolado_nao_avisa()
    {
        Assert.Empty(HardwareAlerts.Sample(95f, 50f, T0));
        Assert.Empty(HardwareAlerts.Sample(60f, 50f, T0.AddSeconds(1)));   // voltou ao normal: zera a contagem
        Assert.Empty(HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(2)));
        Assert.Empty(HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(3)));
    }

    [Fact]
    public void Intervalo_entre_avisos_evita_spam()
    {
        for (int i = 0; i < 3; i++) HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(i));
        Assert.True(HardwareAlerts.HasFired("cpu"));

        // Continua quente: não avisa de novo antes do intervalo.
        for (int i = 0; i < 20; i++)
            Assert.Empty(HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(10 + i)));

        // Depois do intervalo, avisa de novo (o último aviso foi em T0+2 s).
        var depois = HardwareAlerts.Sample(95f, 50f, T0.AddSeconds(2).Add(HardwareAlerts.TempCooldown).AddSeconds(1));
        Assert.Single(depois);
    }

    [Fact]
    public void Gpu_e_cpu_avisam_separadamente()
    {
        for (int i = 0; i < 3; i++) HardwareAlerts.Sample(50f, 89f, T0.AddSeconds(i));
        Assert.False(HardwareAlerts.HasFired("cpu"));
        Assert.True(HardwareAlerts.HasFired("gpu"));
    }

    [Fact]
    public void Dados_ausentes_nao_geram_aviso()
    {
        for (int i = 0; i < 5; i++)
            Assert.Empty(HardwareAlerts.Sample(null, null, T0.AddSeconds(i)));
    }

    [Fact]
    public void Disco_critico_avisa()
    {
        var lista = HardwareAlerts.CheckDisks(
            new[] { new DiskSample("Samsung SSD", DiskVerdict.Critical, 40, 10) }, T0);
        Assert.Single(lista);
        Assert.Equal("disk", lista[0].Kind);
    }

    [Fact]
    public void Disco_quente_avisa()
    {
        var lista = HardwareAlerts.CheckDisks(
            new[] { new DiskSample("HD Externo", DiskVerdict.Good, 65, 5) }, T0);
        Assert.Single(lista);
        Assert.Contains("65", lista[0].Text);
    }

    [Fact]
    public void Disco_saudavel_e_frio_nao_avisa()
    {
        var lista = HardwareAlerts.CheckDisks(
            new List<DiskSample> { new("SSD", DiskVerdict.Good, 35, 20) }, T0);
        Assert.Empty(lista);
    }

    [Fact]
    public void Desgaste_alto_da_bateria_avisa()
    {
        Assert.Single(HardwareAlerts.CheckBattery(35, T0));
        Assert.Empty(HardwareAlerts.CheckBattery(10, T0));
        Assert.Empty(HardwareAlerts.CheckBattery(null, T0));
    }
}
