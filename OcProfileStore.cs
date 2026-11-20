namespace GuiTech;

/// <summary>
/// Regras da lista de perfis de overclock (achar, salvar por nome, nome livre e texto de
/// resumo). Ficam separadas da janela para poderem ser testadas sem abrir a interface.
/// </summary>
public static class OcProfileStore
{
    /// <summary>Posição do perfil com esse nome (sem diferenciar maiúsculas/espaços nas pontas), ou -1.</summary>
    public static int IndexOf(IReadOnlyList<OcProfile> list, string name)
    {
        string key = name.Trim();
        if (key.Length == 0) return -1;
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i].Name.Trim(), key, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>Perfil com esse nome, ou null quando o nome é vazio ou não existe.</summary>
    public static OcProfile? Find(IReadOnlyList<OcProfile> list, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        int at = IndexOf(list, name);
        return at < 0 ? null : list[at];
    }

    /// <summary>Salva: substitui o perfil de mesmo nome (mantendo a posição) ou acrescenta no fim.</summary>
    public static void Save(List<OcProfile> list, OcProfile profile)
    {
        int at = IndexOf(list, profile.Name);
        if (at >= 0) list[at] = profile;
        else list.Add(profile);
    }

    /// <summary>Nome livre a partir de uma base: "Perfil", "Perfil 2", "Perfil 3"…</summary>
    public static string UniqueName(IReadOnlyList<OcProfile> list, string wanted)
    {
        string baseName = wanted.Trim();
        if (baseName.Length == 0) baseName = "Perfil";
        if (IndexOf(list, baseName) < 0) return baseName;
        for (int n = 2; n < 10000; n++)
        {
            string candidate = $"{baseName} {n}";
            if (IndexOf(list, candidate) < 0) return candidate;
        }
        return baseName;
    }

    /// <summary>Resumo curto do perfil: "GPU 1820 MHz · VRAM 1828 MHz · energia +16 %".</summary>
    public static string Describe(OcProfile profile)
        => $"GPU {profile.Core} MHz · VRAM {profile.Mem} MHz · energia " +
           Od8Tuning.Describe(profile.Power, "%", signed: true);

    /// <summary>O perfil corresponde aos três valores informados (para não reaplicar à toa)?</summary>
    public static bool Matches(OcProfile profile, int core, int memory, int power)
        => profile.Core == core && profile.Mem == memory && profile.Power == power;
}
