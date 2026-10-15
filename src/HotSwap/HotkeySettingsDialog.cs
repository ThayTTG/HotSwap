namespace HotSwap;

/// <summary>
/// Diálogo para ativar/desativar e configurar o atalho de teclado global que
/// alterna entre Perfil 1 e Perfil 2.
/// </summary>
internal class HotkeySettingsDialog : ThemedDialog
{
    private readonly ToggleSwitch _enabledToggle;
    private readonly KeyCapBox _hotkeyBox;

    public bool HotkeyEnabled => _enabledToggle.Checked;
    public string HotkeyText => _hotkeyBox.HotkeyText;

    public HotkeySettingsDialog(bool currentEnabled, string currentHotkey)
        : base("Atalho de perfis", "Alterna entre Perfil 1 e Perfil 2 de qualquer lugar do Windows", Glyph.Keyboard)
    {
        _enabledToggle = AddField(new ToggleSwitch { Text = "Ativar atalho de teclado global", Checked = currentEnabled }, 2);

        AddLabel("Combinação", 16);
        _hotkeyBox = AddField(new KeyCapBox { HotkeyText = currentHotkey, Enabled = currentEnabled });
        AddHint("Clique no campo e pressione as teclas juntas (ex: Ctrl + Alt + R). Precisa de pelo menos um modificador — Ctrl, Alt, Shift ou Win. Backspace limpa.");

        _enabledToggle.CheckedChanged += (_, _) => _hotkeyBox.Enabled = _enabledToggle.Checked;

        FinishLayout();
    }
}
