using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace HotSwap;

/// <summary>Ícones de linha usados no menu e nos diálogos.</summary>
internal enum Glyph
{
    Monitor,
    Swap,
    Save,
    Keyboard,
    Bell,
    Refresh,
    Folder,
    Info,
    Power,
}

/// <summary>
/// Desenha os ícones de linha (traço arredondado, estilo Fluent) em código,
/// no tamanho exato em pixels pedido — fica nítido em qualquer escala de DPI,
/// sem precisar de arquivos de imagem extras. Tudo é desenhado num espaço de
/// 16×16 unidades e escalado. Os bitmaps são cacheados e vivem o processo
/// inteiro (o menu é reconstruído várias vezes; reaproveitar evita vazar
/// handles GDI a cada reconstrução).
/// </summary>
internal static class Glyphs
{
    private static readonly Dictionary<(Glyph, int, int), Bitmap> Cache = new();
    private static readonly Dictionary<(int, int, int), Bitmap> ChipCache = new();

    public static Bitmap Get(Glyph glyph, int px, Color color)
    {
        var key = (glyph, px, color.ToArgb());
        lock (Cache)
        {
            if (!Cache.TryGetValue(key, out var bmp))
            {
                bmp = Render(glyph, px, color);
                Cache[key] = bmp;
            }
            return bmp;
        }
    }

