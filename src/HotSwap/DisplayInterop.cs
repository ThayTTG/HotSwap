using System.Runtime.InteropServices;
using System.Text;

namespace HotSwap;

/// <summary>
/// Camada fina sobre as APIs nativas do Windows (user32.dll) usadas para
/// enumerar monitores, ler os modos de vídeo suportados e aplicar uma
/// nova resolução/taxa de atualização.
/// </summary>
internal static class DisplayInterop
{
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DISP_CHANGE_RESTART = 1;
    private const int DISP_CHANGE_FAILED = -1;
    private const int DISP_CHANGE_BADMODE = -2;
    private const int DISP_CHANGE_NOTUPDATED = -3;
    private const int DISP_CHANGE_BADFLAGS = -4;
    private const int DISP_CHANGE_BADPARAM = -5;
    private const int DISP_CHANGE_BADDUALVIEW = -6;
    private const int CDS_UPDATEREGISTRY = 0x01;
    private const int CDS_SET_PRIMARY = 0x10;
    private const int CDS_NORESET = 0x10000000;
    private const int CDS_RESET = 0x40000000;
    private const int ENUM_REGISTRY_SETTINGS = -2;

    /// <summary>Traduz o código numérico de retorno de ChangeDisplaySettingsEx para texto legível.</summary>
    private static string DescribeChangeResult(int code) => code switch
    {
        DISP_CHANGE_SUCCESSFUL => "sucesso",
        DISP_CHANGE_RESTART => "é necessário reiniciar o Windows para aplicar",
        DISP_CHANGE_FAILED => "o driver de vídeo recusou a mudança (DISP_CHANGE_FAILED)",
        DISP_CHANGE_BADMODE => "esse modo não é suportado pelo monitor/adaptador de vídeo (DISP_CHANGE_BADMODE)",
        DISP_CHANGE_NOTUPDATED => "não foi possível gravar as configurações no registro (DISP_CHANGE_NOTUPDATED)",
        DISP_CHANGE_BADFLAGS => "combinação de flags inválida (DISP_CHANGE_BADFLAGS) — provável bug no HotSwap",
        DISP_CHANGE_BADPARAM => "parâmetro inválido (DISP_CHANGE_BADPARAM) — provável bug no HotSwap",
        DISP_CHANGE_BADDUALVIEW => "conflito com configuração de múltiplos monitores (DISP_CHANGE_BADDUALVIEW)",
        _ => $"código de erro desconhecido ({code})",
    };

    // CharSet.Unicode AQUI é essencial e precisa bater com o CharSet declarado
    // nos [DllImport] que recebem "ref DEVMODE" (EnumDisplaySettings,
    // EnumDisplaySettingsEx, ChangeDisplaySettingsEx). O CharSet do DllImport
    // NÃO se propaga sozinho para o StructLayout da struct usada como
    // parâmetro — sem declarar aqui também, o struct usa o default do C#
    // (Ansi), enquanto a chamada da função pode resolver para a variante
    // Unicode (W) do Windows. Isso desalinha os campos ByValTStr (tamanho em
    // bytes diferente por char) e corrompe TODOS os campos numéricos que vêm
    // depois deles no struct — foi exatamente esse bug que fazia
    // EnumDisplayDevices devolver DeviceName='\' vazio/corrompido.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;

        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    // Campos válidos em dmFields
    private const int DM_POSITION = 0x00000020;
    private const int DM_DISPLAYORIENTATION = 0x00000080;
    private const int DM_BITSPERPEL = 0x00040000;
    private const int DM_PELSWIDTH = 0x00080000;
    private const int DM_PELSHEIGHT = 0x00100000;
    private const int DM_DISPLAYFREQUENCY = 0x00400000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    // Ver o comentário em DEVMODE acima: precisa bater com o CharSet do
    // DllImport de EnumDisplayDevices — sem isso, os campos vêm vazios/
    // corrompidos (era a causa do "DeviceName='\'" no diagnóstico).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags]
    private enum DisplayDeviceStateFlags : uint
    {
        AttachedToDesktop = 0x1,
        PrimaryDevice = 0x4,
    }

    // CharSet.Unicode em todos os P/Invoke desta classe (Windows moderno resolve
    // para Unicode/W): sem isso, algumas dessas chamadas usariam a variante
    // ANSI por padrão enquanto outras usavam Unicode, o que deixa
    // dmDeviceName/dmFormName (e nomes de dispositivo/monitor) inconsistentes
    // entre chamadas diferentes.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsEx(string deviceName, int modeNum, ref DEVMODE devMode, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

