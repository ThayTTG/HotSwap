using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace HotSwap;

/// <summary>
/// Controles desenhados com a identidade visual do HotSwap, usados pelos
/// diálogos. Os controles padrão do WinForms (CheckBox, ComboBox, bordas de
/// TextBox) não aceitam bem um tema escuro, então os que aparecem nos
/// diálogos são desenhados aqui.
/// </summary>
internal static class ControlPaintHelpers
{
    /// <summary>Preenche o fundo com a cor do controle pai — para os cantos
    /// "de fora" das formas arredondadas não ficarem com outra cor.</summary>
    public static void ClearWithParent(Control c, Graphics g)
        => g.Clear(c.Parent?.BackColor ?? Theme.Background);

    public static void DrawFocusRing(Graphics g, RectangleF r, float radius, int dpi)
    {
        using var path = Theme.RoundedRect(r, radius);
        using var pen = new Pen(Theme.AccentHover, Math.Max(1f, Theme.Scale(1.5f, dpi)));
        g.DrawPath(pen, path);
    }
}

/// <summary>Botão arredondado: primário (azul) ou secundário (superfície).</summary>
internal sealed class ThemedButton : Button
{
    private bool _hover;
    private bool _down;

    public bool Primary { get; set; }

    public ThemedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Size = new Size(104, 34);
        Font = Theme.UiFontBold;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = DeviceDpi;
        ControlPaintHelpers.ClearWithParent(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill = Primary
            ? (_down ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent)
            : (_down ? Theme.SurfacePressed : _hover ? Theme.SurfaceHover : Theme.Surface);
        if (!Enabled)
            fill = Theme.Surface;

        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = Theme.Scale(6f, dpi);
        using (var path = Theme.RoundedRect(r, radius))
        {
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
            if (!Primary)
            {
                using var p = new Pen(Theme.Border);
                g.DrawPath(p, path);
            }
        }

        if (Focused && ShowFocusCues)
        {
            var inner = RectangleF.Inflate(r, -Theme.Scale(2f, dpi), -Theme.Scale(2f, dpi));
            ControlPaintHelpers.DrawFocusRing(g, inner, radius - 1, dpi);
        }

        Color textColor = !Enabled ? Theme.TextDim : Primary ? Color.White : Theme.Text;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>Switch liga/desliga com texto ao lado (substitui o CheckBox).</summary>
internal sealed class ToggleSwitch : Control
{
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable | ControlStyles.StandardClick, true);
        // Dois cliques rápidos = duas trocas (não um "duplo clique" ignorado).
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Size = new Size(320, 28);
        ForeColor = Theme.Text;
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            Checked = !Checked;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = DeviceDpi;
        ControlPaintHelpers.ClearWithParent(this, g);

        float w = Theme.Scale(36f, dpi);
        float h = Theme.Scale(20f, dpi);
        var sw = new RectangleF(1, (Height - h) / 2f, w, h);
        Theme.DrawSwitch(g, sw, _checked, Enabled);

        if (Focused && ShowFocusCues)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            ControlPaintHelpers.DrawFocusRing(g, RectangleF.Inflate(sw, Theme.Scale(2f, dpi), Theme.Scale(2f, dpi)), h / 2f + 2, dpi);
        }

        var textRect = new Rectangle((int)(sw.Right + Theme.Scale(10f, dpi)), 0,
            Width - (int)(sw.Right + Theme.Scale(10f, dpi)), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Theme.Text : Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// Campo que "grava" um atalho de teclado: clique nele e pressione a
/// combinação; ela aparece como teclas (Ctrl, Alt, R…). Backspace/Delete
/// limpa. Esc e Enter (sem modificadores) continuam fechando/confirmando o
/// diálogo normalmente.
/// </summary>
internal sealed class KeyCapBox : Control
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private string _hotkeyText = "";
    private string? _hint;

    public string HotkeyText
    {
        get => _hotkeyText;
        set
        {
            _hotkeyText = value ?? "";
            _hint = null;
            Invalidate();
        }
    }

    public KeyCapBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Size = new Size(340, 44);
        BackColor = Theme.Surface;
    }