    /// <summary>Bolinha colorida com o número do perfil (1 ou 2) — a mesma
    /// linguagem visual do selo no ícone da bandeja.</summary>
    public static Bitmap ProfileChip(int number, int px, Color color)
    {
        var key = (number, px, color.ToArgb());
        lock (ChipCache)
        {
            if (!ChipCache.TryGetValue(key, out var bmp))
            {
                bmp = new Bitmap(px, px, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                DrawNumberBadge(g, new RectangleF(0.5f, 0.5f, px - 1f, px - 1f), number, color);
                ChipCache[key] = bmp;
            }
            return bmp;
        }
    }

    /// <summary>Círculo preenchido com um número centralizado (usado no
    /// menu e no selo do ícone da bandeja).</summary>
    public static void DrawNumberBadge(Graphics g, RectangleF r, int number, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var fill = new SolidBrush(color))
            g.FillEllipse(fill, r);

        // Desenha o número como "path" para centralizar pelo contorno real
        // dos glifos (centralizar por MeasureString deixa o número torto em
        // tamanhos pequenos por causa do espaço interno da fonte).
        using var path = new GraphicsPath();
        float em = r.Height * 0.9f;
        FontFamily family;
        bool ownsFamily;
        try
        {
            family = new FontFamily("Segoe UI");
            ownsFamily = true;
        }
        catch (ArgumentException)
        {
            // Fonte ausente (improvável no Windows): usa a sans-serif padrão,
            // que é compartilhada e não deve ser descartada.
            family = FontFamily.GenericSansSerif;
            ownsFamily = false;
        }
        try
        {
            using var typographic = StringFormat.GenericTypographic; // devolve uma instância nova a cada chamada
            path.AddString(number.ToString(), family, (int)FontStyle.Bold, em, PointF.Empty, typographic);
        }
        finally
        {
            if (ownsFamily)
                family.Dispose();
        }
        var bounds = path.GetBounds();
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            using var m = new Matrix();
            m.Translate(
                r.X + (r.Width - bounds.Width) / 2f - bounds.X,
                r.Y + (r.Height - bounds.Height) / 2f - bounds.Y);
            path.Transform(m);
            using var textBrush = new SolidBrush(Theme.Background);
            g.FillPath(textBrush, path);
        }
    }

    private static Bitmap Render(Glyph glyph, int px, Color color)
    {
        var bmp = new Bitmap(px, px, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.ScaleTransform(px / 16f, px / 16f);

        // Traço um pouco mais grosso em tamanhos pequenos para não "sumir".
        float stroke = px <= 16 ? 1.5f : 1.35f;
        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var brush = new SolidBrush(color);

        switch (glyph)
        {
            case Glyph.Monitor:
                using (var body = Theme.RoundedRect(new RectangleF(1.5f, 2.5f, 13f, 9f), 1.6f))
                    g.DrawPath(pen, body);
                g.DrawLine(pen, 8f, 11.5f, 8f, 13.5f);
                g.DrawLine(pen, 5f, 13.5f, 11f, 13.5f);
                break;

            case Glyph.Swap:
                // Seta para cima (esquerda) e para baixo (direita) — o mesmo
                // desenho do ícone do app.
                g.DrawLine(pen, 5f, 13f, 5f, 3f);
                g.DrawLines(pen, new[] { new PointF(2f, 6f), new PointF(5f, 3f), new PointF(8f, 6f) });
                g.DrawLine(pen, 11f, 3f, 11f, 13f);
                g.DrawLines(pen, new[] { new PointF(8f, 10f), new PointF(11f, 13f), new PointF(14f, 10f) });
                break;

            case Glyph.Save:
                g.DrawLine(pen, 8f, 2f, 8f, 10f);
                g.DrawLines(pen, new[] { new PointF(4.75f, 6.75f), new PointF(8f, 10f), new PointF(11.25f, 6.75f) });
                g.DrawLines(pen, new[] { new PointF(2.5f, 10.5f), new PointF(2.5f, 13.5f), new PointF(13.5f, 13.5f), new PointF(13.5f, 10.5f) });
                break;

            case Glyph.Keyboard:
                using (var body = Theme.RoundedRect(new RectangleF(1.5f, 3.5f, 13f, 9f), 1.6f))
                    g.DrawPath(pen, body);
                foreach (var x in new[] { 4.5f, 7f, 9.5f, 12f })
                    g.FillEllipse(brush, x - 0.75f, 6.25f - 0.75f, 1.5f, 1.5f);
                g.DrawLine(pen, 5f, 9.5f, 11f, 9.5f);
                break;

            case Glyph.Bell:
                using (var bell = new GraphicsPath())
                {
                    bell.AddLine(3.5f, 11.5f, 4.5f, 10f);
                    bell.AddLine(4.5f, 10f, 4.5f, 7f);
                    bell.AddArc(4.5f, 3f, 7f, 7f, 180, 180);
                    bell.AddLine(11.5f, 6.5f, 11.5f, 10f);
                    bell.AddLine(11.5f, 10f, 12.5f, 11.5f);
                    bell.CloseFigure();
                    g.DrawPath(pen, bell);
                }
                g.DrawLine(pen, 6.75f, 13.75f, 9.25f, 13.75f);
                g.DrawLine(pen, 8f, 1.75f, 8f, 3f);
                break;

            case Glyph.Refresh:
            {
                // Arco quase completo com a ponta da seta no fim.
                g.DrawArc(pen, 2.5f, 2.5f, 11f, 11f, 30f, 290f);
                double end = (30 + 290) * Math.PI / 180.0;
                var tip = new PointF(8f + 5.5f * (float)Math.Cos(end), 8f + 5.5f * (float)Math.Sin(end));
                // Direção do movimento (sentido horário na tela) no fim do arco.
                var dir = new PointF(-(float)Math.Sin(end), (float)Math.Cos(end));
                DrawArrowHead(g, pen, tip, dir, 3f);
                break;
            }

            case Glyph.Folder:
                using (var folder = new GraphicsPath())
                {
                    folder.AddLines(new[]
                    {
                        new PointF(1.5f, 12.5f), new PointF(1.5f, 3.5f), new PointF(6f, 3.5f),
                        new PointF(7.5f, 5f), new PointF(14.5f, 5f), new PointF(14.5f, 12.5f),
                    });
                    folder.CloseFigure();
                    g.DrawPath(pen, folder);
                }
                g.DrawLine(pen, 1.5f, 7f, 14.5f, 7f);
                break;

            case Glyph.Info:
                g.DrawEllipse(pen, 2f, 2f, 12f, 12f);
                g.DrawLine(pen, 8f, 7.5f, 8f, 11f);
                g.FillEllipse(brush, 7.1f, 4.4f, 1.8f, 1.8f);
                break;

            case Glyph.Power:
                g.DrawArc(pen, 2.5f, 3f, 11f, 11f, 300f, 300f);
                g.DrawLine(pen, 8f, 1.75f, 8f, 7.5f);
                break;
        }

        return bmp;
    }

    private static void DrawArrowHead(Graphics g, Pen pen, PointF tip, PointF dir, float length)
    {
        // Duas "abas" da ponta, a ±40° da direção oposta ao movimento.
        double back = Math.Atan2(-dir.Y, -dir.X);
        const double spread = 40 * Math.PI / 180.0;
        var a = new PointF(tip.X + length * (float)Math.Cos(back + spread), tip.Y + length * (float)Math.Sin(back + spread));
        var b = new PointF(tip.X + length * (float)Math.Cos(back - spread), tip.Y + length * (float)Math.Sin(back - spread));
        g.DrawLines(pen, new[] { a, tip, b });
    }
}
