using System.Text.Json;

namespace HotSwap;

/// <summary>
/// Responsável por carregar/salvar o perfis.json, detectar resoluções suportadas
/// pelos monitores conectados e aplicar um conjunto de perfis ao sistema.
/// </summary>
internal class ProfileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string FilePath { get; }
    public ProfileStore Store { get; private set; } = new();

    public ProfileManager(string filePath)
    {
        FilePath = filePath;
    }

    public void Load()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                Store = JsonSerializer.Deserialize<ProfileStore>(json) ?? new ProfileStore();
            }
            catch
            {
                // Arquivo corrompido/ilegível: começa do zero em vez de travar o app.
                Store = new ProfileStore();
            }
        }
        else
        {
            Store = new ProfileStore();
        }

        EnsureDefaultsAndMainProfiles();
        Save();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(Store, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    /// <summary>
    /// Detecta os monitores ativos e, para cada um, registra os modos suportados
    /// como "Perfis padrão" (sem duplicar os que já existem). Também garante que
    /// existam 2 Perfis Principais (criados a partir do estado atual dos monitores
    /// na primeira execução).
    /// </summary>
    private void EnsureDefaultsAndMainProfiles()
    {
        var deviceNames = DisplayInterop.GetActiveDeviceNames();

        foreach (var deviceName in deviceNames)
        {
            var friendlyName = DisplayInterop.GetFriendlyName(deviceName);
            var monitorEntry = Store.Monitors.FirstOrDefault(m => m.DeviceName == deviceName);
            if (monitorEntry == null)
            {
                monitorEntry = new MonitorProfiles { DeviceName = deviceName, FriendlyName = friendlyName };
                Store.Monitors.Add(monitorEntry);
            }
            else
            {
                monitorEntry.FriendlyName = friendlyName;
            }

            var supportedModes = DisplayInterop.GetSupportedModes(deviceName);
            var current = DisplayInterop.GetCurrentMode(deviceName);

            foreach (var mode in supportedModes)
            {
                bool alreadyExists = monitorEntry.Profiles.Any(p =>
                    p.IsAutoDetected && p.Width == mode.Width && p.Height == mode.Height && p.Hz == mode.Hz);

                if (!alreadyExists)
                {
                    monitorEntry.Profiles.Add(new DisplayProfile
                    {
                        Name = $"{mode.Width}x{mode.Height} @ {mode.Hz}Hz",
                        IsAutoDetected = true,
                        MonitorDeviceName = deviceName,
                        MonitorFriendlyName = friendlyName,
                        Width = mode.Width,
                        Height = mode.Height,
                        Hz = mode.Hz,
                        BitsPerPixel = mode.BitsPerPixel,
                        PosX = current?.PosX ?? 0,
                        PosY = current?.PosY ?? 0,
                        Orientation = current?.Orientation ?? Orientation.Landscape,
                    });
                }
            }
        }

        // Cria os 2 Perfis Principais na primeira execução, capturando o estado
        // atual de todos os monitores como ponto de partida para ambos.
        if (Store.MainProfiles.Count == 0)
        {
            var snapshot = CaptureCurrentStateAsProfileSet("Perfil 1");
            var snapshot2 = CaptureCurrentStateAsProfileSet("Perfil 2");
            Store.MainProfiles.Add(snapshot);
            Store.MainProfiles.Add(snapshot2);
        }
    }

    private MainProfileSet CaptureCurrentStateAsProfileSet(string name)
    {
        var set = new MainProfileSet { Name = name };
        foreach (var deviceName in DisplayInterop.GetActiveDeviceNames())
        {
            var current = DisplayInterop.GetCurrentMode(deviceName);
            if (current == null) continue;

            set.MonitorSettings.Add(new DisplayProfile
            {
                Name = name,
                IsMain = true,
                MonitorDeviceName = deviceName,
                MonitorFriendlyName = DisplayInterop.GetFriendlyName(deviceName),
                Width = current.Width,
                Height = current.Height,
                Hz = current.Hz,
                BitsPerPixel = current.BitsPerPixel,
                PosX = current.PosX,
                PosY = current.PosY,
                Orientation = current.Orientation,
            });
        }
        return set;
    }

    /// <summary>
    /// Verifica se a combinação width/height/Hz de um perfil realmente existe na
    /// lista de modos suportados que o driver do monitor reporta. É comum um
    /// monitor aceitar um Hz alto só em certas resoluções (ex: 75Hz em 800x600,
    /// mas não em 1024x768) — sem essa checagem, o Windows simplesmente recusa
    /// a mudança sem dizer por quê.
    /// </summary>
    private ApplyResult ValidateAgainstSupportedModes(DisplayProfile profile)
    {
        var supported = DisplayInterop.GetSupportedModes(profile.MonitorDeviceName);
        if (supported.Count == 0)
        {
            // Não foi possível enumerar nada para esse monitor — deixa o
            // Windows tentar mesmo assim, sem bloquear por falta de dados.
            return ApplyResult.Ok();
        }

        var match = supported.FirstOrDefault(m => m.Width == profile.Width && m.Height == profile.Height && m.Hz == profile.Hz);
        if (match != null)
        {
            // Perfil já existente pode ter sido salvo antes de guardarmos o bpp
            // (ficaria 0/valor antigo) — corrige silenciosamente para o bpp real
            // que o driver reporta para esse modo, evitando aplicar com um bpp
            // desatualizado/errado.
            if (profile.BitsPerPixel != match.BitsPerPixel)
                profile.BitsPerPixel = match.BitsPerPixel;
            return ApplyResult.Ok();
        }

        var sameResolution = supported
            .Where(m => m.Width == profile.Width && m.Height == profile.Height)
            .Select(m => $"{m.Hz}Hz")
            .ToList();

        string detail = sameResolution.Count > 0
            ? $"Hz disponíveis em {profile.Width}x{profile.Height}: {string.Join(", ", sameResolution)}."
            : $"O monitor '{profile.MonitorFriendlyName}' não reporta suporte a {profile.Width}x{profile.Height} em nenhuma taxa de atualização.";

        return ApplyResult.Fail(
            $"{profile.Width}x{profile.Height} @ {profile.Hz}Hz não é suportado por '{profile.MonitorFriendlyName}'. {detail}");
    }

    /// <summary>Aplica um perfil principal (0 ou 1) a todos os monitores que ele cobre.</summary>
    public ApplyResult ApplyMainProfile(int index)
    {
        if (index < 0 || index >= Store.MainProfiles.Count)
            return ApplyResult.Fail("Perfil principal inválido.");

        var set = Store.MainProfiles[index];
        if (set.MonitorSettings.Count == 0)
            return ApplyResult.Fail($"'{set.Name}' não tem nenhuma configuração de monitor salva.");

        // Valida TODOS os monitores antes de aplicar qualquer um — evita deixar
        // o sistema num estado parcialmente trocado quando um dos monitores
        // teria a mudança recusada.
        var errors = new List<string>();
        foreach (var p in set.MonitorSettings)
        {
            var validation = ValidateAgainstSupportedModes(p);
            if (!validation.Success)
                errors.Add(validation.Message);
        }
        if (errors.Count > 0)
            return ApplyResult.Fail(string.Join(" ", errors));

        var applyErrors = new List<string>();

        if (set.MonitorSettings.Count == 1)
        {
            // Um monitor só: aplica direto, sem o padrão CDS_NORESET + commit
            // separado. Esse padrão em duas etapas, num monitor sem EDID
            // ("Generic PnP Monitor"), foi visto retornando sucesso sem a
            // troca realmente acontecer na tela — a chamada direta é o
            // caminho que replica o que as Configurações de Exibição do
            // próprio Windows fazem, e que se confirmou funcionar.
            var p = set.MonitorSettings[0];
            if (!DisplayInterop.ApplyModeDirect(p.MonitorDeviceName, p.Width, p.Height, p.Hz, p.PosX, p.PosY, p.Orientation, p.BitsPerPixel, out var directDetail))
                applyErrors.Add($"{p.MonitorFriendlyName}: {directDetail}");
        }
        else
        {
            // 2+ monitores: mantém o padrão CDS_NORESET + commit único no
            // final, para não deixar geometrias temporariamente inválidas
            // enquanto cada monitor é configurado um a um.
            foreach (var p in set.MonitorSettings)
            {
                if (!DisplayInterop.ApplyMode(p.MonitorDeviceName, p.Width, p.Height, p.Hz, p.PosX, p.PosY, p.Orientation, p.BitsPerPixel, out var detail))
                    applyErrors.Add($"{p.MonitorFriendlyName}: {detail}");
            }

            if (applyErrors.Count == 0 && !DisplayInterop.CommitPendingChanges(out var commitDetail))
                applyErrors.Add($"commit: {commitDetail}");
        }

        if (applyErrors.Count > 0)
            return ApplyResult.Fail("Falha ao aplicar a mudança — " + string.Join(" | ", applyErrors));

        Store.ActiveMainProfileIndex = index;
        Save();
        return ApplyResult.Ok();
    }

    /// <summary>Alterna entre o Perfil 1 e o Perfil 2 (usado no duplo-clique).</summary>
    public ApplyResult ToggleMainProfile()
    {
        int next = Store.ActiveMainProfileIndex == 0 ? 1 : 0;
        return ApplyMainProfile(next);
    }

    /// <summary>Aplica um único perfil (de um monitor específico) — usado pelo menu de contexto.</summary>
    public ApplyResult ApplySingleProfile(DisplayProfile profile)
    {
        var validation = ValidateAgainstSupportedModes(profile);
        if (!validation.Success)
            return validation;

        // Um monitor só: aplica direto (uma única chamada, sem o padrão
        // CDS_NORESET + commit separado usado para múltiplos monitores). Foi
        // esse o ajuste que corrigiu um caso real em que o padrão de duas
        // etapas retornava sucesso sem a troca realmente acontecer na tela.
        if (!DisplayInterop.ApplyModeDirect(profile.MonitorDeviceName, profile.Width, profile.Height, profile.Hz, profile.PosX, profile.PosY, profile.Orientation, profile.BitsPerPixel, out var detail))
            return ApplyResult.Fail($"Falha ao aplicar a mudança — {detail}");

        return ApplyResult.Ok();
    }

    /// <summary>Re-detecta monitores e modos suportados, sem apagar perfis já existentes.</summary>
    public void RefreshDetection()
    {
        EnsureDefaultsAndMainProfiles();
        Save();
    }
}
