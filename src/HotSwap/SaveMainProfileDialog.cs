namespace HotSwap;

/// <summary>
/// Diálogo para escolher em qual dos 2 slots principais (Perfil 1 / Perfil 2)
/// salvar a configuração atual dos monitores, e com que nome.
/// </summary>
internal class SaveMainProfileDialog : ThemedDialog
{
    private readonly ThemedSelect _slotSelect;
    private readonly TextField _nameField;

    public int TargetIndex { get; private set; }

    /// <summary>Nome digitado; se ficar em branco, usa "Perfil N" do slot escolhido.</summary>
    public string ChosenName
    {
        get
        {
            var name = _nameField.Box.Text.Trim();
            return name.Length > 0 ? name : $"Perfil {TargetIndex + 1}";
        }
    }

    public SaveMainProfileDialog(List<string> existingNames)
        : base("Salvar perfil principal", "Guarda a resolução e o Hz atuais de todas as telas", Glyph.Save)
    {
        AddLabel("Salvar no slot", 4);

        // _nameField precisa existir ANTES de ligarmos o SelectedIndexChanged
        // e de escolhermos o índice inicial: o evento dispara imediatamente
        // ao definir SelectedIndex, ainda aqui dentro do construtor.
        _nameField = new TextField();
        _nameField.Box.Text = existingNames.Count > 0 ? existingNames[0] : "Perfil 1";

        _slotSelect = AddField(new ThemedSelect());
        _slotSelect.SetOptions(new[]
        {
            new ThemedSelect.Option("0", existingNames.Count > 0 ? $"Perfil 1  ·  {existingNames[0]}" : "Perfil 1"),
            new ThemedSelect.Option("1", existingNames.Count > 1 ? $"Perfil 2  ·  {existingNames[1]}" : "Perfil 2"),
        });
        _slotSelect.SelectedIndexChanged += (_, _) =>
        {
            _nameField.Box.Text = _slotSelect.SelectedIndex < existingNames.Count
                ? existingNames[_slotSelect.SelectedIndex]
                : $"Perfil {_slotSelect.SelectedIndex + 1}";
        };
        _slotSelect.SelectedIndex = 0;

        AddLabel("Nome", 14);
        AddField(_nameField);
        AddHint("O nome aparece no menu da bandeja. A configuração salva é a que está na tela agora.");

        OkButton.Click += (_, _) => TargetIndex = Math.Max(0, _slotSelect.SelectedIndex);

        FinishLayout();
    }
}
