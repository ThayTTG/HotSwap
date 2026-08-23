using System.Text.Json.Serialization;

namespace HotSwap;

/// <summary>
/// Resultado de uma tentativa de aplicar um perfil: sucesso/falha e, em caso
/// de falha, uma mensagem explicando o motivo (em vez de só "não deu certo").
/// </summary>
public readonly struct ApplyResult
{
    public bool Success { get; }
    public string Message { get; }

    private ApplyResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static ApplyResult Ok() => new(true, "");
    public static ApplyResult Fail(string message) => new(false, message);
}

/// <summary>Um perfil de exibição: configurações a aplicar em um ou mais monitores.</summary>
public class DisplayProfile
{
    /// <summary>Identificador único e estável do perfil (guid curto).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Nome exibido no menu (ex: "Perfil 1", "1440p 144Hz", "Streaming").</summary>
    public string Name { get; set; } = "Novo Perfil";

    /// <summary>
    /// true para os 2 perfis principais (Perfil 1 / Perfil 2), que aparecem
    /// fixos no topo do menu e são os alvos do duplo-clique alternado.
    /// </summary>
    public bool IsMain { get; set; } = false;

    /// <summary>
    /// true quando o perfil foi criado automaticamente a partir da detecção
    /// das resoluções suportadas pelo monitor ("Perfil padrão").
    /// </summary>
    public bool IsAutoDetected { get; set; } = false;

    /// <summary>Nome do monitor ao qual este perfil pertence (chave do dicionário de MonitorProfiles).</summary>
    public string MonitorDeviceName { get; set; } = "";

    /// <summary>Nome amigável do monitor, só para exibição/organização no JSON.</summary>
    public string MonitorFriendlyName { get; set; } = "";

    public int Width { get; set; }
    public int Height { get; set; }
    public int Hz { get; set; }
    public int PosX { get; set; }
    public int PosY { get; set; }

    /// <summary>
    /// Profundidade de cor (bits por pixel) que o driver reporta para esse
    /// modo específico. Importa porque a mesma resolução+Hz pode só ser
    /// válida em certos bpp — aplicar com o bpp errado faz o Windows recusar
    /// a troca (DISP_CHANGE_BADMODE) mesmo a resolução/Hz sendo corretos.
    /// 0/ausente em perfis salvos antes dessa correção: nesse caso o HotSwap
    /// deixa o Windows usar o bpp atual do desktop, como fazia antes.
    /// </summary>
    public int BitsPerPixel { get; set; } = 32;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Orientation Orientation { get; set; } = Orientation.Landscape;

    [JsonIgnore]
    public string DisplayText => $"{Width}x{Height} @ {Hz}Hz";
}

/// <summary>Perfis agrupados por monitor.</summary>
public class MonitorProfiles
{
    public string DeviceName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public List<DisplayProfile> Profiles { get; set; } = new();
}

/// <summary>Raiz do arquivo perfis.json.</summary>
public class ProfileStore
{
    /// <summary>Versão do formato do arquivo, para permitir migrações futuras.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Perfis principais (até 2), aplicáveis a todos os monitores conectados de uma vez.
    /// Cada "perfil principal" é, na verdade, um conjunto: uma configuração por monitor.</summary>
    public List<MainProfileSet> MainProfiles { get; set; } = new();

    /// <summary>Perfis organizados por monitor (inclui os detectados automaticamente e os extras criados pelo usuário).</summary>
    public List<MonitorProfiles> Monitors { get; set; } = new();

    /// <summary>Índice do perfil principal atualmente ativo (0 ou 1), usado para saber para qual alternar no próximo duplo-clique.</summary>
    public int ActiveMainProfileIndex { get; set; } = 0;
}

/// <summary>
/// Um "perfil principal" pode conter uma configuração diferente para cada monitor
/// (ex: monitor 1 em 1920x1080@144, monitor 2 em 2560x1440@60).
/// </summary>
public class MainProfileSet
{
    public string Name { get; set; } = "Perfil";

    /// <summary>Configuração por monitor: chave = nome do dispositivo (ex: \\.\DISPLAY1).</summary>
    public List<DisplayProfile> MonitorSettings { get; set; } = new();
}
