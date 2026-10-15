using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HotSwap;

/// <summary>
/// Identidade visual do HotSwap: paleta (tons de azul sobre um fundo
/// azul-marinho escuro), fontes, helpers de DPI e os ajustes nativos do
/// Windows (barra de título escura, cantos arredondados no Windows 11) usados
/// pelo menu da bandeja e pelos diálogos.
/// </summary>
internal static class Theme
{
    // --- Paleta -----------------------------------------------------------
    public static readonly Color Background = Color.FromArgb(0x0E, 0x17, 0x29);
    public static readonly Color Surface = Color.FromArgb(0x15, 0x21, 0x3A);
    public static readonly Color SurfaceHover = Color.FromArgb(0x1C, 0x2D, 0x4F);
    public static readonly Color SurfacePressed = Color.FromArgb(0x23, 0x39, 0x6A);
    public static readonly Color Border = Color.FromArgb(0x26, 0x37, 0x5A);
    public static readonly Color Separator = Color.FromArgb(0x1F, 0x2D, 0x4A);
    public static readonly Color Text = Color.FromArgb(0xE7, 0xEE, 0xFB);
    public static readonly Color TextMuted = Color.FromArgb(0x8D, 0xA2, 0xC7);
    public static readonly Color TextDim = Color.FromArgb(0x5B, 0x6E, 0x93);
    /// <summary>Texto secundário à direita dos itens (resolução, atalho) —
    /// mais claro que TextMuted para continuar nítido em letra pequena.</summary>
    public static readonly Color TextSecondary = Color.FromArgb(0xA6, 0xB8, 0xD8);
    public static readonly Color SectionText = Color.FromArgb(0x74, 0x89, 0xB0);
    public static readonly Color HeaderSubtitle = Color.FromArgb(0xB4, 0xC7, 0xE8);
    public static readonly Color Accent = Color.FromArgb(0x3B, 0x82, 0xF6);
    public static readonly Color AccentHover = Color.FromArgb(0x60, 0xA5, 0xFA);
    public static readonly Color AccentPressed = Color.FromArgb(0x25, 0x63, 0xEB);
    public static readonly Color Cyan = Color.FromArgb(0x22, 0xD3, 0xEE);
    public static readonly Color Danger = Color.FromArgb(0xF8, 0x71, 0x71);
    public static readonly Color HeaderStart = Color.FromArgb(0x1B, 0x3A, 0x78);
    public static readonly Color HeaderEnd = Color.FromArgb(0x10, 0x1D, 0x36);
    public static readonly Color KeyCap = Color.FromArgb(0x1E, 0x2D, 0x4D);
    public static readonly Color KeyCapBorder = Color.FromArgb(0x2E, 0x43, 0x70);

    /// <summary>Cor de cada perfil principal: Perfil 1 = azul, Perfil 2 = ciano
    /// (as mesmas cores das duas setas do ícone).</summary>
    public static Color ProfileColor(int index) => index == 0 ? Accent : Cyan;

    // --- Fontes (vivem o processo inteiro; nunca são descartadas) -----------
    private const string FontFamilyName = "Segoe UI";
    public static readonly Font UiFont = new(FontFamilyName, 9f);
    public static readonly Font UiFontBold = new(FontFamilyName, 9f, FontStyle.Bold);
    public static readonly Font SmallFont = new(FontFamilyName, 8f);
    public static readonly Font SectionFont = new(FontFamilyName, 7.5f, FontStyle.Bold);
    public static readonly Font TitleFont = new(FontFamilyName, 11.5f, FontStyle.Bold);
    public static readonly Font MenuTitleFont = new(FontFamilyName, 10.5f, FontStyle.Bold);
    public static readonly Font KeyFont = new(FontFamilyName, 9f, FontStyle.Bold);

    // --- DPI ---------------------------------------------------------------
    public static int Scale(int logical, int dpi) => (int)Math.Round(logical * dpi / 96.0);
    public static float Scale(float logical, int dpi) => logical * dpi / 96f;

    // --- Ícone do app --------------------------------------------------------
    private static Icon? _appIcon;