    /// <summary>Nomes dos dispositivos de monitor ativos, ex: "\\.\DISPLAY1".</summary>
    public static List<string> GetActiveDeviceNames()
    {
        var names = new List<string>();
        var dd = new DISPLAY_DEVICE();
        dd.cb = Marshal.SizeOf(dd);
        uint i = 0;
        while (EnumDisplayDevices(null, i, ref dd, 0))
        {
            if (dd.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop))
                names.Add(dd.DeviceName);
            i++;
            dd.cb = Marshal.SizeOf(dd);
        }

        // Fallback: se nenhum adaptador reportou AttachedToDesktop (o que não
        // deveria acontecer com um monitor físico realmente ligado, mas já foi
        // visto acontecer de forma transitória com certos drivers), tenta de
        // novo sem o filtro — qualquer adaptador enumerado que aceite
        // EnumDisplaySettings(ENUM_CURRENT_SETTINGS) com sucesso é, na prática,
        // um monitor utilizável, mesmo que a flag não tenha vindo setada.
        if (names.Count == 0)
        {
            i = 0;
            dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(dd);
            while (EnumDisplayDevices(null, i, ref dd, 0))
            {
                var probe = new DEVMODE();
                probe.dmSize = (short)Marshal.SizeOf(probe);
                if (EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref probe))
                    names.Add(dd.DeviceName);
                i++;
                dd.cb = Marshal.SizeOf(dd);
            }
        }

        return names;
    }

    /// <summary>
    /// Diagnóstico: lista todos os adaptadores/monitores que EnumDisplayDevices
    /// enxerga, com seus StateFlags brutos — usado para investigar por que
    /// GetActiveDeviceNames() não está encontrando nenhum monitor.
    /// </summary>
    public static List<string> DebugListAllDevices()
    {
        var lines = new List<string>();
        var dd = new DISPLAY_DEVICE();
        dd.cb = Marshal.SizeOf(dd);
        uint i = 0;
        while (EnumDisplayDevices(null, i, ref dd, 0))
        {
            bool hasCurrentMode = false;
            var probe = new DEVMODE();
            probe.dmSize = (short)Marshal.SizeOf(probe);
            hasCurrentMode = EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref probe);

            lines.Add($"[{i}] DeviceName='{dd.DeviceName}' DeviceString='{dd.DeviceString}' " +
                      $"StateFlags=0x{(int)dd.StateFlags:X} AttachedToDesktop={dd.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop)} " +
                      $"EnumDisplaySettingsOk={hasCurrentMode}" +
                      (hasCurrentMode ? $" ({probe.dmPelsWidth}x{probe.dmPelsHeight}@{probe.dmDisplayFrequency}Hz)" : ""));
            i++;
            dd.cb = Marshal.SizeOf(dd);
        }
        if (lines.Count == 0)
            lines.Add("EnumDisplayDevices não retornou NENHUM dispositivo (nem no índice 0).");
        return lines;
    }

    /// <summary>Descrição amigável do monitor (nome do adaptador/monitor), para exibir no menu.</summary>
    public static string GetFriendlyName(string deviceName)
    {
        var dd = new DISPLAY_DEVICE();
        dd.cb = Marshal.SizeOf(dd);
        // Primeiro pega o adaptador
        uint i = 0;
        var adapter = new DISPLAY_DEVICE();
        adapter.cb = Marshal.SizeOf(adapter);
        while (EnumDisplayDevices(null, i, ref adapter, 0))
        {
            if (adapter.DeviceName == deviceName)
            {
                var monitor = new DISPLAY_DEVICE();
                monitor.cb = Marshal.SizeOf(monitor);
                if (EnumDisplayDevices(deviceName, 0, ref monitor, 0) && !string.IsNullOrWhiteSpace(monitor.DeviceString))
                    return monitor.DeviceString;
                return adapter.DeviceString;
            }
            i++;
            adapter.cb = Marshal.SizeOf(adapter);
        }
        return deviceName;
    }

    /// <summary>Modo de vídeo atualmente em uso por um monitor.</summary>
    public static DisplayModeInfo? GetCurrentMode(string deviceName)
    {
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(dm);
        if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm))
            return null;

        return new DisplayModeInfo(
            dm.dmPelsWidth,
            dm.dmPelsHeight,
            dm.dmDisplayFrequency,
            dm.dmPositionX,
            dm.dmPositionY,
            OrientationFromDmValue(dm.dmDisplayOrientation),
            dm.dmBitsPerPel);
    }

    /// <summary>Todos os modos de vídeo únicos (resolução + Hz) suportados por um monitor.</summary>
    public static List<DisplayModeInfo> GetSupportedModes(string deviceName)
    {
        // Mesma resolução+Hz pode aparecer várias vezes na enumeração, cada
        // vez com uma profundidade de cor (dmBitsPerPel) diferente — por
        // exemplo, 1024x768@75Hz pode existir tanto em 16 quanto em 32 bits.
        // dmBitsPerPel importa: se aplicarmos width/height/Hz corretos mas
        // com um bpp que não é válido para esse modo específico (ex: herdado
        // à toa do modo atualmente em uso), o Windows recusa a chamada
        // inteira com DISP_CHANGE_BADMODE — mesmo a resolução/Hz sendo, cada
        // um isoladamente, perfeitamente válidos. Por isso guardamos, para
        // cada combinação width/height/Hz, o MAIOR bpp anunciado pelo driver
        // (o que a UI do Windows também usa como padrão).
        var bestBpp = new Dictionary<(int, int, int), int>();
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(dm);

        int modeIndex = 0;
        while (EnumDisplaySettings(deviceName, modeIndex, ref dm))
        {
            // Ignora modos absurdamente pequenos (modos legados de compatibilidade)
            if (dm.dmPelsWidth >= 640 && dm.dmPelsHeight >= 480 && dm.dmDisplayFrequency > 0)
            {
                var key = (dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency);
                if (!bestBpp.TryGetValue(key, out var currentBest) || dm.dmBitsPerPel > currentBest)
                    bestBpp[key] = dm.dmBitsPerPel;
            }
            modeIndex++;
            dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(dm) };
        }

        var results = bestBpp
            .Select(kv => new DisplayModeInfo(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, 0, 0, Orientation.Landscape, kv.Value))
            .ToList();

        results.Sort((a, b) =>
        {
            int cmp = (b.Width * b.Height).CompareTo(a.Width * a.Height);
            if (cmp != 0) return cmp;
            return b.Hz.CompareTo(a.Hz);
        });

        return results;
    }

    /// <summary>
    /// Prepara (grava no registro, sem aplicar ainda) a resolução, Hz, posição e
    /// orientação de um monitor específico usando CDS_NORESET. Quando estiver
    /// configurando vários monitores em sequência, chame isso para cada um e,
    /// no final, chame <see cref="CommitPendingChanges"/> uma única vez para
    /// aplicar tudo de forma atômica.
    /// </summary>
    /// <returns>
    /// true se o Windows aceitou os parâmetros para esse monitor; em caso de
    /// falha, <paramref name="errorDetail"/> traz o motivo reportado pela API.
    /// </returns>
    public static bool ApplyMode(string deviceName, int width, int height, int hz, int posX, int posY, Orientation orientation, int? bitsPerPixel, out string errorDetail)
    {
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(dm);

        if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm))
        {
            errorDetail = "não foi possível ler as configurações atuais do monitor (EnumDisplaySettings falhou)";
            return false;
        }

        // Só marca DM_DISPLAYORIENTATION como campo alterado se a orientação
        // pedida for de fato diferente da atual. Muitos monitores/drivers
        // (principalmente monitores fixos sem suporte a rotação) recusam a
        // chamada inteira quando esse flag vem setado, mesmo pedindo a mesma
        // orientação que já está em uso — então evitamos tocar nele à toa.
        bool orientationChanged = dm.dmDisplayOrientation != OrientationToDmValue(orientation);

        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;
        dm.dmDisplayFrequency = hz;
        dm.dmPositionX = posX;
        dm.dmPositionY = posY;
        dm.dmDisplayOrientation = OrientationToDmValue(orientation);
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_POSITION;
        if (orientationChanged)
            dm.dmFields |= DM_DISPLAYORIENTATION;

        // dmBitsPerPel NÃO vem em dm.dmFields por padrão — o valor herdado do
        // "modo atual" (lido acima via EnumDisplaySettings) pode não ser válido
        // para o modo alvo. Alguns monitores só anunciam certas combinações de
        // resolução+Hz em uma profundidade de cor específica (ex: 16 bits em
        // vez dos 32 bits do desktop atual); se o bpp não bater exatamente com
        // o que o driver espera para esse modo, o Windows recusa a chamada
        // inteira com DISP_CHANGE_BADMODE mesmo a resolução/Hz sendo válidos
        // isoladamente. Quando sabemos o bpp correto (vindo de GetSupportedModes),
        // fixamos ele explicitamente em vez de confiar no valor herdado.
        if (bitsPerPixel is int bpp && bpp > 0)
        {
            dm.dmBitsPerPel = bpp;
            dm.dmFields |= DM_BITSPERPEL;
        }

        // CDS_NORESET: grava a configuração mas NÃO aplica ainda. A aplicação
        // de fato acontece em CommitPendingChanges(), chamada uma única vez
        // depois que todos os monitores foram preparados — isso evita
        // geometrias temporariamente inválidas quando há 2+ monitores.
        int result = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);
        errorDetail = result == DISP_CHANGE_SUCCESSFUL ? "" : DescribeChangeResult(result);
        return result == DISP_CHANGE_SUCCESSFUL;
    }

    /// <summary>
    /// Aplica resolução/Hz/posição/orientação a UM monitor em uma única
    /// chamada direta (sem o padrão CDS_NORESET + commit em duas etapas).
    /// Use isso ao mexer em apenas um monitor por vez — é o caminho mais
    /// próximo do que as Configurações de Exibição do próprio Windows fazem,
    /// e evitou um caso real em que o padrão de duas etapas retornava
    /// sucesso (DISP_CHANGE_SUCCESSFUL) sem a mudança realmente chegar a
    /// aparecer na tela física (visto em um monitor sem EDID/"Generic PnP
    /// Monitor", onde CDS_NORESET + commit separado não disparava a
    /// renegociação de sinal de verdade).
    /// </summary>
    public static bool ApplyModeDirect(string deviceName, int width, int height, int hz, int posX, int posY, Orientation orientation, int? bitsPerPixel, out string errorDetail)
    {
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(dm);

        if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm))
        {
            errorDetail = "não foi possível ler as configurações atuais do monitor (EnumDisplaySettings falhou)";
            return false;
        }

        bool orientationChanged = dm.dmDisplayOrientation != OrientationToDmValue(orientation);

        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;
        dm.dmDisplayFrequency = hz;
        dm.dmPositionX = posX;
        dm.dmPositionY = posY;
        dm.dmDisplayOrientation = OrientationToDmValue(orientation);
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_POSITION;
        if (orientationChanged)
            dm.dmFields |= DM_DISPLAYORIENTATION;

        if (bitsPerPixel is int bpp && bpp > 0)
        {
            dm.dmBitsPerPel = bpp;
            dm.dmFields |= DM_BITSPERPEL;
        }

        // Sem CDS_NORESET: testa E aplica na mesma chamada, contra o
        // dispositivo específico (não null). CDS_UPDATEREGISTRY persiste a
        // escolha para a próxima vez que o Windows iniciar nesse modo.
        int result = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        errorDetail = result == DISP_CHANGE_SUCCESSFUL ? "" : DescribeChangeResult(result);
        return result == DISP_CHANGE_SUCCESSFUL;
    }

    /// <summary>
    /// Força o Windows a re-aplicar/consolidar mudanças pendentes de todos os monitores
    /// (a chamada "final" depois de uma ou mais ApplyMode com CDS_NORESET).
    /// </summary>
    /// <returns>true se a consolidação foi bem-sucedida; caso contrário, <paramref name="errorDetail"/> traz o motivo.</returns>
    public static bool CommitPendingChanges(out string errorDetail)
    {
        // IMPORTANTE: precisa ser um DEVMODE válido com dmSize preenchido — um
        // DEVMODE zerado (dmSize=0) faz o ChangeDisplaySettingsEx rejeitar a
        // chamada, o que impede QUALQUER mudança preparada anteriormente de
        // ser efetivada (esse era o bug: tudo era "preparado" com sucesso mas
        // nunca de fato aplicado, porque o commit final sempre falhava).
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(dm);

        int result = ChangeDisplaySettingsEx(null!, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
        errorDetail = result == DISP_CHANGE_SUCCESSFUL ? "" : DescribeChangeResult(result);
        return result == DISP_CHANGE_SUCCESSFUL;
    }

    private static Orientation OrientationFromDmValue(int value) => value switch
    {
        0 => Orientation.Landscape,
        1 => Orientation.Portrait,
        2 => Orientation.LandscapeFlipped,
        3 => Orientation.PortraitFlipped,
        _ => Orientation.Landscape,
    };

    private static int OrientationToDmValue(Orientation o) => o switch
    {
        Orientation.Landscape => 0,
        Orientation.Portrait => 1,
        Orientation.LandscapeFlipped => 2,
        Orientation.PortraitFlipped => 3,
        _ => 0,
    };
}

public enum Orientation
{
    Landscape,
    Portrait,
    LandscapeFlipped,
    PortraitFlipped,
}

/// <summary>Representa um modo de vídeo: resolução, taxa de atualização, posição, orientação e profundidade de cor.</summary>
public record DisplayModeInfo(int Width, int Height, int Hz, int PosX, int PosY, Orientation Orientation, int BitsPerPixel = 32)
{
    public override string ToString() => $"{Width}x{Height} @ {Hz}Hz";
}
