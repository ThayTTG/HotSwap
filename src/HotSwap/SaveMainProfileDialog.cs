namespace HotSwap;

/// <summary>
/// Diálogo simples para escolher em qual dos 2 slots principais (Perfil 1 / Perfil 2)
/// salvar a configuração atual dos monitores, e com que nome.
/// </summary>
internal class SaveMainProfileDialog : Form
{
    private readonly ComboBox _slotCombo;
    private readonly TextBox _nameBox;

    public int TargetIndex { get; private set; }
    public string ChosenName => _nameBox.Text.Trim();

    public SaveMainProfileDialog(List<string> existingNames)
    {
        Text = "Salvar perfil principal";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(340, 150);

        var slotLabel = new Label { Text = "Salvar como:", Location = new Point(12, 15), AutoSize = true };

        // _nameBox precisa existir ANTES de registrarmos o handler de
        // SelectedIndexChanged e ANTES de setar SelectedIndex logo abaixo,
        // porque o ComboBox dispara o evento imediatamente ao atribuir
        // SelectedIndex — mesmo aqui dentro do construtor.
        var nameLabel = new Label { Text = "Nome:", Location = new Point(12, 70), AutoSize = true };
        _nameBox = new TextBox
        {
            Location = new Point(12, 90),
            Width = 300,
            Text = existingNames.Count > 0 ? existingNames[0] : "Perfil 1",
        };

        _slotCombo = new ComboBox
        {
            Location = new Point(12, 35),
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _slotCombo.Items.Add(existingNames.Count > 0 ? $"Perfil 1 ({existingNames[0]})" : "Perfil 1");
        _slotCombo.Items.Add(existingNames.Count > 1 ? $"Perfil 2 ({existingNames[1]})" : "Perfil 2");
        _slotCombo.SelectedIndexChanged += (_, _) =>
        {
            _nameBox.Text = _slotCombo.SelectedIndex < existingNames.Count
                ? existingNames[_slotCombo.SelectedIndex]
                : $"Perfil {_slotCombo.SelectedIndex + 1}";
        };
        _slotCombo.SelectedIndex = 0;

        var okButton = new Button { Text = "Salvar", DialogResult = DialogResult.OK, Location = new Point(150, 120), Width = 80 };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(236, 120), Width = 80 };

        okButton.Click += (_, _) => TargetIndex = _slotCombo.SelectedIndex;

        Controls.Add(slotLabel);
        Controls.Add(_slotCombo);
        Controls.Add(nameLabel);
        Controls.Add(_nameBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
