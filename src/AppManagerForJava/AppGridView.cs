using System.Drawing.Drawing2D;
namespace AppManagerForJava;

internal sealed class AppGridView : ListView
{
    private ListViewItem? hovered;
    public event Action<JarApp?>? AppHovered;
    public Func<Guid,bool>? IsAppRunning { get; set; }
    public AppGridView()
    {
        DoubleBuffered=true; OwnerDraw=true; View=View.Tile; MultiSelect=false; HideSelection=false;
        BorderStyle=BorderStyle.None; BackColor=VisualTheme.Canvas; ForeColor=VisualTheme.Ink;
        ShowGroups=false; TileSize=new Size(246,210); Columns.Add("Application",220); FullRowSelect=true;
        AccessibleName="Java application library";
    }
    private void Reflow()
    {
        if (ClientSize.Width < 20) return;
        float scale = DeviceDpi / 96f;
        int columns = Math.Max(1, (int)(ClientSize.Width / (246*scale)));
        var size = new Size(Math.Max((int)(200*scale), (ClientSize.Width-(int)(4*scale))/columns), (int)(210*scale));
        if(TileSize!=size)TileSize=size;
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Reflow(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if(IsHandleCreated)Reflow(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Reflow(); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); SetHoveredItem(GetItemAt(e.X,e.Y));
    }
    internal void SetHoveredItem(ListViewItem? item)
    {
        if(item==hovered)return;
        var previous=hovered; hovered=item; if(previous!=null&&previous.ListView==this)Invalidate(previous.Bounds); if(item!=null)Invalidate(item.Bounds);
        AppHovered?.Invoke(item?.Tag as JarApp);
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHoveredItem(null); }
    public void ResetHover() { hovered=null; AppHovered?.Invoke(null); }
    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        if(View!=View.Tile) { e.DrawDefault=true; return; }
        if(e.Item.Tag is not JarApp app)return;
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; float s=DeviceDpi/96f;
        using(var canvas=new SolidBrush(BackColor))g.FillRectangle(canvas,e.Bounds);
        bool hot=hovered==e.Item, selected=e.Item.Selected;
        var card=new RectangleF(e.Bounds.X+8*s,e.Bounds.Y+7*s,e.Bounds.Width-16*s,e.Bounds.Height-18*s);
        using(var shadow=new SolidBrush(Color.FromArgb(hot?24:12,79,62,127))) VisualTheme.FillRound(g,shadow,new RectangleF(card.X,card.Y+(hot?5:3)*s,card.Width,card.Height),18*s);
        using(var fill=new SolidBrush(selected?Color.FromArgb(240,234,255):Color.White)) VisualTheme.FillRound(g,fill,card,18*s);
        using(var outline=new Pen(selected?VisualTheme.Accent:hot?Color.FromArgb(201,183,239):Color.FromArgb(233,229,243),selected?1.7f*s:1*s))
        using(var shape=VisualTheme.Round(card,18*s))g.DrawPath(outline,shape);
        var colors=VisualTheme.Palette(app.Name); var icon=new RectangleF(card.X+20*s,card.Y+19*s,58*s,58*s);
        using(var brush=new LinearGradientBrush(icon,colors[0],colors[1],60f))VisualTheme.FillRound(g,brush,icon,17*s);
        using(var shine=new SolidBrush(Color.FromArgb(40,255,255,255)))g.FillEllipse(shine,icon.X+3*s,icon.Y+3*s,38*s,27*s);
        string initial=string.IsNullOrWhiteSpace(app.Name)?"J":System.Globalization.StringInfo.GetNextTextElement(app.Name);
        using(var f=new Font("Segoe UI Semibold",22))TextRenderer.DrawText(g,initial,f,Rectangle.Round(icon),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        bool active=IsAppRunning?.Invoke(app.Id)==true;
        using(var dot=new SolidBrush(active?Color.FromArgb(54,174,142):Color.FromArgb(191,183,211)))g.FillEllipse(dot,card.Right-26*s,card.Top+25*s,7*s,7*s);
        using(var title=new Font("Segoe UI Semibold",11))TextRenderer.DrawText(g,app.Name,title,new Rectangle((int)(card.X+18*s),(int)(card.Y+89*s),(int)(card.Width-36*s),(int)(29*s)),VisualTheme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        using(var small=new Font("Segoe UI",8))
        {
            TextRenderer.DrawText(g,"JAVA APPLICATION",small,new Rectangle((int)(card.X+19*s),(int)(card.Y+119*s),(int)(card.Width-35*s),(int)(18*s)),VisualTheme.Muted,TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g,active?"● Running":"Ready to launch",small,new Rectangle((int)(card.X+19*s),(int)(card.Bottom-34*s),(int)(card.Width-55*s),(int)(20*s)),active?Color.FromArgb(39,140,113):VisualTheme.Accent,TextFormatFlags.SingleLine);
        }
        using(var pen=new Pen(VisualTheme.Accent,1.6f*s)) { float x=card.Right-25*s,y=card.Bottom-25*s; g.DrawLine(pen,x-5*s,y,x+4*s,y);g.DrawLine(pen,x,y-4*s,x+4*s,y);g.DrawLine(pen,x,y+4*s,x+4*s,y); }
        if(e.Item.Focused&&Focused)ControlPaint.DrawFocusRectangle(g,Rectangle.Round(new RectangleF(card.X+5*s,card.Y+5*s,card.Width-10*s,card.Height-10*s)),VisualTheme.Accent,Color.White);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // ListView is a native control; the empty-state message uses its native property.
    }
}
