namespace HotSwap;

/// <summary>
/// Base dos diálogos do HotSwap: cabeçalho com degradê azul, corpo em
/// coluna única e barra de botões (Salvar/Cancelar) no rodapé, tudo no tema
/// escuro. As classes filhas só adicionam o conteúdo com os helpers
/// <see cref="AddLabel"/>, <see cref="AddHint"/> e <see cref="AddField"/> e
/// chamam <see cref="FinishLayout"/> no fim do construtor.
///
/// Medidas são escritas em pixels "lógicos" (96 DPI) e o WinForms reescala
/// tudo sozinho (AutoScaleMode.Dpi) — o layout é por painéis de tamanho
/// automático, então nada fica cortado em 125%/150%/200%.
/// </summary>
internal class ThemedDialog : Form
{
    private readonly TableLayoutPanel _body;

    protected ThemedButton OkButton { get; }
    protected ThemedButton CancelButtonControl { get; }

    protected ThemedDialog(string title, string subtitle, Glyph glyph, string okText = "Salvar")
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = title;
        Font = Theme.UiFont;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        Icon = Theme.AppIcon;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = Point.Empty,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var header = new DialogHeader(title, subtitle, glyph)
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(440, 72),
        };

        _body = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 14, 22, 10),
            Margin = Padding.Empty,
            BackColor = Theme.Background,
        };
        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var separator = new Panel
        {
            Height = 1,
            Dock = DockStyle.Fill,
            BackColor = Theme.Separator,
            Margin = new Padding(0, 6, 0, 0),
        };

        // Fluxo da esquerda para a direita, painel encostado à direita:
        // [Salvar] [Cancelar], e o Tab passa por Salvar primeiro.
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(16, 14, 16, 16),
            Margin = Padding.Empty,
            BackColor = Theme.Background,
        };

        OkButton = new ThemedButton { Text = okText, Primary = true, DialogResult = DialogResult.OK, Margin = new Padding(0, 0, 8, 0) };
        CancelButtonControl = new ThemedButton { Text = "Cancelar", DialogResult = DialogResult.Cancel, Margin = Padding.Empty };
        buttons.Controls.Add(OkButton);
        buttons.Controls.Add(CancelButtonControl);

        root.Controls.Add(header);
        root.Controls.Add(_body);
        root.Controls.Add(separator);
        root.Controls.Add(buttons);
        Controls.Add(root);

        AcceptButton = OkButton;
        CancelButton = CancelButtonControl;
    }

    /// <summary>Título de um campo (texto normal, com respiro acima).</summary>
    protected Label AddLabel(string text, int topMargin = 10)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Theme.Text,
            BackColor = Theme.Background,
            Font = Theme.UiFontBold,
            Margin = new Padding(0, topMargin, 0, 6),
            Anchor = AnchorStyles.Left,
        };
        _body.Controls.Add(label);
        return label;
    }

    /// <summary>Texto de ajuda pequeno e apagado, com quebra de linha automática.</summary>
    protected Label AddHint(string text, int topMargin = 6)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(396, 0),
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Background,
            Font = Theme.SmallFont,
            Margin = new Padding(0, topMargin, 0, 0),
            Anchor = AnchorStyles.Left,
        };
        _body.Controls.Add(label);
        return label;
    }

    /// <summary>Adiciona um controle ocupando a largura toda do corpo.</summary>
    protected T AddField<T>(T control, int topMargin = 0) where T : Control
    {
        control.Margin = new Padding(0, topMargin, 0, 0);
        control.Dock = DockStyle.Fill;
        _body.Controls.Add(control);
        return control;
    }

    /// <summary>Chamar no fim do construtor da classe filha, depois de
    /// adicionar todo o conteúdo (aplica o escalonamento por DPI).</summary>
    protected void FinishLayout()
    {
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkTitleBar(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Aberto a partir da bandeja, o diálogo às vezes nasce atrás da
        // janela em foco — traz para frente.
        Activate();
    }
}
