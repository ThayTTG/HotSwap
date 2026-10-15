using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace HotSwap;

/// <summary>
/// Renderer do menu da bandeja (e dos submenus): fundo azul-marinho, itens
/// com destaque arredondado ao passar o mouse, barra de cor marcando o perfil
/// ativo, switches no lugar de "check" e textos secundários (resolução,
/// atalhos) em tom mais apagado à direita.
/// </summary>
internal sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    public static readonly MenuRenderer Instance = new();

    private MenuRenderer() : base(new MenuColorTable())
    {
        RoundedEdges = false;
    }

    private static int Dpi(ToolStripItem item) => item.Owner?.DeviceDpi ?? 96;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(Theme.Background);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // No Windows 11 a borda (já arredondada) é desenhada pelo DWM — ver
        // Theme.ApplyRoundedPopup. Nas versões anteriores, borda simples.
        if (Theme.IsWindows11) return;
        using var p = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Sem a faixa cinza clássica da margem de ícones: mesmo fundo do menu.
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var g = e.Graphics;
        int dpi = Dpi(item);
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(
            Theme.Scale(4f, dpi), Theme.Scale(1f, dpi),
            item.Width - Theme.Scale(8f, dpi), item.Height - Theme.Scale(2f, dpi));
        float radius = Theme.Scale(5f, dpi);

        bool isToggle = item is ToggleMenuItem;
        bool isActiveProfile = item is ToolStripMenuItem { Checked: true } && !isToggle;
        bool hot = item.Enabled && (item.Selected || item.Pressed);

        if (hot || isActiveProfile)
        {
            Color fill = hot
                ? (item.Pressed ? Theme.SurfacePressed : Theme.SurfaceHover)
                : Color.FromArgb(0x13, 0x22, 0x40); // destaque leve e permanente do item ativo
            using var path = Theme.RoundedRect(rect, radius);
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
        }

        if (isActiveProfile)
        {
            var accent = item is ProfileMenuItem pmi ? pmi.AccentColor : Theme.Accent;
            float barW = Theme.Scale(3f, dpi);
            float inset = Math.Max(Theme.Scale(6f, dpi), rect.Height * 0.25f);
            var bar = new RectangleF(rect.X, rect.Y + inset, barW, rect.Height - inset * 2);
            using var path = Theme.RoundedRect(bar, barW / 2f);
            using var b = new SolidBrush(accent);
            g.FillPath(b, path);
        }

        if (item is ToggleMenuItem toggle)
        {
            float w = Theme.Scale(30f, dpi);
            float h = Theme.Scale(16f, dpi);
            var sw = new RectangleF(
                item.Width - Theme.Scale(16f, dpi) - w,
                (item.Height - h) / 2f, w, h);
            Theme.DrawSwitch(g, sw, toggle.Checked, item.Enabled);
        }

        g.SmoothingMode = oldMode;
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // O "marcado" é mostrado pela barra de cor (perfil ativo) ou pelo
        // switch (opções liga/desliga) — ver OnRenderMenuItemBackground.
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        // O renderer base pinta um quadrado de "marcado" atrás do ícone de
        // itens com Checked = true (era o quadrado azul atrás do "1" e do
        // sino). Aqui só desenhamos o ícone.
        if (e.Image is null)
            return;
        if (!e.Item.Enabled)
        {
            base.OnRenderItemImage(e); // versão acinzentada padrão
            return;
        }
        var oldMode = e.Graphics.InterpolationMode;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(e.Image, e.ImageRectangle);
        e.Graphics.InterpolationMode = oldMode;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var item = e.Item;
        bool isShortcutText = item is ToolStripMenuItem mi
            && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString)
            && e.Text == mi.ShortcutKeyDisplayString;

        if (isShortcutText && item is ToggleMenuItem)
            return; // espaço reservado para o switch, não é texto de verdade

        Color color;
        if (!item.Enabled)
            color = Theme.TextDim;
        else if (isShortcutText)
            color = Theme.TextSecondary;
        else if (item.Tag is "danger" && item.Selected)
            color = Theme.Danger;
        else
            color = Theme.Text;

        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, color, e.TextFormat);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? Theme.TextDim : Theme.TextMuted;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int dpi = Dpi(e.Item);
        int inset = Theme.Scale(12, dpi);
        int y = e.Item.Height / 2;
        using var p = new Pen(Theme.Separator);
        e.Graphics.DrawLine(p, inset, y, e.Item.Width - inset, y);
    }

    /// <summary>Cores de fallback para qualquer parte que o renderer base
    /// ainda desenhe sozinho.</summary>
    private sealed class MenuColorTable : ProfessionalColorTable
    {
        public MenuColorTable() { UseSystemColors = false; }

        public override Color ToolStripDropDownBackground => Theme.Background;
        public override Color ImageMarginGradientBegin => Theme.Background;
        public override Color ImageMarginGradientMiddle => Theme.Background;
        public override Color ImageMarginGradientEnd => Theme.Background;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => Theme.SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => Theme.SurfaceHover;
        public override Color MenuItemPressedGradientBegin => Theme.SurfacePressed;
        public override Color MenuItemPressedGradientMiddle => Theme.SurfacePressed;
        public override Color MenuItemPressedGradientEnd => Theme.SurfacePressed;
        public override Color SeparatorDark => Theme.Separator;
        public override Color SeparatorLight => Theme.Separator;
        public override Color CheckBackground => Theme.SurfaceHover;
        public override Color CheckSelectedBackground => Theme.SurfacePressed;
        public override Color CheckPressedBackground => Theme.SurfacePressed;
    }
}

