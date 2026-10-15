using System.Runtime.InteropServices;
using System.Text;

namespace HotSwap;

/// <summary>
/// Troca o CONTEÚDO (as janelas) entre dois monitores, sem mexer na
/// resolução/Hz nem na posição física de cada monitor — só reposiciona as
/// janelas para que o que estava na Tela A passe a aparecer na Tela B e
/// vice-versa, mantendo a posição relativa de cada janela dentro da tela
/// nova (ex: uma janela encostada no canto superior esquerdo da Tela A
/// aparece encostada no canto superior esquerdo da Tela B).
/// </summary>
internal static class WindowSwapper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    private const uint GW_OWNER = 4;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    private sealed record MonitorInfo(IntPtr Handle, string DeviceName, RECT Bounds);

    /// <summary>Um monitor ativo, para exibir no menu de configuração de telas a trocar.</summary>
    public sealed record MonitorSummary(string DeviceName, string FriendlyName, int Width, int Height);

    /// <summary>Lista os monitores ativos (via EnumDisplayMonitors), na mesma ordem que o Windows os reporta.</summary>
    public static List<MonitorSummary> GetActiveMonitors()
    {
        var monitors = EnumerateMonitors();
        return monitors
            .Select(m => new MonitorSummary(m.DeviceName, DisplayInterop_GetFriendlyNameSafe(m.DeviceName), m.Bounds.Width, m.Bounds.Height))
            .ToList();
    }

    // DisplayInterop.GetFriendlyName é internal na mesma assembly, então dá
    // para chamar direto — este wrapper só existe para deixar claro, no
    // ponto de uso acima, que estamos cruzando dados de duas APIs distintas
    // (EnumDisplayMonitors aqui vs EnumDisplayDevices em DisplayInterop).
    private static string DisplayInterop_GetFriendlyNameSafe(string deviceName)
    {
        try { return DisplayInterop.GetFriendlyName(deviceName); }
        catch { return deviceName; }
    }

    private static List<MonitorInfo> EnumerateMonitors()
    {
        var result = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT rect, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref mi))
                result.Add(new MonitorInfo(hMonitor, mi.szDevice, mi.rcMonitor));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// Troca todas as janelas visíveis da Tela A com todas as da Tela B,
    /// preservando a posição relativa de cada janela dentro do monitor de
    /// origem e seu estado (normal/maximizada/minimizada). Os monitores em
    /// si NÃO mudam de lugar — só o conteúdo (as janelas) troca de tela.
    /// </summary>
    /// <param name="deviceNameA">DeviceName (ex: "\\.\DISPLAY1") do primeiro monitor. Vazio = usa o primeiro monitor ativo detectado.</param>
    /// <param name="deviceNameB">DeviceName do segundo monitor. Vazio = usa o segundo monitor ativo detectado.</param>
    public static SwapResult SwapMonitorContents(string deviceNameA, string deviceNameB)
    {
        var monitors = EnumerateMonitors();
        if (monitors.Count < 2)
            return SwapResult.Fail("é preciso ter pelo menos 2 monitores ativos para trocar o conteúdo entre eles.");

        var monitorA = string.IsNullOrEmpty(deviceNameA)
            ? monitors[0]
            : monitors.FirstOrDefault(m => m.DeviceName == deviceNameA);
        var monitorB = string.IsNullOrEmpty(deviceNameB)
            ? monitors.FirstOrDefault(m => m.Handle != monitorA?.Handle)
            : monitors.FirstOrDefault(m => m.DeviceName == deviceNameB);

        if (monitorA == null || monitorB == null)
            return SwapResult.Fail("um dos monitores configurados para a troca não está mais conectado. Reconfigure pelo menu \"Trocar telas...\".");

        if (monitorA.Handle == monitorB.Handle)
            return SwapResult.Fail("os dois monitores configurados para a troca são o mesmo — escolha dois monitores diferentes pelo menu \"Trocar telas...\".");

        var windows = EnumerateSwappableWindows();

        // Separa por monitor ANTES de mover qualquer coisa — se fôssemos
        // decidir "de qual monitor essa janela veio" enquanto já movemos
        // outras, correríamos o risco de reclassificar uma janela que acabou
        // de ser movida para o monitor errado.
        var onA = new List<IntPtr>();
        var onB = new List<IntPtr>();
        foreach (var hWnd in windows)
        {
            var monitorHandle = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            if (monitorHandle == monitorA.Handle)
                onA.Add(hWnd);
            else if (monitorHandle == monitorB.Handle)
                onB.Add(hWnd);
            // Janelas em outros monitores (num setup com 3+ telas) ficam onde estão.
        }

        if (onA.Count == 0 && onB.Count == 0)
            return SwapResult.Fail("nenhuma janela visível foi encontrada nos dois monitores selecionados — nada para trocar.");

        int moved = 0;
        foreach (var hWnd in onA)
            if (MoveWindowToMonitor(hWnd, monitorA.Bounds, monitorB.Bounds))
                moved++;
        foreach (var hWnd in onB)
            if (MoveWindowToMonitor(hWnd, monitorB.Bounds, monitorA.Bounds))
                moved++;

        return SwapResult.Ok(moved, onA.Count + onB.Count);
    }

    /// <summary>
    /// Enumera janelas de topo "reais" (visíveis, com título, sem dono, que
    /// não são janelas de ferramenta) — o mesmo tipo de filtro usado por
    /// utilitários de troca de janelas (Alt+Tab) para ignorar janelas
    /// auxiliares/invisíveis que tecnicamente existem mas não são "conteúdo"
    /// do usuário.
    /// </summary>
    private static List<IntPtr> EnumerateSwappableWindows()
    {
        var result = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            // Janelas com "dono" (GW_OWNER != 0) são normalmente diálogos,
            // tooltips ou popups de outra janela — não fazem sentido mover
            // independentemente da janela principal delas.
            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
                return true;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            int titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
                return true; // sem título — quase sempre uma janela auxiliar/invisível do sistema

            result.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// Move uma janela do monitor de origem para o monitor de destino,
    /// preservando sua posição relativa (proporcional) e tamanho dentro da
    /// área de trabalho, e seu estado (normal/maximizada/minimizada).
    /// </summary>
    private static bool MoveWindowToMonitor(IntPtr hWnd, RECT fromBounds, RECT toBounds)
    {
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hWnd, ref placement))
            return false;

        bool wasMaximized = placement.showCmd == SW_SHOWMAXIMIZED || IsZoomed(hWnd);
        bool wasMinimized = placement.showCmd == SW_SHOWMINIMIZED || IsIconic(hWnd);

        // rcNormalPosition é a geometria da janela em estado "restaurado"
        // (nem maximizada, nem minimizada) — é isso que precisamos
        // reposicionar proporcionalmente, mesmo que a janela esteja
        // maximizada/minimizada agora, porque é para lá que ela volta ao
        // ser restaurada.
        var normal = placement.rcNormalPosition;

        // Posição relativa (0.0–1.0) dentro do monitor de origem, calculada
        // a partir do canto superior esquerdo da janela.
        double relX = (normal.Left - fromBounds.Left) / (double)Math.Max(1, fromBounds.Width);
        double relY = (normal.Top - fromBounds.Top) / (double)Math.Max(1, fromBounds.Height);
        double relW = normal.Width / (double)Math.Max(1, fromBounds.Width);
        double relH = normal.Height / (double)Math.Max(1, fromBounds.Height);

        // Limita a janela a caber no monitor de destino, para o caso de o
        // monitor de destino ser menor que o de origem (evita janelas
        // "vazando" para fora da tela nova).
        relW = Math.Min(relW, 1.0);
        relH = Math.Min(relH, 1.0);
        relX = Math.Min(Math.Max(relX, 0.0), 1.0 - relW);
        relY = Math.Min(Math.Max(relY, 0.0), 1.0 - relH);

        int newWidth = (int)Math.Round(relW * toBounds.Width);
        int newHeight = (int)Math.Round(relH * toBounds.Height);
        int newLeft = toBounds.Left + (int)Math.Round(relX * toBounds.Width);
        int newTop = toBounds.Top + (int)Math.Round(relY * toBounds.Height);

        placement.rcNormalPosition = new RECT
        {
            Left = newLeft,
            Top = newTop,
            Right = newLeft + Math.Max(newWidth, 1),
            Bottom = newTop + Math.Max(newHeight, 1),
        };

        // Mantém o mesmo estado (maximizada continua maximizada, minimizada
        // continua minimizada) — só no monitor novo. SetWindowPlacement por
        // si só já maximiza/minimiza a janela no monitor certo quando
        // rcNormalPosition aponta para lá, então basta preservar showCmd.
        placement.showCmd = wasMaximized ? SW_SHOWMAXIMIZED
            : wasMinimized ? SW_SHOWMINIMIZED
            : SW_SHOWNORMAL;

        bool ok = SetWindowPlacement(hWnd, ref placement);

        // Para janelas maximizadas, SetWindowPlacement já reposiciona
        // corretamente no monitor de destino (o Windows maximiza "no
        // monitor onde a janela está", e movemos rcNormalPosition para lá
        // antes de maximizar). Um SetWindowPos adicional sem
        // SWP_NOMOVE/SWP_NOSIZE garante que janelas em estado normal também
        // fiquem exatamente na geometria calculada, sem alterar o
        // z-order nem roubar o foco.
        if (ok && !wasMaximized && !wasMinimized)
        {
            SetWindowPos(hWnd, IntPtr.Zero, newLeft, newTop, Math.Max(newWidth, 1), Math.Max(newHeight, 1),
                SWP_NOZORDER | SWP_NOACTIVATE);
        }

        return ok;
    }
}

/// <summary>Resultado de uma tentativa de trocar o conteúdo de duas telas.</summary>
public readonly struct SwapResult
{
    public bool Success { get; }
    public string Message { get; }
    public int WindowsMoved { get; }
    public int WindowsTotal { get; }

    private SwapResult(bool success, string message, int moved, int total)
    {
        Success = success;
        Message = message;
        WindowsMoved = moved;
        WindowsTotal = total;
    }

    public static SwapResult Ok(int moved, int total) => new(true, "", moved, total);
    public static SwapResult Fail(string message) => new(false, message, 0, 0);
}
