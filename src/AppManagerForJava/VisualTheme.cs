using System.Drawing.Drawing2D;
namespace AppManagerForJava;

internal static class VisualTheme
{
    public static readonly Color Canvas = Color.FromArgb(246, 245, 251);
    public static readonly Color Ink = Color.FromArgb(44, 39, 73);
    public static readonly Color Muted = Color.FromArgb(116, 110, 140);
    public static readonly Color Accent = Color.FromArgb(117, 83, 218);
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) return path;
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    public static void FillRound(Graphics g, Brush b, RectangleF r, float radius) { using var path = Round(r, radius); g.FillPath(b, path); }
    public static void Apply(Form form)
    {
        form.BackColor=Canvas; form.ForeColor=Ink; form.Font=new Font("Segoe UI",10);
        void Style(Control parent)
        {
            foreach(Control control in parent.Controls)
            {
                if(control is Button button)
                {
                    button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderColor=Color.FromArgb(225,216,244);
                    button.FlatAppearance.MouseOverBackColor=Color.FromArgb(236,227,253);
                    button.BackColor=Color.White; button.ForeColor=Accent; button.Cursor=Cursors.Hand;
                }
                else if(control is TextBox text) { text.BackColor=Color.White; text.ForeColor=Ink; text.BorderStyle=BorderStyle.FixedSingle; }
                else if(control is Label label) label.ForeColor=Muted;
                Style(control);
            }
        }
        Style(form);
    }
    public static Color[] Palette(string name)
    {
        Color[][] pairs = {
            new[] { Color.FromArgb(134, 101, 230), Color.FromArgb(87, 64, 172) },
            new[] { Color.FromArgb(82, 181, 174), Color.FromArgb(39, 123, 149) },
            new[] { Color.FromArgb(239, 156, 121), Color.FromArgb(209, 99, 135) },
            new[] { Color.FromArgb(111, 163, 235), Color.FromArgb(83, 109, 196) },
            new[] { Color.FromArgb(206, 135, 204), Color.FromArgb(149, 85, 169) }
        };
        uint hash = 2166136261; foreach (char c in name) hash = (hash ^ c) * 16777619;
        return pairs[hash % (uint)pairs.Length];
    }
}
internal sealed class RoundPanel : Panel
{
    public RoundPanel() { DoubleBuffered = true; BackColor = Color.White; }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e); if (Width < 2 || Height < 2) return;
        using var path = VisualTheme.Round(new RectangleF(0, 0, Width, Height), 20 * DeviceDpi / 96f);
        var old = Region; Region = new Region(path); old?.Dispose();
    }
}
internal sealed class StartOrb : Button
{
    private bool hover;
    public StartOrb()
    {
        Text = "Menu"; AccessibleName = "Open application menu"; AccessibleDescription = "Launch, manage apps and profiles, and configure Java or storage";
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e); if (Width < 2 || Height < 2) return;
        using var shape = new GraphicsPath(); shape.AddEllipse(ClientRectangle);
        var old = Region; Region = new Region(shape); old?.Dispose();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float s = DeviceDpi / 96f;
        var ring = new RectangleF(2*s, 2*s, Width-4*s, Height-4*s);
        using var gradient = new LinearGradientBrush(ClientRectangle, hover ? Color.FromArgb(175, 137, 249) : Color.FromArgb(155, 117, 239), Color.FromArgb(88, 62, 177), 55f);
        g.FillEllipse(gradient, ring); using var pen = new Pen(Color.FromArgb(200, 196, 164, 246), 1.5f*s); g.DrawEllipse(pen, ring);
        float dot=7*s, gap=5*s, startX=(Width-(dot*2+gap))/2, startY=(Height-(dot*2+gap))/2;
        for(int y=0;y<2;y++) for(int x=0;x<2;x++) VisualTheme.FillRound(g, Brushes.White, new RectangleF(startX+x*(dot+gap),startY+y*(dot+gap),dot,dot),2*s);
        if (Focused) { using var focus = new Pen(Color.FromArgb(224, 211, 255), 2*s) { DashStyle = DashStyle.Dot }; g.DrawEllipse(focus, new RectangleF(6*s,6*s,Width-12*s,Height-12*s)); }
    }
}
internal sealed class HoverInfoStrip : Control
{
    public string AppTitle { get; private set; } = "App Manager for Java";
    public string Description { get; private set; } = "A beautiful home for your Java applications";
    public string Detail { get; private set; } = "Hover over an app to explore it";
    public void SetInfo(string title, string description, string detail) { AppTitle=title; Description=description; Detail=detail; Invalidate(); }
    public HoverInfoStrip() { DoubleBuffered=true; BackColor=Color.White; AccessibleName="Application information"; }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s=DeviceDpi/96f; int x=(int)(12*s), width=Width-x-(int)(14*s);
        using var titleFont=new Font("Segoe UI Semibold",14); using var bodyFont=new Font("Segoe UI",9);
        TextRenderer.DrawText(e.Graphics,AppTitle,titleFont,new Rectangle(x,(int)(12*s),width,(int)(27*s)),VisualTheme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics,Description,bodyFont,new Rectangle(x,(int)(42*s),width,(int)(21*s)),VisualTheme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics,Detail,bodyFont,new Rectangle(x,(int)(65*s),width,(int)(20*s)),VisualTheme.Accent,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
    }
}