/// <summary>Item de perfil principal: guarda a cor do perfil para a barra de "ativo".</summary>
internal sealed class ProfileMenuItem : ToolStripMenuItem
{
    public Color AccentColor { get; set; } = Theme.Accent;

    public ProfileMenuItem(string text) : base(text) { }
}

/// <summary>Opção liga/desliga, desenhada como um switch à direita do item.</summary>
internal sealed class ToggleMenuItem : ToolStripMenuItem
{
    public ToggleMenuItem(string text) : base(text)
    {
        CheckOnClick = true;
        // Reserva espaço à direita para o switch (o renderer não desenha
        // esse "texto" — ver MenuRenderer.OnRenderItemText).
        ShortcutKeyDisplayString = "   ";
    }
}

/// <summary>
/// Base dos itens "decorativos" do menu (cabeçalho, rótulo de seção,
/// separador). Herdam de ToolStripMenuItem — e não de ToolStripItem/
/// ToolStripSeparator — porque o menu suspenso só dá a LARGURA TODA para
/// itens de menu; os outros tipos são empurrados para a direita da coluna de
/// ícones (era o cabeçalho e as seções deslocados na v1.1.0). Desabilitados
/// para não serem clicáveis; a pintura é toda feita aqui.
/// </summary>
internal abstract class MenuDecorItem : ToolStripMenuItem
{
    protected MenuDecorItem()
    {
        Enabled = false;
        AutoSize = true;
    }

    protected int CurrentDpi => Owner?.DeviceDpi ?? 96;

    /// <summary>Texto nítido (ClearType, igual ao resto do menu). Os flags
    /// Preserve* fazem o TextRenderer respeitar o deslocamento do item.</summary>
    protected static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags extra = TextFormatFlags.Default)
    {
        TextRenderer.DrawText(g, text, font, bounds, color,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding | TextFormatFlags.Left |
            TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping | extra);
    }

    protected static int TextHeight(Graphics g, Font font)
        => TextRenderer.MeasureText(g, "Hg", font, Size.Empty, TextFormatFlags.NoPadding).Height;
}

/// <summary>Cabeçalho do menu: logo, nome do app e o perfil ativo.</summary>
internal sealed class MenuHeaderItem : MenuDecorItem
{
    private readonly string _title;
    private readonly string _subtitle;

    public MenuHeaderItem(string title, string subtitle)
    {
        _title = title;
        _subtitle = subtitle;
    }

    public override Size GetPreferredSize(Size constrainingSize)
        => new(Theme.Scale(296, CurrentDpi), Theme.Scale(62, CurrentDpi));

    protected override void OnPaint(PaintEventArgs e)
    {
        int dpi = CurrentDpi;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var card = new RectangleF(
            Theme.Scale(4f, dpi), 0,
            Width - Theme.Scale(8f, dpi), Height - Theme.Scale(6f, dpi));
        if (card.Width <= 0 || card.Height <= 0) return;

        using (var path = Theme.RoundedRect(card, Theme.Scale(6f, dpi)))
        using (var brush = new LinearGradientBrush(card, Theme.HeaderStart, Theme.HeaderEnd, 15f))
            g.FillPath(brush, path);

        int logoSize = Theme.Scale(32, dpi);
        var logoRect = new Rectangle(
            (int)(card.X + Theme.Scale(12f, dpi)),
            (int)(card.Y + (card.Height - logoSize) / 2f),
            logoSize, logoSize);
        g.DrawImage(Theme.AppLogo(logoSize), logoRect);

        int textX = logoRect.Right + Theme.Scale(10, dpi);
        int textW = (int)card.Right - textX - Theme.Scale(10, dpi);
        int titleH = TextHeight(g, Theme.MenuTitleFont);
        int subH = TextHeight(g, Theme.SmallFont);
        int gap = Theme.Scale(2, dpi);
        int top = (int)(card.Y + (card.Height - titleH - gap - subH) / 2f);

        DrawText(g, _title, Theme.MenuTitleFont, new Rectangle(textX, top, textW, titleH), Color.White);
        DrawText(g, _subtitle, Theme.SmallFont, new Rectangle(textX, top + titleH + gap, textW, subH), Theme.HeaderSubtitle);
    }
}

/// <summary>Rótulo de seção (ex: "PERFIS PRINCIPAIS"), pequeno e discreto.</summary>
internal sealed class MenuSectionItem : MenuDecorItem
{
    private readonly string _label;

    public MenuSectionItem(string label)
    {
        _label = label.ToUpperInvariant();
    }

    public override Size GetPreferredSize(Size constrainingSize)
        => new(Theme.Scale(140, CurrentDpi), Theme.Scale(26, CurrentDpi));

    protected override void OnPaint(PaintEventArgs e)
    {
        int dpi = CurrentDpi;
        var g = e.Graphics;
        int h = TextHeight(g, Theme.SectionFont);
        int x = Theme.Scale(14, dpi);
        DrawText(g, _label, Theme.SectionFont,
            new Rectangle(x, Height - h - Theme.Scale(5, dpi), Width - x * 2, h), Theme.SectionText);
    }
}

/// <summary>Linha divisória fina, com recuo dos dois lados.</summary>
internal sealed class MenuSeparatorItem : MenuDecorItem
{
    public override Size GetPreferredSize(Size constrainingSize)
        => new(Theme.Scale(40, CurrentDpi), Theme.Scale(9, CurrentDpi));

    protected override void OnPaint(PaintEventArgs e)
    {
        int dpi = CurrentDpi;
        int inset = Theme.Scale(12, dpi);
        int y = Height / 2;
        using var p = new Pen(Theme.Separator);
        e.Graphics.DrawLine(p, inset, y, Width - inset, y);
    }
}
