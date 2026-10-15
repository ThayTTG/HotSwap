namespace HotSwap;

/// <summary>
/// Diálogo para ativar/desativar e configurar o atalho de teclado global que
/// troca o conteúdo (as janelas) entre duas telas, e escolher quais duas
/// telas participam da troca quando há 3 ou mais monitores conectados.
/// </summary>
internal class SwapScreensSettingsDialog : ThemedDialog
{
    private readonly ToggleSwitch _enabledToggle;
    private readonly KeyCapBox _hotkeyBox;
    private readonly ThemedSelect _monitorASelect;
    private readonly ThemedSelect _monitorBSelect;

    public bool HotkeyEnabled => _enabledToggle.Checked;
    public string HotkeyText => _hotkeyBox.HotkeyText;

    /// <summary>DeviceName do monitor escolhido, ou "" para "automático" (primeiro/segundo monitor ativo).</summary>
    public string MonitorA => _monitorASelect.SelectedOption?.Value ?? "";
    public string MonitorB => _monitorBSelect.SelectedOption?.Value ?? "";

    public SwapScreensSettingsDialog(bool currentEnabled, string currentHotkey, List<WindowSwapper.MonitorSummary> monitors, string currentMonitorA, string currentMonitorB)
        : base("Trocar telas", "Troca todas as janelas de uma tela com todas as da outra", Glyph.Swap)
    {
        _enabledToggle = AddField(new ToggleSwitch { Text = "Ativar atalho para trocar telas", Checked = currentEnabled }, 2);

        AddLabel("Combinação", 16);
        _hotkeyBox = AddField(new KeyCapBox { HotkeyText = currentHotkey, Enabled = currentEnabled });
        AddHint("Os monitores não mudam de lugar — só o conteúdo troca de tela. Cada janela mantém a posição e o estado (normal, maximizada, minimizada).");

        AddLabel("Tela A", 18);
        _monitorASelect = AddField(new ThemedSelect());
        _monitorASelect.SetOptions(BuildOptions(monitors, "(automático — primeiro monitor detectado)"));
        SelectMonitor(_monitorASelect, currentMonitorA);

        AddLabel("Tela B", 12);
        _monitorBSelect = AddField(new ThemedSelect());
        _monitorBSelect.SetOptions(BuildOptions(monitors, "(automático — segundo monitor detectado)"));
        SelectMonitor(_monitorBSelect, currentMonitorB);

        AddHint(monitors.Count > 2
            ? "Você tem mais de 2 monitores — escolha quais dois participam da troca."
            : "Com 2 monitores, pode deixar as duas em \"automático\".");

        _enabledToggle.CheckedChanged += (_, _) => _hotkeyBox.Enabled = _enabledToggle.Checked;

        FinishLayout();
    }

    private static IEnumerable<ThemedSelect.Option> BuildOptions(List<WindowSwapper.MonitorSummary> monitors, string automaticLabel)
    {
        yield return new ThemedSelect.Option("", automaticLabel);
        foreach (var m in monitors)
            yield return new ThemedSelect.Option(m.DeviceName, $"{m.FriendlyName}  ·  {m.Width}×{m.Height}");
    }

    /// <summary>Seleciona o monitor salvo; se ele não estiver mais conectado
    /// (ou nunca foi escolhido), cai em "automático".</summary>
    private static void SelectMonitor(ThemedSelect select, string deviceName)
    {
        int index = 0;
        for (int i = 0; i < select.Options.Count; i++)
        {
            if (select.Options[i].Value == deviceName)
            {
                index = i;
                break;
            }
        }
        select.SelectedIndex = index;
    }
}