    private static bool WinKeyDown =>
        (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        var mods = keyData & Keys.Modifiers;
        // Tab, Esc e Enter "puros" seguem para o diálogo (navegar, cancelar,
        // salvar); todo o resto vira candidato a atalho.
        if (mods == Keys.None && key is Keys.Tab or Keys.Escape or Keys.Enter)
            return false;
        // Shift+Tab (voltar o foco) e Alt+F4 (fechar) mantêm o significado normal.
        if (keyData == (Keys.Shift | Keys.Tab) || keyData == (Keys.Alt | Keys.F4))
            return false;
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Handled + SuppressKeyPress: a tecla não segue para o Windows — sem
        // isso, Alt+letra gera um "bip" (mnemônico não encontrado) e soltar o
        // Alt ativa o menu de sistema da janela.
        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            _hint = null;
            Invalidate(); // mostra os modificadores sendo segurados, ao vivo
            return;
        }

        if (e.Modifiers == Keys.None && !WinKeyDown && e.KeyCode is Keys.Back or Keys.Delete)
        {
            HotkeyText = "";
            return;
        }

        bool win = WinKeyDown;
        if (!e.Control && !e.Alt && !e.Shift && !win)
        {
            _hint = "Use pelo menos um modificador: Ctrl, Alt, Shift ou Win";
            Invalidate();
            return;
        }

