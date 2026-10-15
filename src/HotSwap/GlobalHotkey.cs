using System.Runtime.InteropServices;

namespace HotSwap;

/// <summary>
/// Registra atalhos de teclado globais (funcionam mesmo com o HotSwap em
/// segundo plano, sem foco em nenhuma janela) usando RegisterHotKey do
/// Windows. Para receber a mensagem WM_HOTKEY, o Windows exige um HWND que
/// bombeie mensagens na mesma thread que registrou o atalho — em vez de criar
/// uma janela visível só para isso, usamos uma janela "apenas mensagens"
/// (HWND_MESSAGE), que nunca aparece na tela nem na barra de tarefas.
///
/// Suporta múltiplos atalhos simultâneos (ex: um para alternar Perfil 1/2,
/// outro para trocar o conteúdo entre duas telas) — cada um é identificado
/// por um <see cref="HotkeySlot"/> próprio, todos compartilhando a mesma
/// janela de mensagens.
/// </summary>
internal sealed class GlobalHotkey : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    /// <summary>Identifica qual atalho foi pressionado/registrado — cada slot
    /// usa um ID do Windows diferente (arbitrário, só precisa ser único
    /// dentro deste processo).</summary>
    public enum HotkeySlot
    {
        ToggleMainProfile = 0xA1B2,
        SwapMonitorContents = 0xA1B3,
    }

    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x1,
        Control = 0x2,
        Shift = 0x4,
        Win = 0x8,
        // MOD_NOREPEAT: evita reenviar o evento em repetição enquanto a tecla
        // fica pressionada — sem isso, segurar o atalho alternaria o perfil
        // várias vezes por segundo.
        NoRepeat = 0x4000,
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Disparado na thread da UI sempre que um atalho registrado é pressionado, com o slot correspondente.</summary>
    public event Action<HotkeySlot>? Pressed;

    private readonly HashSet<HotkeySlot> _registeredSlots = new();

    public GlobalHotkey()
    {
        var cp = new CreateParams
        {
            Parent = new IntPtr(-3), // HWND_MESSAGE: janela "apenas mensagens", nunca visível
        };
        CreateHandle(cp);
    }

    /// <summary>
    /// Tenta registrar (ou re-registrar, se já havia um atalho ativo nesse
    /// slot) a combinação de teclas. Retorna false se o Windows recusar —
    /// geralmente porque outro programa já usa exatamente essa combinação.
    /// </summary>
    public bool TryRegister(HotkeySlot slot, Modifiers modifiers, Keys key, out string errorDetail)
    {
        Unregister(slot);

        bool ok = RegisterHotKey(Handle, (int)slot, (uint)(modifiers | Modifiers.NoRepeat), (uint)key);
        if (ok)
            _registeredSlots.Add(slot);

        errorDetail = ok
            ? ""
            : $"o Windows recusou registrar essa combinação (código {Marshal.GetLastWin32Error()}) — provavelmente já está em uso por outro programa";
        return ok;
    }

    public void Unregister(HotkeySlot slot)
    {
        if (_registeredSlots.Remove(slot))
            UnregisterHotKey(Handle, (int)slot);
    }

    public void UnregisterAll()
    {
        foreach (var slot in _registeredSlots.ToList())
            Unregister(slot);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && Enum.IsDefined(typeof(HotkeySlot), m.WParam.ToInt32()))
            Pressed?.Invoke((HotkeySlot)m.WParam.ToInt32());

        base.WndProc(ref m);
    }

    /// <summary>
    /// Converte texto tipo "Ctrl+Alt+R" nos modificadores + tecla que
    /// RegisterHotKey espera. Exige pelo menos um modificador — um atalho sem
    /// modificador (ex: só "R") tomaria conta dessa tecla em qualquer app.
    /// </summary>
    public static bool TryParse(string text, out Modifiers modifiers, out Keys key)
    {
        modifiers = Modifiers.None;
        key = Keys.None;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false; // precisa de ao menos 1 modificador + 1 tecla

        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= Modifiers.Control;
                    break;
                case "alt":
                    modifiers |= Modifiers.Alt;
                    break;
                case "shift":
                    modifiers |= Modifiers.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= Modifiers.Win;
                    break;
                default:
                    return false; // token desconhecido — string malformada
            }
        }

        return modifiers != Modifiers.None
            && Enum.TryParse<Keys>(parts[^1], true, out key)
            && key != Keys.None;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}
