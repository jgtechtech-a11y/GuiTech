using System.Runtime.InteropServices;

namespace GuiTech;

/// <summary>
/// Controle da GPU pela API oficial de vídeo da AMD (ADL / Overdrive8).
/// Lê os limites e a curva que o driver usa hoje, aplica uma curva própria de 5 pontos
/// e devolve o controle ao driver em "Voltar ao automático". Também lê e aplica o clock
/// máximo da GPU, o clock da memória e o limite de energia (os mesmos recursos que o
/// MSI Afterburner usa em placas AMD).
/// Só funciona em placas AMD cujo driver exponha esses recursos (RDNA em diante). Não instala
/// nada, não usa firmware modificado e não mexe em tensão.
/// </summary>
public static class AmdGpuFan
{
    private const string Dll = "atiadlxx.dll";

    /// <summary>Capacidade das tabelas do Overdrive8. O driver de hoje devolve 77 recursos; 96 dá folga.</summary>
    private const int Max = 96;

    // Índices fixos da tabela do Overdrive8 (ADL_OD8_FAN_CURVE_*).
    private const int CurveTemp1 = 19;
    private const int CurveSpeed5 = 28;

    // Outros índices fixos da mesma tabela: clock máximo da GPU (MHz), clock da memória (MHz)
    // e limite de energia (%). O clock mínimo (índice 1) fica de fora de propósito: só o teto interessa.
    private const int GfxClkMax = 0;
    private const int MemClock = 8;
    private const int PowerLimit = 9;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr MallocCallback(int size);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    private static extern int ADL2_Main_Control_Create(MallocCallback callback, int enumConnectedAdapters, out IntPtr context);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    private static extern int ADL2_Main_Control_Destroy(IntPtr context);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    private static extern int ADL_Adapter_NumberOfAdapters_Get(out int count);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    private static extern int ADL2_Overdrive8_Init_SettingX2_Get(IntPtr context, int adapter, out int caps, ref int count, out IntPtr list);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    private static extern int ADL2_Overdrive8_Current_SettingX2_Get(IntPtr context, int adapter, ref int count, out IntPtr list);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    private static extern int ADL2_Overdrive8_Setting_Set(IntPtr context, int adapter, ref SetSetting settings, ref CurrentSetting current);