    /// <summary>
    /// Ícone do app (todos os tamanhos, 16 a 256 px), lido do recurso
    /// embutido no .exe — assim funciona mesmo no build "arquivo único", sem
    /// depender de um app.ico solto ao lado do executável. Instância única do
    /// processo: quem usa NUNCA deve descartá-la.
    /// </summary>
    public static Icon AppIcon => _appIcon ??= LoadAppIcon();

    private static Icon LoadAppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HotSwap.app.ico");
            if (stream != null)
                return new Icon(stream);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
                return new Icon(iconPath);
        }
        catch
        {
            // ícone corrompido/ilegível: cai no ícone padrão abaixo
        }
        return SystemIcons.Application;
    }

    /// <summary>Bitmap do ícone do app no tamanho pedido (usa o quadro mais
    /// próximo do .ico — os tamanhos pequenos foram desenhados à mão).
    /// Cacheado por tamanho; não descartar.</summary>
    public static Bitmap AppLogo(int px)
    {
        lock (LogoCache)
        {
            if (!LogoCache.TryGetValue(px, out var bmp))
            {
                using var sized = new Icon(AppIcon, px, px);
                bmp = new Bitmap(px, px, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.DrawIcon(sized, new Rectangle(0, 0, px, px));
                LogoCache[px] = bmp;
            }
            return bmp;
        }
    }
    private static readonly Dictionary<int, Bitmap> LogoCache = new();

    // --- Formas --------------------------------------------------------------
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Desenha um "switch" estilo Windows 11 (pílula com bolinha).</summary>
    public static void DrawSwitch(Graphics g, RectangleF r, bool on, bool enabled)
    {
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = on ? (enabled ? Accent : TextDim) : Surface;
        using (var path = RoundedRect(r, r.Height / 2f))
        {
            using var b = new SolidBrush(track);
            g.FillPath(b, path);
            if (!on)
            {
                using var p = new Pen(enabled ? TextMuted : TextDim, Math.Max(1f, r.Height / 14f));
                g.DrawPath(p, path);
            }
        }

        float knob = r.Height * 0.62f;
        float pad = (r.Height - knob) / 2f;
        float x = on ? r.Right - pad - knob : r.X + pad;
        using (var kb = new SolidBrush(on ? Color.White : (enabled ? TextMuted : TextDim)))
            g.FillEllipse(kb, x, r.Y + pad, knob, knob);

        g.SmoothingMode = oldMode;
    }

    // --- Atalhos: nome amigável para exibir -----------------------------------
    /// <summary>
    /// Converte "Ctrl+Alt+D1" em partes amigáveis ["Ctrl","Alt","1"] só para
    /// EXIBIÇÃO — o texto salvo no JSON continua usando os nomes de Keys do
    /// .NET, que é o que GlobalHotkey.TryParse entende.
    /// </summary>
    public static string[] HotkeyDisplayParts(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return Array.Empty<string>();
        return hotkey
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(KeyDisplayName)
            .ToArray();
    }

    public static string FormatHotkey(string hotkey) => string.Join("+", HotkeyDisplayParts(hotkey));

    private static string KeyDisplayName(string token)
    {
        if (token.Length == 2 && token[0] == 'D' && char.IsDigit(token[1]))
            return token[1].ToString();
        if (token.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && token.Length == 7 && char.IsDigit(token[6]))
            return "Num " + token[6];

        return token switch
        {
            "Control" => "Ctrl",
            "Return" or "Enter" => "Enter",
            "Next" or "PageDown" => "PgDn",
            "Prior" or "PageUp" => "PgUp",
            "Capital" or "CapsLock" => "Caps",
            "Escape" => "Esc",
            "Delete" => "Del",
            "Insert" => "Ins",
            "Back" => "Backspace",
            "Oemcomma" => ",",
            "OemPeriod" => ".",
            "OemMinus" => "-",
            "Oemplus" => "=",
            "Oem1" or "OemSemicolon" => ";",
            "Oem2" or "OemQuestion" => "/",
            "Oem3" or "Oemtilde" => "`",
            "Oem4" or "OemOpenBrackets" => "[",
            "Oem5" or "OemPipe" => "\\",
            "Oem6" or "OemCloseBrackets" => "]",
            "Oem7" or "OemQuotes" => "'",
            "Oem102" or "OemBackslash" => "\\",
            "Multiply" => "Num *",
            "Add" => "Num +",
            "Subtract" => "Num -",
            "Divide" => "Num /",
            "Decimal" => "Num ,",
            _ => token,
        };
    }

    // --- Menus ---------------------------------------------------------------
    /// <summary>
    /// Aplica o tema a um menu suspenso (o menu da bandeja e cada submenu
    /// dele — submenus NÃO herdam o renderer do menu pai, então precisam ser
    /// tematizados um a um; esta função desce a árvore sozinha).
    /// </summary>
    public static void StyleDropDown(ToolStripDropDown dropDown)
    {
        int dpi = dropDown.DeviceDpi;

        dropDown.Renderer = MenuRenderer.Instance;
        dropDown.BackColor = Background;
        dropDown.ForeColor = Text;
        dropDown.Font = UiFont;
        // No Windows 11 a sombra vem do próprio DWM junto com os cantos
        // arredondados; a sombra "clássica" (CS_DROPSHADOW) é retangular e
        // ficaria aparecendo nos cantos.
        dropDown.DropShadowEnabled = !IsWindows11;

        if (dropDown is ToolStripDropDownMenu menu)
        {
            menu.ShowCheckMargin = false;
            menu.ShowImageMargin = true;
            menu.Padding = new Padding(0, Scale(4, dpi), 0, Scale(4, dpi));
        }

        dropDown.HandleCreated -= OnDropDownHandleCreated;
        dropDown.HandleCreated += OnDropDownHandleCreated;
        if (dropDown.IsHandleCreated)
            ApplyRoundedPopup(dropDown.Handle);

        foreach (ToolStripItem item in dropDown.Items)
        {
            if (item is ToolStripDropDownItem dropItem && dropItem.HasDropDownItems)
                StyleDropDown(dropItem.DropDown);
        }
    }

    private static void OnDropDownHandleCreated(object? sender, EventArgs e)
    {
        if (sender is Control c)
            ApplyRoundedPopup(c.Handle);
    }

    // --- Ajustes nativos (DWM) -------------------------------------------------
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809–1909
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;     // Windows 10 2004+ / 11
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;    // Windows 11
    private const int DWMWA_BORDER_COLOR = 34;                // Windows 11
    private const int DWMWA_CAPTION_COLOR = 35;               // Windows 11
    private const int DWMWA_TEXT_COLOR = 36;                  // Windows 11
    private const int DWMWCP_ROUND = 2;

    /// <summary>Windows 11 = build 22000+. (No .NET 5+, OSVersion devolve a
    /// versão real, sem a "mentira" de compatibilidade do .NET Framework.)</summary>
    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static void TrySetAttribute(IntPtr hwnd, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    /// <summary>Barra de título escura (Win10 2004+) e com a cor do tema (Win11).
    /// Em versões que não suportam, simplesmente não faz nada.</summary>
    public static void ApplyDarkTitleBar(IntPtr hwnd)
    {
        int on = 1;
        int result = -1;
        try
        {
            result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            if (result != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));
        }
        catch (DllNotFoundException) { return; }
        catch (EntryPointNotFoundException) { return; }

        if (IsWindows11)
        {
            TrySetAttribute(hwnd, DWMWA_CAPTION_COLOR, ColorRef(Background));
            TrySetAttribute(hwnd, DWMWA_TEXT_COLOR, ColorRef(Text));
            TrySetAttribute(hwnd, DWMWA_BORDER_COLOR, ColorRef(Border));
        }
    }

    /// <summary>Cantos arredondados + borda na cor do tema para menus (Win11).</summary>
    public static void ApplyRoundedPopup(IntPtr hwnd)
    {
        if (!IsWindows11) return;
        TrySetAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        TrySetAttribute(hwnd, DWMWA_BORDER_COLOR, ColorRef(Border));
    }
}