        var parts = new List<string>();
        if (e.Control) parts.Add("Ctrl");
        if (e.Alt) parts.Add("Alt");
        if (e.Shift) parts.Add("Shift");
        if (win) parts.Add("Win");
        parts.Add(e.KeyCode.ToString());
        HotkeyText = string.Join("+", parts);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        e.Handled = true; // idem: evita que soltar o Alt abra o menu de sistema
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { _hint = null; Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = DeviceDpi;
        ControlPaintHelpers.ClearWithParent(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = Theme.Scale(7f, dpi);
        using (var path = Theme.RoundedRect(r, radius))
        {
            using var b = new SolidBrush(Enabled ? Theme.Surface : Theme.Background);
            g.FillPath(b, path);
            using var p = new Pen(Focused ? Theme.Accent : Theme.Border, Focused ? Theme.Scale(1.5f, dpi) : 1f);
            g.DrawPath(p, path);
        }

        float x = Theme.Scale(10f, dpi);

        // Enquanto grava, mostra os modificadores sendo segurados agora.
        string[] parts;
        bool live = false;
        if (Focused && (ModifierKeys != Keys.None || WinKeyDown))
        {
            var held = new List<string>();
            if ((ModifierKeys & Keys.Control) != 0) held.Add("Ctrl");
            if ((ModifierKeys & Keys.Alt) != 0) held.Add("Alt");
            if ((ModifierKeys & Keys.Shift) != 0) held.Add("Shift");
            if (WinKeyDown) held.Add("Win");
            held.Add("…");
            parts = held.ToArray();
            live = true;
        }
        else
        {
            parts = Theme.HotkeyDisplayParts(_hotkeyText);
        }

        if (_hint != null)
        {
            TextRenderer.DrawText(g, _hint, Theme.SmallFont,
                new Rectangle((int)x, 0, Width - (int)x * 2, Height), Theme.Danger,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        if (parts.Length == 0)
        {
            string placeholder = Focused ? "Pressione a combinação de teclas…" : "Clique aqui e pressione a combinação";
            TextRenderer.DrawText(g, placeholder, Font,
                new Rectangle((int)x, 0, Width - (int)x * 2, Height), Enabled ? Theme.TextMuted : Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        float capH = Height - Theme.Scale(14f, dpi);
        float capY = (Height - capH) / 2f;
        float padX = Theme.Scale(9f, dpi);
        var plusSize = TextRenderer.MeasureText(g, "+", Font, Size.Empty, TextFormatFlags.NoPadding);

        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                x += Theme.Scale(5f, dpi);
                TextRenderer.DrawText(g, "+", Font,
                    new Rectangle((int)x, 0, plusSize.Width, Height), Theme.TextDim,
                    TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                x += plusSize.Width + Theme.Scale(5f, dpi);
            }

            bool pending = live && parts[i] == "…";
            var textSize = TextRenderer.MeasureText(g, parts[i], Theme.KeyFont, Size.Empty, TextFormatFlags.NoPadding);
            float capW = Math.Max(textSize.Width + padX * 2, capH);
            if (x + capW > Width - Theme.Scale(6f, dpi)) break; // não cabe: corta (sem estourar o campo)

            var cap = new RectangleF(x, capY, capW, capH);
            using (var capPath = Theme.RoundedRect(cap, Theme.Scale(4f, dpi)))
            {
                using var fill = new SolidBrush(pending ? Theme.Surface : Theme.KeyCap);
                g.FillPath(fill, capPath);
                using var border = new Pen(live ? Theme.Accent : Theme.KeyCapBorder);
                g.DrawPath(border, capPath);
            }
            // "Sombra" inferior da tecla, para lembrar um keycap.
            using (var shadow = new Pen(live ? Theme.AccentPressed : Theme.KeyCapBorder, Theme.Scale(2f, dpi)))
                g.DrawLine(shadow, cap.X + Theme.Scale(3f, dpi), cap.Bottom, cap.Right - Theme.Scale(3f, dpi), cap.Bottom);

            TextRenderer.DrawText(g, parts[i], Theme.KeyFont, Rectangle.Round(cap),
                Enabled ? Theme.Text : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            x += capW;
        }
    }
}

/// <summary>
/// Caixa de seleção (substitui o ComboBox): mostra a opção escolhida e, ao
/// clicar, abre a lista como um menu com o mesmo visual do menu da bandeja.
/// </summary>
internal sealed class ThemedSelect : Control
{
    public sealed record Option(string Value, string Label);

    private readonly List<Option> _options = new();
    private int _selectedIndex = -1;
    private bool _hover;

    public event EventHandler? SelectedIndexChanged;

    public ThemedSelect()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Size = new Size(340, 36);
    }

    public void SetOptions(IEnumerable<Option> options)
    {
        _options.Clear();
        _options.AddRange(options);
        _selectedIndex = -1;
        Invalidate();
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = _options.Count == 0 ? -1 : Math.Clamp(value, 0, _options.Count - 1);
            if (clamped == _selectedIndex) return;
            _selectedIndex = clamped;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Option? SelectedOption => _selectedIndex >= 0 && _selectedIndex < _options.Count ? _options[_selectedIndex] : null;

    public IReadOnlyList<Option> Options => _options;

    protected override void OnClick(EventArgs e)
    {
        Focus();
        ShowDropDown();
        base.OnClick(e);
    }

    protected override bool IsInputKey(Keys keyData)
        => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up:
                if (e.Alt) ShowDropDown(); else SelectedIndex = Math.Max(0, _selectedIndex - 1);
                e.Handled = true;
                break;
            case Keys.Down:
                if (e.Alt) ShowDropDown(); else SelectedIndex = _selectedIndex + 1;
                e.Handled = true;
                break;
            case Keys.Space:
            case Keys.F4:
                ShowDropDown();
                e.Handled = true;
                break;
        }
        base.OnKeyDown(e);
    }

    private void ShowDropDown()
    {
        if (_options.Count == 0) return;

        var menu = new ContextMenuStrip();
        int dpi = DeviceDpi;
        for (int i = 0; i < _options.Count; i++)
        {
            int index = i;
            var item = new ToolStripMenuItem(_options[i].Label)
            {
                Checked = i == _selectedIndex,
                Padding = new Padding(0, Theme.Scale(4, dpi), 0, Theme.Scale(4, dpi)),
                AutoSize = true,
            };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }
        Theme.StyleDropDown(menu);
        menu.ShowImageMargin = false;

        // Descarta o menu temporário depois de fechado — adiado com
        // BeginInvoke para não descartar no meio do próprio evento de clique.
        menu.Closed += (_, _) =>
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(menu.Dispose));
            else
                menu.Dispose();
        };
        menu.Show(this, new Point(0, Height + Theme.Scale(2, dpi)));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = DeviceDpi;
        ControlPaintHelpers.ClearWithParent(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = Theme.RoundedRect(r, Theme.Scale(6f, dpi)))
        {
            using var b = new SolidBrush(_hover ? Theme.SurfaceHover : Theme.Surface);
            g.FillPath(b, path);
            using var p = new Pen(Focused ? Theme.Accent : Theme.Border, Focused ? Theme.Scale(1.5f, dpi) : 1f);
            g.DrawPath(p, path);
        }

        // Seta (chevron) à direita.
        float cx = Width - Theme.Scale(16f, dpi);
        float cy = Height / 2f;
        float s = Theme.Scale(4f, dpi);
        using (var chevron = new Pen(Theme.TextMuted, Theme.Scale(1.5f, dpi)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(chevron, new[] { new PointF(cx - s, cy - s / 2f), new PointF(cx, cy + s / 2f), new PointF(cx + s, cy - s / 2f) });

        var textRect = new Rectangle(Theme.Scale(12, dpi), 0, Width - Theme.Scale(40, dpi), Height);
        TextRenderer.DrawText(g, SelectedOption?.Label ?? "", Font, textRect, Enabled ? Theme.Text : Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>Moldura arredondada para uma TextBox sem borda (campo de texto temático).</summary>
internal sealed class TextField : Control
{
    public TextBox Box { get; }

    public TextField()
    {
        // Box precisa existir ANTES de mudar o Size: o redimensionamento
        // dispara OnLayout na hora, e OnLayout usa o Box.
        Box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
        };
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Controls.Add(Box);

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // Só a TextBox interna recebe foco — a moldura não é uma parada de Tab.
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Size = new Size(340, 36);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        Box.Font = Font;
        base.OnFontChanged(e);
        PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Box is null) return; // defensivo: layout durante a construção
        int padX = Theme.Scale(12, DeviceDpi);
        int h = Box.PreferredHeight;
        Box.SetBounds(padX, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - padX * 2), h);
    }

    protected override void OnClick(EventArgs e)
    {
        Box.Focus();
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = DeviceDpi;
        ControlPaintHelpers.ClearWithParent(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(r, Theme.Scale(6f, dpi));
        using var b = new SolidBrush(Theme.Surface);
        g.FillPath(b, path);
        bool focused = Box.Focused;
        using var p = new Pen(focused ? Theme.Accent : Theme.Border, focused ? Theme.Scale(1.5f, dpi) : 1f);
        g.DrawPath(p, path);
    }
}

/// <summary>Faixa de cabeçalho dos diálogos: ícone, título e subtítulo sobre um degradê azul.</summary>
internal sealed class DialogHeader : Control
{
    private readonly Glyph _glyph;
    private readonly string _subtitle;

    public DialogHeader(string title, string subtitle, Glyph glyph)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // Só decorativo: não pode "roubar" o foco inicial do diálogo.
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Text = title;
        _subtitle = subtitle;
        _glyph = glyph;
        Height = 72;
        Margin = Padding.Empty;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = DeviceDpi;
        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using (var bg = new LinearGradientBrush(bounds, Theme.HeaderStart, Theme.Background, 10f))
            g.FillRectangle(bg, bounds);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int tile = Theme.Scale(40, dpi);
        int left = Theme.Scale(20, dpi);
        var tileRect = new Rectangle(left, (Height - tile) / 2, tile, tile);
        using (var tilePath = Theme.RoundedRect(tileRect, Theme.Scale(9f, dpi)))
        using (var tileBrush = new LinearGradientBrush(tileRect, Theme.AccentHover, Theme.AccentPressed, 45f))
            g.FillPath(tileBrush, tilePath);

        int glyphSize = Theme.Scale(20, dpi);
        g.DrawImage(Glyphs.Get(_glyph, glyphSize, Color.White),
            tileRect.X + (tile - glyphSize) / 2, tileRect.Y + (tile - glyphSize) / 2, glyphSize, glyphSize);

        int textX = tileRect.Right + Theme.Scale(14, dpi);
        int textW = Width - textX - Theme.Scale(16, dpi);
        var titleSize = TextRenderer.MeasureText(g, Text, Theme.TitleFont, new Size(textW, int.MaxValue), TextFormatFlags.NoPadding);
        var subSize = TextRenderer.MeasureText(g, _subtitle, Theme.SmallFont, new Size(textW, int.MaxValue), TextFormatFlags.NoPadding);
        int gap = Theme.Scale(3, dpi);
        int top = (Height - titleSize.Height - gap - subSize.Height) / 2;

        TextRenderer.DrawText(g, Text, Theme.TitleFont, new Rectangle(textX, top, textW, titleSize.Height), Color.White,
            TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, _subtitle, Theme.SmallFont, new Rectangle(textX, top + titleSize.Height + gap, textW, subSize.Height),
            Color.FromArgb(0x9F, 0xB6, 0xDE), TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}