    [StructLayout(LayoutKind.Sequential)]
    private struct InitSetting
    {
        public int FeatureId;
        public int Min;
        public int Max;
        public int Default;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SingleSet
    {
        public int Value;
        public int Requested;
        public int Reset;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SetSetting
    {
        public int Count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Max)] public SingleSet[] Table;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CurrentSetting
    {
        public int Count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Max)] public int[] Table;
    }

    // O driver chama este callback para alocar a memória das listas; liberamos com FreeHGlobal.
    private static readonly MallocCallback Allocator = size => Marshal.AllocHGlobal(size);

    private static readonly object Gate = new();
    private static IntPtr _context;
    private static int _adapter = -1;
    private static int _count;
    private static int _caps;
    private static InitSetting[] _init = Array.Empty<InitSetting>();
    private static bool _hasCurve;
    private static bool _hasTuning;
    private static bool _tried;
    private static string _error = "";

    /// <summary>A GPU aceita controle de ventoinha por esta via?</summary>
    public static bool Available { get { lock (Gate) { return EnsureOpenLocked() && _hasCurve; } } }

    /// <summary>A GPU aceita clock/energia por esta via?</summary>
    public static bool TuningAvailable { get { lock (Gate) { return EnsureOpenLocked() && _hasTuning; } } }

    /// <summary>Por que a ventoinha não está disponível (ou vazio quando está).</summary>
    public static string Error
    {
        get
        {
            lock (Gate)
            {
                if (EnsureOpenLocked() && !_hasCurve) return NoCurveMessage;
                return _error;
            }
        }
    }

    /// <summary>Por que os ajustes de clock/energia não estão disponíveis (ou vazio quando estão).</summary>
    public static string TuningError
    {
        get
        {
            lock (Gate)
            {
                if (EnsureOpenLocked() && !_hasTuning) return NoTuningMessage;
                return _error;
            }
        }
    }

    private const string NoCurveMessage =
        "Esta GPU não expõe controle de ventoinha pela API da AMD. " +
        "Placas AMD com driver recente (RDNA em diante) funcionam; nas demais, use a curva do fabricante.";

    private const string NoTuningMessage =
        "Esta GPU não expõe clock e energia pela API da AMD. " +
        "Placas AMD com driver recente (RDNA em diante) funcionam; nas demais, use o software do fabricante.";

    /// <summary>Um recurso do Overdrive8: faixa aceita, padrão de fábrica e valor em uso.</summary>
    public readonly record struct Reading(int Min, int Max, int Default, int Current);

    /// <summary>Clock máximo da GPU (MHz): faixa, padrão e valor atual.</summary>
    public static Reading? Core() => Read(GfxClkMax);

    /// <summary>Clock da memória (MHz): faixa, padrão e valor atual.</summary>
    public static Reading? Memory() => Read(MemClock);

    /// <summary>Limite de energia (%): faixa, padrão e valor atual.</summary>
    public static Reading? Power() => Read(PowerLimit);

    /// <summary>Faixa de temperatura aceita pela placa (°C).</summary>
    public static (int Min, int Max) TempRange
    {
        get
        {
            lock (Gate)
            {
                if (!EnsureOpenLocked()) return (25, 100);
                var (min, max) = (int.MaxValue, int.MinValue);
                for (int i = CurveTemp1; i <= CurveSpeed5; i += 2)
                {
                    min = Math.Min(min, _init[i].Min);
                    max = Math.Max(max, _init[i].Max);
                }
                return min == int.MaxValue ? (25, 100) : (min, max);
            }
        }
    }

    /// <summary>Faixa de velocidade aceita pela placa (%).</summary>
    public static (int Min, int Max) SpeedRange
    {
        get
        {
            lock (Gate)
            {
                if (!EnsureOpenLocked()) return (20, 100);
                var (min, max) = (int.MaxValue, int.MinValue);
                for (int i = CurveTemp1 + 1; i <= CurveSpeed5; i += 2)
                {
                    min = Math.Min(min, _init[i].Min);
                    max = Math.Max(max, _init[i].Max);
                }
                return min == int.MaxValue ? (20, 100) : (min, max);
            }
        }
    }

    /// <summary>Curva que o driver está usando agora (5 pontos de temperatura/velocidade).</summary>
    public static List<(int Temp, int Speed)> Current()
    {
        lock (Gate)
        {
            var list = new List<(int, int)>();
            if (!EnsureOpenLocked()) return list;
            var values = ReadCurrentLocked();
            if (values == null) return list;
            for (int i = CurveTemp1; i <= CurveSpeed5; i += 2)
                list.Add((values[i], values[i + 1]));
            return list;
        }
    }

    /// <summary>Aplica a curva (5 pontos). Lê de volta e confirma que o driver aceitou.</summary>
    public static bool Apply(IReadOnlyList<(int Temp, int Speed)> points, out string error)
    {
        lock (Gate)
        {
            error = "";
            if (!EnsureOpenLocked()) { error = _error; return false; }
            if (points.Count != 5) { error = "A curva precisa de 5 pontos."; return false; }

            var current = ReadCurrentLocked();
            if (current == null) { error = "Não foi possível ler a curva atual da ventoinha."; return false; }

            var set = NewSet();
            int tempMin = _init[CurveTemp1].Min, tempMax = _init[CurveTemp1].Max;
            int speedMin = _init[CurveTemp1 + 1].Min, speedMax = _init[CurveSpeed5].Max;

            // Normaliza: temperatura crescente dentro da faixa da placa, velocidade na faixa aceita.
            int lastTemp = tempMin - 1;
            for (int p = 0; p < 5; p++)
            {
                int temp = Math.Clamp(points[p].Temp, tempMin, tempMax);
                if (temp <= lastTemp) temp = Math.Min(tempMax, lastTemp + 1);
                lastTemp = temp;
                int speed = Math.Clamp(points[p].Speed, speedMin, speedMax);

                set.Table[CurveTemp1 + p * 2].Requested = 1;
                set.Table[CurveTemp1 + p * 2].Value = temp;
                set.Table[CurveTemp1 + p * 2 + 1].Requested = 1;
                set.Table[CurveTemp1 + p * 2 + 1].Value = speed;
            }

            var cur = new CurrentSetting { Count = _count, Table = new int[Max] };
            Array.Copy(current, cur.Table, Math.Min(current.Length, Max));

            int rc = ADL2_Overdrive8_Setting_Set(_context, _adapter, ref set, ref cur);
            if (rc != 0) { error = $"O driver recusou a curva (código {rc})."; return false; }

            // Confere na leitura de volta: garante que a ventoinha realmente está sob nossa curva.
            var check = ReadCurrentLocked();
            if (check == null) { error = "O driver aceitou, mas não devolveu a curva aplicada."; return false; }
            for (int p = 0; p < 5; p++)
            {
                int t = CurveTemp1 + p * 2;
                if (Math.Abs(check[t] - set.Table[t].Value) > 1 || Math.Abs(check[t + 1] - set.Table[t + 1].Value) > 1)
                {
                    error = "O driver não manteve os valores da curva.";
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Devolve a ventoinha ao controle automático do driver (curva de fábrica).</summary>
    public static bool RestoreAuto(out string error)
    {
        lock (Gate)
        {
            error = "";
            if (!EnsureOpenLocked()) { error = _error; return false; }
            var current = ReadCurrentLocked();
            if (current == null) { error = "Não foi possível ler a curva atual."; return false; }

            var set = NewSet();
            for (int i = CurveTemp1; i <= CurveSpeed5; i++)
            {
                set.Table[i].Requested = 1;
                set.Table[i].Reset = 1;
                set.Table[i].Value = _init[i].Default;
            }
            var cur = new CurrentSetting { Count = _count, Table = new int[Max] };
            Array.Copy(current, cur.Table, Math.Min(current.Length, Max));

            int rc = ADL2_Overdrive8_Setting_Set(_context, _adapter, ref set, ref cur);
            if (rc != 0) { error = $"O driver recusou voltar ao automático (código {rc})."; return false; }
            return true;
        }
    }

    /// <summary>
    /// Aplica clock máximo da GPU (MHz), clock da memória (MHz) e limite de energia (%).
    /// Cada valor é preso à faixa que o driver informa; depois a leitura de volta confirma
    /// que a placa aceitou. Nada é gravado no disco: ao reiniciar, o driver volta ao padrão.
    /// </summary>
    public static bool ApplyTuning(int coreClock, int memoryClock, int powerLimit, out string error)
    {
        lock (Gate)
        {
            error = "";
            if (!EnsureOpenLocked()) { error = _error; return false; }
            if (!_hasTuning) { error = NoTuningMessage; return false; }

            var current = ReadCurrentLocked();
            if (current == null) { error = "Não foi possível ler os valores atuais da GPU."; return false; }

            int core = Od8Tuning.Fit(coreClock, _init[GfxClkMax].Min, _init[GfxClkMax].Max);
            int mem = Od8Tuning.Fit(memoryClock, _init[MemClock].Min, _init[MemClock].Max);
            int power = Od8Tuning.Fit(powerLimit, _init[PowerLimit].Min, _init[PowerLimit].Max);

            var set = NewSet();
            set.Table[GfxClkMax].Requested = 1;
            set.Table[GfxClkMax].Value = core;
            set.Table[MemClock].Requested = 1;
            set.Table[MemClock].Value = mem;
            set.Table[PowerLimit].Requested = 1;
            set.Table[PowerLimit].Value = power;

            var cur = new CurrentSetting { Count = _count, Table = new int[Max] };
            Array.Copy(current, cur.Table, Math.Min(current.Length, Max));

            int rc = ADL2_Overdrive8_Setting_Set(_context, _adapter, ref set, ref cur);
            if (rc != 0) { error = $"O driver recusou o ajuste (código {rc})."; return false; }

            // Confere na leitura de volta: garante que a placa está mesmo com os valores pedidos.
            var check = ReadCurrentLocked();
            if (check == null) { error = "O driver aceitou, mas não devolveu os valores aplicados."; return false; }
            var kept = new List<string>();
            if (Math.Abs(check[GfxClkMax] - core) > 1)
                kept.Add($"clock da GPU (ficou em {check[GfxClkMax]} MHz)");
            if (Math.Abs(check[MemClock] - mem) > 1)
                kept.Add($"memória (ficou em {check[MemClock]} MHz)");
            if (Math.Abs(check[PowerLimit] - power) > 1)
                kept.Add($"energia (ficou em {check[PowerLimit]}%)");
            if (kept.Count > 0)
            {
                error = "O driver não manteve " + string.Join(", ", kept) + ".";
                return false;
            }
            return true;
        }
    }

    /// <summary>Devolve clock e limite de energia ao padrão de fábrica do driver.</summary>
    public static bool RestoreTuning(out string error)
    {
        lock (Gate)
        {
            error = "";
            if (!EnsureOpenLocked()) { error = _error; return false; }
            if (!_hasTuning) { error = NoTuningMessage; return false; }

            var current = ReadCurrentLocked();
            if (current == null) { error = "Não foi possível ler os valores atuais da GPU."; return false; }

            var set = NewSet();
            foreach (int i in new[] { GfxClkMax, MemClock, PowerLimit })
            {
                set.Table[i].Requested = 1;
                set.Table[i].Reset = 1;
                set.Table[i].Value = _init[i].Default;
            }
            var cur = new CurrentSetting { Count = _count, Table = new int[Max] };
            Array.Copy(current, cur.Table, Math.Min(current.Length, Max));

            int rc = ADL2_Overdrive8_Setting_Set(_context, _adapter, ref set, ref cur);
            if (rc != 0) { error = $"O driver recusou voltar ao padrão (código {rc})."; return false; }
            return true;
        }
    }

    // ------------------------------------------------------------------ interno

    private static Reading? Read(int index)
    {
        lock (Gate)
        {
            if (!EnsureOpenLocked() || !_hasTuning || _init.Length <= index) return null;
            var values = ReadCurrentLocked();
            if (values == null || values.Length <= index) return null;
            var init = _init[index];
            return new Reading(init.Min, init.Max, init.Default, values[index]);
        }
    }

    private static SetSetting NewSet()
    {
        var set = new SetSetting { Count = _count, Table = new SingleSet[Max] };
        for (int i = 0; i < Max; i++) set.Table[i] = new SingleSet();
        return set;
    }

    private static int[]? ReadCurrentLocked()
    {
        try
        {
            int count = Max;
            IntPtr list;
            int rc = ADL2_Overdrive8_Current_SettingX2_Get(_context, _adapter, ref count, out list);
            if (rc != 0 || list == IntPtr.Zero) return null;
            int take = Math.Min(Math.Min(count, Max), Max);
            var values = new int[take];
            for (int i = 0; i < take; i++) values[i] = Marshal.ReadInt32(list, i * 4);
            Marshal.FreeHGlobal(list);
            return values.Length > PowerLimit ? values : null;
        }
        catch { return null; }
    }

    private static bool EnsureOpenLocked()
    {
        if (_context != IntPtr.Zero) return _adapter >= 0;
        if (_tried) return false;
        _tried = true;

        try
        {
            if (ADL2_Main_Control_Create(Allocator, 1, out IntPtr context) != 0 || context == IntPtr.Zero)
            {
                _error = "A biblioteca de vídeo da AMD (ADL) não respondeu. Instale/atualize o driver da placa.";
                return false;
            }
            _context = context;

            if (ADL_Adapter_NumberOfAdapters_Get(out int adapters) != 0) adapters = 1;

            for (int adapter = 0; adapter < Math.Max(adapters, 1); adapter++)
            {
                int caps = 0, count = Max;
                IntPtr list;
                if (ADL2_Overdrive8_Init_SettingX2_Get(_context, adapter, out caps, ref count, out list) != 0)
                {
                    if (list != IntPtr.Zero) Marshal.FreeHGlobal(list);
                    continue;
                }

                int take = Math.Min(Math.Min(count, Max), Max);
                var init = new InitSetting[Max];
                if (list != IntPtr.Zero)
                {
                    int size = Marshal.SizeOf<InitSetting>();
                    for (int i = 0; i < take; i++)
                        init[i] = Marshal.PtrToStructure<InitSetting>(new IntPtr(list.ToInt64() + i * size));
                    Marshal.FreeHGlobal(list);
                }

                // A curva existe quando o recurso "FAN_CURVE" está marcado e os 10 valores têm faixa válida.
                bool curve = take > CurveSpeed5
                             && (caps & init[CurveTemp1].FeatureId) != 0
                             && init[CurveTemp1].Max > init[CurveTemp1].Min
                             && init[CurveSpeed5].Max > init[CurveSpeed5].Min;

                // Clock/energia existem quando os três recursos têm identificador e faixa válidos.
                bool tuning = take > PowerLimit
                              && init[GfxClkMax].FeatureId != 0 && init[GfxClkMax].Max > init[GfxClkMax].Min
                              && init[MemClock].FeatureId != 0 && init[MemClock].Max > init[MemClock].Min
                              && init[PowerLimit].FeatureId != 0 && init[PowerLimit].Max > init[PowerLimit].Min;
                if (!curve && !tuning) continue;

                int ccount = Max;
                IntPtr clist;
                if (ADL2_Overdrive8_Current_SettingX2_Get(_context, adapter, ref ccount, out clist) != 0)
                {
                    if (clist != IntPtr.Zero) Marshal.FreeHGlobal(clist);
                    continue;
                }
                int ctake = Math.Min(Math.Min(ccount, Max), Max);
                if (clist != IntPtr.Zero) Marshal.FreeHGlobal(clist);
                if (ctake <= (curve ? CurveSpeed5 : PowerLimit)) continue;

                _adapter = adapter;
                _count = ctake;
                _caps = caps;
                _init = init;
                _hasCurve = curve;
                _hasTuning = tuning;
                _error = "";
                return true;
            }

            _error = NoCurveMessage;
            return false;
        }
        catch (DllNotFoundException)
        {
            _error = "A biblioteca de vídeo da AMD (atiadlxx.dll) não foi encontrada. Este recurso é só para placas AMD.";
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            _error = "O driver de vídeo instalado é antigo e não tem a API de curva da ventoinha. Atualize o driver AMD.";
            return false;
        }
        catch (Exception ex)
        {
            _error = "Não foi possível falar com o driver de vídeo: " + ex.Message;
            return false;
        }
    }
}
