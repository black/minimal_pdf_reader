using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

// ════════════════════════════════════════════════════════════════════════════════
//  Native bindings
// ════════════════════════════════════════════════════════════════════════════════
public static class PdfNative
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    // Native caption is back (no custom title bar) — just tell it to render dark.
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_InitLibrary();
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_DestroyLibrary();
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr FPDF_LoadDocument(string path, string password);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_CloseDocument(IntPtr doc);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int FPDF_GetPageCount(IntPtr doc);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_ClosePage(IntPtr page);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern double FPDF_GetPageWidth(IntPtr page);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern double FPDF_GetPageHeight(IntPtr page);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr FPDFBitmap_Create(int w, int h, int alpha);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDFBitmap_FillRect(IntPtr bmp, int l, int t, int w, int h, int color);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDFBitmap_Destroy(IntPtr bmp);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bmp);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int FPDFBitmap_GetStride(IntPtr bmp);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_RenderPageBitmap(IntPtr bmp, IntPtr page, int x, int y, int w, int h, int rot, int flags);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern uint FPDF_GetLastError();
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr FPDFText_LoadPage(IntPtr page);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDFText_ClosePage(IntPtr tp);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int FPDFText_GetCharIndexAtPos(IntPtr tp, double x, double y, double xt, double yt);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int FPDFText_GetText(IntPtr tp, int start, int count, byte[] buf);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int FPDFText_CountRects(IntPtr tp, int start, int count);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern bool FPDFText_GetRect(IntPtr tp, int idx, out double l, out double t, out double r, out double b);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_DeviceToPage(IntPtr page, int sx, int sy, int sw, int sh, int rot, int dx, int dy, out double px, out double py);
    [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void FPDF_PageToDevice(IntPtr page, int sx, int sy, int sw, int sh, int rot, double px, double py, out int dx, out int dy);
}

// ════════════════════════════════════════════════════════════════════════════════
//  Theme — Windows 11 Fluent-inspired palette
// ════════════════════════════════════════════════════════════════════════════════
public static class T
{
    public static bool Dark = true;

    public const int Radius    = 4;    // corner radius for buttons / pills (WinUI default)
    public const int TabRadius = 9;    // corner radius for the top of tabs

    static Color D(int r, int g, int b) { return Color.FromArgb(r, g, b); }

    // Neutral surfaces — two tiers, matching the design file exactly:
    // Bg = header/chrome tier (#202020), Bar = content-card tier (#272727), everything
    // below the title bar (toolbar, tabs, canvas, status bar) shares the Bar tone.
    public static Color Bg     { get { return Dark ? D(0x20, 0x20, 0x20) : D(255, 255, 255); } }
    public static Color Bar    { get { return Dark ? D(0x27, 0x27, 0x27) : D(247, 247, 247); } }
    // The whole content card (toolbar, tab strip, canvas, status bar) is one uniform
    // surface now that there's no separate header row above it — tabs read as raised
    // pills sitting on that surface rather than a separate darker chrome band.
    public static Color TabBg  { get { return Bar; } }
    public static Color TabOn  { get { return PillBg; } }
    public static Color TabOff { get { return Dark ? Color.FromArgb(16, 255, 255, 255) : Color.FromArgb(10, 0, 0, 0); } }
    public static Color TabHov { get { return Dark ? D(66, 66, 66)    : D(225, 225, 225); } }
    public static Color Canvas { get { return Bar; } }
    public static Color ToggleOn  { get { return D(0xfd, 0xe0, 0x47); } } // amber pill fill from the design file
    public static Color Txt    { get { return Dark ? D(205, 205, 205) : D(50, 50, 50);   } }
    public static Color TxtDim { get { return Dark ? D(155, 155, 155) : D(120, 120, 120); } }
    public static Color TxtBrt { get { return Dark ? D(255, 255, 255) : D(0, 0, 0);      } }
    public static Color BtnHov { get { return Dark ? D(62, 62, 62)    : D(229, 229, 229); } }
    public static Color BtnPrs { get { return Dark ? D(48, 48, 48)    : D(213, 213, 213); } }
    // "Filled" (standard, WinUI-style) buttons stay visible at rest instead of only on hover.
    public static Color PillBg  { get { return Dark ? D(58, 58, 58)   : D(235, 235, 235); } }
    public static Color PillHov { get { return Dark ? D(74, 74, 74)   : D(221, 221, 221); } }
    public static Color Sep    { get { return Dark ? D(60, 60, 60)    : D(225, 225, 225); } }
    public static Color Border { get { return Dark ? D(70, 70, 70)    : D(210, 210, 210); } }

    // Windows system accent — vivid on dark, deep on light
    public static Color Accent    { get { return Dark ? D(96, 205, 255) : D(0, 103, 192);  } }
    public static Color AccentTxt { get { return Dark ? D(10, 30, 45)   : D(255, 255, 255); } }
    public static Color AccentDim { get { return Dark ? D(0, 70, 105)   : D(210, 233, 250); } }

    public static Color StatBg { get { return Bar; } }
    public static Color XHov   { get { return D(196, 43, 28); } }

    static Font _uiFont, _uiBig;
    public static Font UiFont { get { if (_uiFont == null) _uiFont = new Font("Segoe UI", 9f);  return _uiFont; } }
    public static Font UiBig  { get { if (_uiBig  == null) _uiBig  = new Font("Segoe UI", 10f); return _uiBig;  } }

    public static void Reset() { _uiFont = _uiBig = null; }

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Rounded only at the top — used for browser-style tabs
    public static GraphicsPath TopRoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        p.CloseFigure();
        return p;
    }

    // Rounded only at the bottom — mirror of TopRoundRect, for the content card's floor.
    public static GraphicsPath BottomRoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddLine(r.X, r.Y, r.Right, r.Y);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//  Small vector icon painters — avoids any dependency on icon fonts being installed
// ════════════════════════════════════════════════════════════════════════════════
public static class Ico
{
    public static void Chevron(Graphics g, Rectangle r, Color c, bool left)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = 4.5f;
        using (var p = new Pen(c, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            float dir = left ? 1f : -1f;
            g.DrawLines(p, new[] {
                new PointF(cx + dir * s * 0.55f, cy - s),
                new PointF(cx - dir * s * 0.55f, cy),
                new PointF(cx + dir * s * 0.55f, cy + s)
            });
        }
    }

    public static void PlusMinus(Graphics g, Rectangle r, Color c, bool plus)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = 5f;
        using (var p = new Pen(c, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(p, cx - s, cy, cx + s, cy);
            if (plus) g.DrawLine(p, cx, cy - s, cx, cy + s);
        }
    }

    public static void Folder(Graphics g, Rectangle r, Color c)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        var body = new RectangleF(cx - 7, cy - 3.5f, 14, 9);
        var tab  = new RectangleF(cx - 7, cy - 6f, 6, 3);
        using (var p = new Pen(c, 1.6f) { LineJoin = LineJoin.Round })
        {
            g.DrawRectangle(p, body.X, body.Y, body.Width, body.Height);
            g.DrawLines(p, new[] { new PointF(tab.X, tab.Bottom), new PointF(tab.X, tab.Y), new PointF(tab.Right, tab.Y), new PointF(tab.Right + 1, tab.Bottom) });
        }
    }

    // "space_dashboard"-style panel/layout glyph — matches the design file's app-mark icon.
    public static void Dashboard(Graphics g, Rectangle r, Color c)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = 8f;
        var outer = new RectangleF(cx - s, cy - s, s * 2, s * 2);
        using (var p = new Pen(c, 1.4f) { LineJoin = LineJoin.Round })
        {
            g.DrawRectangle(p, outer.X, outer.Y, outer.Width, outer.Height);
            float midX = outer.X + outer.Width * 0.55f;
            g.DrawLine(p, midX, outer.Y, midX, outer.Bottom);
            float midY = outer.Y + outer.Height * 0.5f;
            g.DrawLine(p, midX, midY, outer.Right, midY);
        }
    }

    public static void Sun(Graphics g, Rectangle r, Color c)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, rad = 3.2f;
        using (var p = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
            for (int i = 0; i < 8; i++)
            {
                double ang = i * Math.PI / 4;
                float x1 = cx + (float)Math.Cos(ang) * (rad + 2.5f), y1 = cy + (float)Math.Sin(ang) * (rad + 2.5f);
                float x2 = cx + (float)Math.Cos(ang) * (rad + 5.5f), y2 = cy + (float)Math.Sin(ang) * (rad + 5.5f);
                g.DrawLine(p, x1, y1, x2, y2);
            }
        }
    }

    public static void Moon(Graphics g, Rectangle r, Color c)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, rad = 5.5f;
        using (var full = new GraphicsPath())
        using (var bite = new GraphicsPath())
        {
            full.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
            bite.AddEllipse(cx - rad + 3.5f, cy - rad - 1.5f, rad * 2, rad * 2);
            using (var region = new Region(full))
            {
                region.Exclude(bite);
                using (var b = new SolidBrush(c)) g.FillRegion(b, region);
            }
        }
    }

}

// ════════════════════════════════════════════════════════════════════════════════
//  Flat button — rounded corners, subtle border/hover states, optional vector icon
// ════════════════════════════════════════════════════════════════════════════════
public class FlatBtn : Control
{
    bool _hov, _prs;
    public bool Accent;
    public bool Toggled;
    public bool Filled;    // "standard" WinUI button — visible pill at rest, not just on hover
    public Action<Graphics, Rectangle, Color> Icon;

    public FlatBtn(string text, int w = 72, int h = 30)
    {
        Text = text; Size = new Size(w, h);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand; Font = T.UiBig;
    }

    protected override void OnMouseEnter(EventArgs e) { _hov = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hov = _prs = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _prs = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)   { _prs = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool special = Accent || Toggled;
        Color bg = special ? T.Accent
                 : _prs ? T.BtnPrs
                 : _hov ? (Filled ? T.PillHov : T.BtnHov)
                 : Filled ? T.PillBg
                 : Color.Empty;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = T.RoundRect(rect, T.Radius))
        {
            if (bg != Color.Empty)
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            if (!special && (_hov || _prs))
                using (var p = new Pen(T.Border)) g.DrawPath(p, path);
        }

        Color fg = !Enabled ? T.TxtDim
                 : special ? T.AccentTxt
                 : T.TxtBrt;

        if (Icon != null && !string.IsNullOrEmpty(Text))
        {
            var iconRect = new Rectangle(8, 0, 22, Height);
            Icon(g, iconRect, fg);
            var textRect = new Rectangle(iconRect.Right, 0, Width - iconRect.Right - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
        else if (Icon != null)
        {
            Icon(g, ClientRectangle, fg);
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        }
    }

    public void Refresh2() { Font = T.UiFont; Invalidate(); }
}

// ════════════════════════════════════════════════════════════════════════════════
//  Theme toggle — amber pill switch (from the design file), sun/moon icon + sliding thumb
// ════════════════════════════════════════════════════════════════════════════════
public class ThemeSwitch : Control
{
    bool _hov;
    bool _on;
    float _pos; // animated 0 (dark/moon/thumb-left) .. 1 (light/sun/thumb-right)
    Timer _anim;
    public event EventHandler Toggled;

    public bool On
    {
        get { return _on; }
        set
        {
            if (_on == value) return; // already correct — don't stomp an in-progress animation
            _on = value;
            _pos = value ? 1f : 0f;   // programmatic set (e.g. initial sync) — snap, no animation
            Invalidate();
        }
    }

    public ThemeSwitch()
    {
        Size = new Size(76, 36);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        _anim = new Timer { Interval = 15 };
        _anim.Tick += (s, e) => StepAnim();
    }

    void StepAnim()
    {
        float target = _on ? 1f : 0f;
        _pos += (target - _pos) * 0.25f; // eased — fast start, settles smoothly
        if (Math.Abs(target - _pos) < 0.01f) { _pos = target; _anim.Stop(); }
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hov = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hov = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e)
    {
        _on = !_on;
        _anim.Start();
        if (Toggled != null) Toggled(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _anim != null) _anim.Dispose();
        base.Dispose(disposing);
    }

    static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    static Color Lerp(Color a, Color b, float t)
    {
        return Color.FromArgb((int)Lerp(a.A, b.A, t), (int)Lerp(a.R, b.R, t), (int)Lerp(a.G, b.G, t), (int)Lerp(a.B, b.B, t));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color trackOff = _hov ? T.PillHov : T.PillBg;
        Color trackOn  = _hov ? ControlPaint.Light(T.ToggleOn, 0.1f) : T.ToggleOn;
        using (var path = T.RoundRect(rect, Height / 2))
            using (var b = new SolidBrush(Lerp(trackOff, trackOn, _pos))) g.FillPath(b, path);

        int pad = 6, d = Height - pad * 2;
        float leftX = pad, rightX = Width - pad - d;
        var iconRect  = new Rectangle((int)Lerp(rightX, leftX, _pos), pad, d, d); // right(dark) -> left(light)
        var thumbRect = new Rectangle((int)Lerp(leftX, rightX, _pos), pad, d, d); // left(dark) -> right(light)

        Color iconColor = Lerp(T.TxtBrt, Color.FromArgb(90, 70, 10), _pos);
        if (_pos >= 0.5f) Ico.Sun(g, iconRect, iconColor); else Ico.Moon(g, iconRect, iconColor);

        Color thumbColor = Lerp(T.TxtDim, Color.White, _pos);
        using (var b = new SolidBrush(thumbColor)) g.FillEllipse(b, thumbRect);
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//  Floating pill container — rounded, soft-shadowed, houses a group of controls
//  (used to group zoom in/out/fit into one floating island over the page)
// ════════════════════════════════════════════════════════════════════════════════
public class FloatPill : Control
{
    public const int Shadow = 6; // margin reserved around the visible pill for the shadow to bleed into

    public FloatPill()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var body = Rectangle.Inflate(ClientRectangle, -Shadow, -Shadow);
        for (int i = Shadow; i >= 1; i--)
        {
            int alpha = Math.Min(3 + (Shadow - i) * 5, 45);
            using (var path = T.RoundRect(Rectangle.Inflate(body, i, i), body.Height / 2 + i))
                using (var b = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    g.FillPath(b, path);
        }
        using (var path = T.RoundRect(body, body.Height / 2))
            using (var b = new SolidBrush(T.PillBg)) g.FillPath(b, path);
        using (var path = T.RoundRect(body, body.Height / 2))
            using (var p = new Pen(T.Border)) g.DrawPath(p, path);
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//  Chrome-style tab control with close buttons
// ════════════════════════════════════════════════════════════════════════════════
public class ChromeTabs : TabControl
{
    int _xHov = -1, _tabHov = -1;
    const int TAB_H = 46, CLOSE = 16, TAB_W = 252, GAP = 8, TOP_INSET = 11;

    public ChromeTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);
        ItemSize = new Size(TAB_W, TAB_H);
        SizeMode = TabSizeMode.Fixed;
        Font = T.UiBig;
    }

    // Visible (inset, floating) tab shape — leaves a gap on each side and headroom on top
    Rectangle TabDrawRect(int i)
    {
        var full = GetTabRect(i);
        return new Rectangle(full.X + GAP / 2, full.Y + TOP_INSET, full.Width - GAP, full.Height - TOP_INSET);
    }

    Rectangle XRect(Rectangle r) { return new Rectangle(r.Right - CLOSE - 8, r.Top + (r.Height - CLOSE) / 2, CLOSE, CLOSE); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(T.TabBg);

        for (int i = 0; i < TabCount; i++)
        {
            var r = TabDrawRect(i);
            bool sel  = (SelectedIndex == i);
            bool hov  = (_tabHov == i);
            bool xhov = (_xHov == i);

            Color bg = sel ? T.TabOn : hov ? T.TabHov : T.TabOff;
            using (var path = T.TopRoundRect(r, T.TabRadius))
            {
                if (bg.A > 0)
                    using (var b = new SolidBrush(bg)) g.FillPath(b, path);

                if (sel)
                    using (var p = new Pen(T.Accent, 2.5f))
                        g.DrawLine(p, r.Left + T.TabRadius / 2, r.Top + 1, r.Right - T.TabRadius / 2, r.Top + 1);
            }

            // Title
            var tr = new Rectangle(r.Left + 12, r.Top, r.Width - CLOSE - 26, r.Height);
            TextRenderer.DrawText(g, TabPages[i].Text, Font, tr,
                sel ? T.TxtBrt : T.Txt,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            // Close X
            if (sel || hov)
            {
                var cr = XRect(r);
                if (xhov)
                    using (var b = new SolidBrush(T.XHov))
                        g.FillEllipse(b, cr.Left - 3, cr.Top - 3, cr.Width + 6, cr.Height + 6);
                using (var p = new Pen(xhov ? Color.White : T.Txt, 1.5f))
                {
                    g.DrawLine(p, cr.Left + 2, cr.Top + 2, cr.Right - 2, cr.Bottom - 2);
                    g.DrawLine(p, cr.Right - 2, cr.Top + 2, cr.Left + 2, cr.Bottom - 2);
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int px = _xHov, ph = _tabHov;
        _xHov = _tabHov = -1;
        for (int i = 0; i < TabCount; i++)
        {
            var full = GetTabRect(i);
            if (full.Contains(e.Location))
            {
                _tabHov = i;
                if (XRect(TabDrawRect(i)).Contains(e.Location)) _xHov = i;
                break;
            }
        }
        if (_xHov != px || _tabHov != ph) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _xHov = _tabHov = -1; Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        for (int i = 0; i < TabCount; i++)
            if (XRect(TabDrawRect(i)).Contains(e.Location)) { RequestClose(i); return; }
        base.OnMouseClick(e);
    }

    public void RequestClose(int i)
    {
        if (i < 0 || i >= TabCount) return;
        var page = TabPages[i];
        foreach (Control c in page.Controls)
        { PdfViewer pv = c as PdfViewer; if (pv != null) pv.Dispose(); }
        TabPages.RemoveAt(i);
        Invalidate();
    }

    public void ApplyTheme() { Font = T.UiFont; Invalidate(); }
}

// ════════════════════════════════════════════════════════════════════════════════
//  PDF viewer (canvas only — toolbar lives in the main form)
// ════════════════════════════════════════════════════════════════════════════════
public class PdfViewer : UserControl
{
    IntPtr _doc = IntPtr.Zero, _page = IntPtr.Zero, _textPage = IntPtr.Zero, _pdfBmp = IntPtr.Zero;
    int _pageCount, _currentPage;
    float _zoom = 1.0f;
    bool _selecting;
    int _selStart = -1, _selEnd = -1;
    Timer _resizeTimer;
    Panel _scroll;
    PictureBox _canvas;

    public event EventHandler StateChanged;

    public int   CurrentPage { get { return _currentPage; } }
    public int   PageCount   { get { return _pageCount; } }
    public float ZoomLevel   { get { return _zoom; } }
    public bool  HasDoc      { get { return _doc != IntPtr.Zero; } }

    public event EventHandler LoadFailed;

    public PdfViewer()
    {
        Dock = DockStyle.Fill;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        BuildUI();
    }

    void BuildUI()
    {
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = T.Canvas };
        _canvas = new PictureBox { Location = new Point(0, 0), Size = new Size(1, 1), SizeMode = PictureBoxSizeMode.AutoSize };
        _scroll.Controls.Add(_canvas);

        _canvas.MouseDown += (s, e) => { _selecting = true; _selStart = _selEnd = GetChar(e.X, e.Y); _canvas.Invalidate(); };
        _canvas.MouseMove += (s, e) => {
            if (_selecting) { _selEnd = GetChar(e.X, e.Y); _canvas.Invalidate(); }
            _canvas.Cursor = GetChar(e.X, e.Y) >= 0 ? Cursors.IBeam : Cursors.Default;
        };
        _canvas.MouseUp += (s, e) => _selecting = false;
        _canvas.Paint += OnPaint;
        _scroll.Paint += DrawPageShadow;

        _scroll.MouseWheel += (s, e) => DoScroll(0, -e.Delta);

        Size _lastSz = Size.Empty;
        _resizeTimer = new Timer { Interval = 150 };
        _resizeTimer.Tick += (s, e) => { _resizeTimer.Stop(); if (HasDoc) ShowPage(_currentPage); };
        _scroll.Resize += (s, e) => {
            Center();
            if (_scroll.Width != _lastSz.Width) { _lastSz = _scroll.Size; _resizeTimer.Stop(); _resizeTimer.Start(); }
            else _lastSz = _scroll.Size;
        };

        Controls.Add(_scroll);
    }

    // ── Public navigation ────────────────────────────────────────────────────
    public void PrevPage() { if (_currentPage > 0) ShowPage(_currentPage - 1); }
    public void NextPage() { if (_currentPage < _pageCount - 1) ShowPage(_currentPage + 1); }

    public void SetZoom(float z) { _zoom = Math.Max(0.1f, Math.Min(5f, z)); ShowPage(_currentPage); }
    public void FitWidth()
    {
        if (_page == IntPtr.Zero) return;
        double pw = PdfNative.FPDF_GetPageWidth(_page);
        int vw = _scroll.Width - 30;
        if (vw < 100) vw = 600;
        _zoom = (float)(vw / pw);
        ShowPage(_currentPage);
    }

    // ── File loading ─────────────────────────────────────────────────────────
    public void LoadFile(string path)
    {
        CloseFile();
        try
        {
            _doc = PdfNative.FPDF_LoadDocument(path, null);
            int attempts = 0;
            while (_doc == IntPtr.Zero)
            {
                uint err = PdfNative.FPDF_GetLastError();
                if (err == 4)
                {
                    if (attempts >= 3) { Msg("Maximum password attempts reached."); Fire(LoadFailed); return; }
                    string pw = AskPassword();
                    if (pw == null) { Fire(LoadFailed); return; }
                    _doc = PdfNative.FPDF_LoadDocument(path, pw);
                    attempts++;
                }
                else
                {
                    string[] msgs = { "", "Unknown error.", "File not found.", "File corrupted.", "Password required.", "Unsupported security.", "Page error." };
                    Msg(err < msgs.Length ? msgs[err] : "Failed to open.");
                    Fire(LoadFailed); return;
                }
            }
            _pageCount = PdfNative.FPDF_GetPageCount(_doc);
            _currentPage = 0;
            ShowPage(0);
        }
        catch (Exception ex) { Msg("Error: " + ex.Message); Fire(LoadFailed); }
    }

    public void CloseFile()
    {
        CleanPage();
        if (_doc != IntPtr.Zero) { PdfNative.FPDF_CloseDocument(_doc); _doc = IntPtr.Zero; }
        _pageCount = 0; _currentPage = 0;
        Fire(StateChanged);
    }

    // ── Internal rendering ───────────────────────────────────────────────────
    [HandleProcessCorruptedStateExceptions, SecurityCritical]
    void ShowPage(int index)
    {
        if (_doc == IntPtr.Zero || index < 0 || index >= _pageCount) return;
        CleanPage();
        _currentPage = index;
        _page = PdfNative.FPDF_LoadPage(_doc, index);
        if (_page == IntPtr.Zero) return;

        try
        {
            double pw = PdfNative.FPDF_GetPageWidth(_page);
            double ph = PdfNative.FPDF_GetPageHeight(_page);
            int vw = _scroll.Width - 30; if (vw < 100) vw = 600;
            float scale = (float)(vw / pw) * _zoom;
            int w = (int)(pw * scale), h = (int)(ph * scale);

            _pdfBmp = PdfNative.FPDFBitmap_Create(w, h, 1);
            if (_pdfBmp == IntPtr.Zero) throw new Exception("Bitmap alloc failed");

            PdfNative.FPDFBitmap_FillRect(_pdfBmp, 0, 0, w, h, unchecked((int)0xFFFFFFFF));
            PdfNative.FPDF_RenderPageBitmap(_pdfBmp, _page, 0, 0, w, h, 0, 0x10);

            IntPtr buf = PdfNative.FPDFBitmap_GetBuffer(_pdfBmp);
            int stride = PdfNative.FPDFBitmap_GetStride(_pdfBmp);

            if (buf != IntPtr.Zero && stride >= w * 4)
            {
                unsafe
                {
                    byte* ptr = (byte*)buf.ToPointer();
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = ptr + y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int o = x * 4;
                            byte b = row[o]; row[o] = row[o + 2]; row[o + 2] = b;
                        }
                    }
                }
            }

            _canvas.Image = new Bitmap(w, h, stride, PixelFormat.Format32bppArgb, buf);
        }
        catch (Exception ex) { Msg("Render error: " + ex.Message); }

        try { _textPage = PdfNative.FPDFText_LoadPage(_page); } catch { _textPage = IntPtr.Zero; }

        _scroll.AutoScrollPosition = new Point(0, 0);
        Center();
        Fire(StateChanged);
        GC.Collect(); GC.WaitForPendingFinalizers();
    }

    void CleanPage()
    {
        if (_canvas.Image != null) { _canvas.Image.Dispose(); _canvas.Image = null; }
        if (_pdfBmp != IntPtr.Zero) { PdfNative.FPDFBitmap_Destroy(_pdfBmp); _pdfBmp = IntPtr.Zero; }
        if (_textPage != IntPtr.Zero) { PdfNative.FPDFText_ClosePage(_textPage); _textPage = IntPtr.Zero; }
        if (_page != IntPtr.Zero) { PdfNative.FPDF_ClosePage(_page); _page = IntPtr.Zero; }
        _selStart = _selEnd = -1;
    }

    void Center()
    {
        if (_canvas.Image == null) return;
        int cw = _scroll.ClientSize.Width, ch = _scroll.ClientSize.Height;
        _canvas.Location = new Point(Math.Max(0, (cw - _canvas.Width) / 2), Math.Max(0, (ch - _canvas.Height) / 2));
        _scroll.Invalidate();
    }

    // Soft layered drop-shadow + hairline border behind the page, for a card-like modern look
    void DrawPageShadow(object sender, PaintEventArgs e)
    {
        if (_canvas.Image == null) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = _canvas.Bounds;
        const int blur = 10;
        for (int i = blur; i >= 1; i--)
        {
            int alpha = Math.Min(3 + (blur - i) * 2, 45);
            using (var b = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                g.FillRectangle(b, r.X - i, r.Y - i + 3, r.Width + i * 2, r.Height + i * 2);
        }
        using (var p = new Pen(T.Border)) g.DrawRectangle(p, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1);
    }

    void DoScroll(int dx, int dy)
    {
        Point cur = _scroll.AutoScrollPosition;
        int cy = Math.Abs(cur.Y), maxY = Math.Max(0, _canvas.Height - _scroll.ClientSize.Height);
        if (dy > 0 && cy >= maxY) { NextPage(); return; }
        if (dy < 0 && cy <= 0)   { PrevPage(); return; }
        _scroll.AutoScrollPosition = new Point(Math.Abs(cur.X) + dx, cy + dy);
    }

    int GetChar(int x, int y)
    {
        if (_textPage == IntPtr.Zero || _page == IntPtr.Zero || _canvas.Image == null) return -1;
        try
        {
            double px, py;
            PdfNative.FPDF_DeviceToPage(_page, 0, 0, _canvas.Image.Width, _canvas.Image.Height, 0, x, y, out px, out py);
            return PdfNative.FPDFText_GetCharIndexAtPos(_textPage, px, py, 10, 10);
        }
        catch { return -1; }
    }

    void OnPaint(object sender, PaintEventArgs e)
    {
        if (_textPage == IntPtr.Zero || _selStart < 0 || _selEnd < 0 || _canvas.Image == null) return;
        int s = Math.Min(_selStart, _selEnd), n = Math.Max(_selStart, _selEnd) - s + 1;
        if (n <= 0) return;
        int w = _canvas.Image.Width, h = _canvas.Image.Height;
        try
        {
            int rc = PdfNative.FPDFText_CountRects(_textPage, s, n);
            using (Brush b = new SolidBrush(Color.FromArgb(100, 0, 100, 255)))
                for (int i = 0; i < rc; i++)
                {
                    double l, t, r, bv;
                    if (!PdfNative.FPDFText_GetRect(_textPage, i, out l, out t, out r, out bv)) continue;
                    int x1, y1, x2, y2;
                    PdfNative.FPDF_PageToDevice(_page, 0, 0, w, h, 0, l, t, out x1, out y1);
                    PdfNative.FPDF_PageToDevice(_page, 0, 0, w, h, 0, r, bv, out x2, out y2);
                    e.Graphics.FillRectangle(b, Rectangle.FromLTRB(Math.Min(x1,x2), Math.Min(y1,y2), Math.Max(x1,x2), Math.Max(y1,y2)));
                }
        }
        catch { }
    }

    public void CopySelection()
    {
        if (_textPage == IntPtr.Zero || _selStart < 0 || _selEnd < 0) return;
        int s = Math.Min(_selStart, _selEnd), n = Math.Max(_selStart, _selEnd) - s + 1;
        byte[] buf = new byte[(n + 1) * 2];
        if (PdfNative.FPDFText_GetText(_textPage, s, n, buf) > 0)
        {
            string txt = System.Text.Encoding.Unicode.GetString(buf).Replace("\0", "");
            if (!string.IsNullOrEmpty(txt)) Clipboard.SetText(txt);
        }
    }

    public void ApplyTheme()
    {
        _scroll.BackColor = T.Canvas;
        _scroll.Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Right)           { NextPage(); return true; }
        if (keyData == Keys.Left)            { PrevPage(); return true; }
        if (keyData == Keys.Down)            { DoScroll(0,  120); return true; }
        if (keyData == Keys.Up)              { DoScroll(0, -120); return true; }
        if (keyData == (Keys.Control|Keys.C)){ CopySelection(); return true; }
        if (msg.Msg == 0x20A && (Control.ModifierKeys & Keys.Control) != 0)
        {
            int delta = (int)((long)msg.WParam >> 16);
            if (delta > 0) SetZoom(_zoom + 0.2f); else SetZoom(_zoom - 0.2f);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { if (_resizeTimer != null) _resizeTimer.Dispose(); CloseFile(); }
        base.Dispose(disposing);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    string AskPassword()
    {
        using (var f = new Form { Text = "Password Required", ClientSize = new Size(420, 160), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, MinimizeBox = false, AutoScaleMode = AutoScaleMode.None })
        {
            var lbl = new Label { Text = "This PDF is password protected:", Left = 20, Top = 20, Width = 380, Height = 30 };
            var tb  = new TextBox { Left = 20, Top = 60, Width = 380, Height = 24, UseSystemPasswordChar = true };
            var ok  = new Button { Text = "OK",     Left = 205, Top = 112, Width = 90, Height = 32, DialogResult = DialogResult.OK };
            var cn  = new Button { Text = "Cancel", Left = 305, Top = 112, Width = 95, Height = 32, DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { lbl, tb, ok, cn });
            f.AcceptButton = ok; f.CancelButton = cn;
            return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
        }
    }

    static void Msg(string s)  { MessageBox.Show(s, "PDF Reader", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    static void Fire(EventHandler h) { if (h != null) h(null, EventArgs.Empty); }
}

// ════════════════════════════════════════════════════════════════════════════════
//  Main form
// ════════════════════════════════════════════════════════════════════════════════
public class MinimalPdfReader : Form
{
    ChromeTabs _tabs;
    Panel      _shell;
    Panel      _toolbar;
    Panel      _statusBar;
    FloatPill  _zoomPill;
    FlatBtn    _btnOpen, _btnPrev, _btnNext, _btnZoomIn, _btnZoomOut, _btnFit;
    ThemeSwitch _themeSwitch;
    Label      _lblPage, _lblZoom;
    Label      _statLeft, _statRight;
    Panel _centerFlow;
    ToolTip _tips;

    public MinimalPdfReader()
    {
        Text = "PDF Reader";
        Size = new Size(1100, 750);
        MinimumSize = new Size(600, 400);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        BackColor = T.Bg;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        try { PdfNative.FPDF_InitLibrary(); }
        catch { MessageBox.Show("Could not initialise pdfium.dll.\nPlace pdfium.dll next to the exe or use the standalone build.", "PDF Reader", MessageBoxButtons.OK, MessageBoxIcon.Error); }

        BuildUI();
        WireEvents();
    }

    // Native title bar (default min/maximize/close) — just tell it to render dark to match the app.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme();
    }

    void ApplyTitleBarTheme()
    {
        if (!IsHandleCreated) return;
        int dark = T.Dark ? 1 : 0;
        try { PdfNative.DwmSetWindowAttribute(Handle, PdfNative.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)); } catch { }
    }

    // ── UI construction ──────────────────────────────────────────────────────
    void BuildUI()
    {
        // Outer margin frame: header-tier background showing around a lighter, rounded
        // content card (toolbar/tabs/canvas/status bar) — matches the design file's
        // two-layer structure (dark chrome frame + inset rounded content panel).
        _shell = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Padding = new Padding(10) };

        // ── Status bar ───────────────────────────────────────────────────────
        _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = T.Bg };
        _statusBar.Paint += (s, e) => {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = T.BottomRoundRect(new Rectangle(0, 0, _statusBar.Width, _statusBar.Height), 10))
                using (var b = new SolidBrush(T.StatBg)) g.FillPath(b, path);
            using (var p = new Pen(T.Sep)) g.DrawLine(p, 0, 0, _statusBar.Width, 0);
        };
        _statLeft  = MkLabel("", DockStyle.Left,  400);
        _statRight = MkLabel("", DockStyle.Right, 260);
        _statLeft.ForeColor = _statRight.ForeColor = T.TxtDim;
        _statLeft.Font = _statRight.Font = T.UiFont;
        _statLeft.TextAlign = ContentAlignment.MiddleLeft;
        _statRight.TextAlign = ContentAlignment.MiddleRight;
        _statusBar.Controls.Add(_statLeft);
        _statusBar.Controls.Add(_statRight);
        _shell.Controls.Add(_statusBar);

        // ── Toolbar (brand mark, Open, page nav, theme toggle) ──────────────────
        const int BRAND_ICON = 22, BRAND_MARGIN = 16;
        _toolbar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = T.Bg };
        _toolbar.Paint += (s, e) => {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = T.TopRoundRect(new Rectangle(0, 0, _toolbar.Width, _toolbar.Height), 10))
                using (var b = new SolidBrush(T.Bar)) g.FillPath(b, path);
            using (var p = new Pen(T.Sep)) g.DrawLine(p, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1);
            Ico.Dashboard(g, new Rectangle(BRAND_MARGIN, (_toolbar.Height - BRAND_ICON) / 2, BRAND_ICON, BRAND_ICON), T.Txt);
            DrawTracked(g, Text.ToUpperInvariant(), T.UiFont,
                new Rectangle(BRAND_MARGIN + BRAND_ICON + 10, 0, 300, _toolbar.Height), T.Txt, 2);
        };

        const int BTN_H = 40;
        const int GAP   = 6;

        _btnPrev   = new FlatBtn("",     40, BTN_H) { Icon = (g, r, c) => Ico.Chevron(g, r, c, true) };
        _btnNext   = new FlatBtn("",     40, BTN_H) { Icon = (g, r, c) => Ico.Chevron(g, r, c, false) };
        _btnZoomOut= new FlatBtn("",     40, BTN_H) { Icon = (g, r, c) => Ico.PlusMinus(g, r, c, false) };
        _btnZoomIn = new FlatBtn("",     40, BTN_H) { Icon = (g, r, c) => Ico.PlusMinus(g, r, c, true) };
        _btnFit    = new FlatBtn("Fit",  56, BTN_H);

        // Open is the primary action (filled pill, always visible — like a WinUI "standard" button).
        _btnOpen  = new FlatBtn("Open", 92, 36) { Icon = Ico.Folder, Filled = true };
        FitButton(_btnOpen);

        _themeSwitch = new ThemeSwitch { On = !T.Dark };
        _themeSwitch.Toggled += (s, e) => { T.Dark = !_themeSwitch.On; ApplyTheme(); };

        _tips = new ToolTip { AutomaticDelay = 400, ShowAlways = true };
        _tips.SetToolTip(_btnOpen,    "Open PDF (Ctrl+O)");
        _tips.SetToolTip(_themeSwitch,"Toggle dark / light theme");
        _tips.SetToolTip(_btnPrev,    "Previous page (←)");
        _tips.SetToolTip(_btnNext,    "Next page (→)");
        _tips.SetToolTip(_btnZoomOut, "Zoom out (Ctrl+Scroll)");
        _tips.SetToolTip(_btnZoomIn,  "Zoom in (Ctrl+Scroll)");
        _tips.SetToolTip(_btnFit,     "Fit to width");

        _lblPage   = new Label { Text = "—",    Size = new Size(100, BTN_H), TextAlign = ContentAlignment.MiddleCenter, ForeColor = T.Txt, Font = T.UiBig, BackColor = Color.Transparent };
        _lblZoom   = new Label { Text = "100%", Size = new Size( 68, BTN_H), TextAlign = ContentAlignment.MiddleCenter, ForeColor = T.Txt, Font = T.UiBig, BackColor = Color.Transparent };

        // Page-nav strip stays in the toolbar; zoom/fit are grouped into the floating pill below.
        Control[] strip = { _btnPrev, _lblPage, _btnNext };
        int stripW = 0;
        foreach (Control c in strip) stripW += c.Width + GAP;
        stripW -= GAP;

        _centerFlow = new Panel { Size = new Size(stripW, BTN_H), BackColor = Color.Transparent };
        int cx = 0;
        foreach (Control c in strip)
        {
            c.Location = new Point(cx, 0);
            _centerFlow.Controls.Add(c);
            cx += c.Width + GAP;
        }

        // Measure the brand text once (static string) so Open can be placed right after it.
        int brandTextW;
        using (var mg = CreateGraphics())
            brandTextW = DrawTracked(mg, Text.ToUpperInvariant(), T.UiFont, new Rectangle(0, 0, 500, BTN_H), Color.Black, 2, false);
        int openLeft = BRAND_MARGIN + BRAND_ICON + 10 + brandTextW + 24;

        _toolbar.Controls.Add(_centerFlow);
        _toolbar.Controls.Add(_btnOpen);
        _toolbar.Controls.Add(_themeSwitch);
        LayoutToolbar(BTN_H, openLeft);
        _toolbar.Resize += (s, e) => LayoutToolbar(BTN_H, openLeft);

        _shell.Controls.Add(_toolbar);
        Controls.Add(_shell);

        // ── Tab control ──────────────────────────────────────────────────────
        _tabs = new ChromeTabs { Dock = DockStyle.Fill };
        _tabs.SelectedIndexChanged += (s, e) => { BindActive(); UpdateStatus(); };
        _shell.Controls.Add(_tabs);
        _tabs.BringToFront();

        // ── Floating zoom pill — groups zoom out/in and fit into one island over the page ──
        _zoomPill = new FloatPill();
        Control[] pillItems = { _btnZoomOut, _lblZoom, _btnZoomIn, _btnFit };
        int pillGap = 14, pad = 10;
        int pillContentW = 0;
        foreach (Control c in pillItems) pillContentW += c.Width;
        pillContentW += GAP * 2 + pillGap; // gaps: out-label, label-in are GAP; in-fit is the wider pillGap
        int pillBodyH = BTN_H + pad;
        _zoomPill.Size = new Size(pillContentW + pad * 2 + FloatPill.Shadow * 2, pillBodyH + FloatPill.Shadow * 2);
        int px = FloatPill.Shadow + pad, py = FloatPill.Shadow + (pillBodyH - BTN_H) / 2;
        for (int i = 0; i < pillItems.Length; i++)
        {
            pillItems[i].Location = new Point(px, py);
            _zoomPill.Controls.Add(pillItems[i]);
            px += pillItems[i].Width + (i == 2 ? pillGap : GAP);
        }
        _shell.Controls.Add(_zoomPill);
        _zoomPill.BringToFront();
        _tabs.Resize += (s, e) => LayoutZoomPill();
        LayoutZoomPill();

        UpdateToolbarEnabled();
    }

    void LayoutToolbar(int btnH, int openLeft)
    {
        int topV = (_toolbar.Height - btnH) / 2;
        _centerFlow.Location  = new Point((_toolbar.Width - _centerFlow.Width) / 2, topV);
        _btnOpen.Location     = new Point(openLeft, (_toolbar.Height - _btnOpen.Height) / 2);
        _themeSwitch.Location = new Point(_toolbar.Width - 16 - _themeSwitch.Width, (_toolbar.Height - _themeSwitch.Height) / 2);
    }

    void LayoutZoomPill()
    {
        if (_zoomPill == null || _tabs == null || _tabs.Width <= 0) return;
        _zoomPill.Location = new Point(_tabs.Left + (_tabs.Width - _zoomPill.Width) / 2, _tabs.Bottom - _zoomPill.Height - 12);
    }

    // Icon+label buttons must be measured, not guessed — a fixed pixel width truncates
    // as soon as the font renders wider than expected (DPI scaling, "Light" vs "Dark", etc).
    void FitButton(FlatBtn b)
    {
        if (string.IsNullOrEmpty(b.Text)) return;
        int textW = TextRenderer.MeasureText(b.Text, b.Font, new Size(int.MaxValue, b.Height), TextFormatFlags.SingleLine).Width;
        int iconZone = (b.Icon != null) ? 8 + 22 : 8;
        b.Width = iconZone + textW + 14;
    }

    // Manual letter-spacing for the brand title — TextRenderer has no tracking option.
    // Returns the total width drawn (or that would be drawn), for layout purposes.
    static int DrawTracked(Graphics g, string text, Font font, Rectangle rect, Color color, int trackingPx, bool draw = true)
    {
        int x = rect.X;
        foreach (char ch in text)
        {
            string s = ch.ToString();
            Size sz = TextRenderer.MeasureText(g, s, font, new Size(int.MaxValue, rect.Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            if (draw)
                TextRenderer.DrawText(g, s, font, new Rectangle(x, rect.Y, sz.Width + trackingPx, rect.Height), color,
                    TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            x += sz.Width + trackingPx;
        }
        return x - rect.X;
    }

    Label MkLabel(string text, DockStyle dock, int w)
    {
        return new Label { Text = text, Dock = dock, Width = w, BackColor = Color.Transparent, Font = T.UiFont };
    }

    void WireEvents()
    {
        _btnOpen.Click    += (s, e) => OpenDialog();
        _btnPrev.Click    += (s, e) => { var v = Active(); if (v != null) v.PrevPage(); };
        _btnNext.Click    += (s, e) => { var v = Active(); if (v != null) v.NextPage(); };
        _btnZoomIn.Click  += (s, e) => { var v = Active(); if (v != null) v.SetZoom(v.ZoomLevel + 0.2f); };
        _btnZoomOut.Click += (s, e) => { var v = Active(); if (v != null) v.SetZoom(v.ZoomLevel - 0.2f); };
        _btnFit.Click     += (s, e) => { var v = Active(); if (v != null) v.FitWidth(); };

        DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop  += (s, e) => {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null) foreach (var f in files) if (f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) AddTab(f);
        };
    }

    // ── Active viewer helpers ────────────────────────────────────────────────
    PdfViewer _boundViewer;

    PdfViewer Active()
    {
        if (_tabs.SelectedTab == null) return null;
        foreach (Control c in _tabs.SelectedTab.Controls)
        { PdfViewer pv = c as PdfViewer; if (pv != null) return pv; }
        return null;
    }

    void BindActive()
    {
        if (_boundViewer != null) _boundViewer.StateChanged -= OnViewerState;
        _boundViewer = Active();
        if (_boundViewer != null) _boundViewer.StateChanged += OnViewerState;
        UpdateToolbarEnabled();
        UpdateStatus();
    }

    void OnViewerState(object s, EventArgs e) { UpdateToolbarEnabled(); UpdateStatus(); }

    void UpdateToolbarEnabled()
    {
        var v = Active();
        bool has = v != null && v.HasDoc;
        _btnPrev.Enabled = has && v.CurrentPage > 0;
        _btnNext.Enabled = has && v.CurrentPage < v.PageCount - 1;
        _btnZoomIn.Enabled = _btnZoomOut.Enabled = _btnFit.Enabled = has;
        _lblPage.Text = has ? string.Format("{0} / {1}", v.CurrentPage + 1, v.PageCount) : "—";
        _lblZoom.Text = has ? string.Format("{0}%", (int)(v.ZoomLevel * 100)) : "—";
        _lblPage.ForeColor = _lblZoom.ForeColor = T.Txt;
    }

    void UpdateStatus()
    {
        var v = Active();
        if (v == null || !v.HasDoc) { _statLeft.Text = ""; _statRight.Text = ""; return; }
        string name = _tabs.SelectedTab != null ? _tabs.SelectedTab.Text : "";
        _statLeft.Text  = "  " + name;
        _statRight.Text = string.Format("Page {0} / {1}   Zoom {2}%  ", v.CurrentPage + 1, v.PageCount, (int)(v.ZoomLevel * 100));
    }

    // ── Open / tabs ──────────────────────────────────────────────────────────
    void OpenDialog()
    {
        using (var ofd = new OpenFileDialog { Filter = "PDF Files|*.pdf", Multiselect = true })
            if (ofd.ShowDialog() == DialogResult.OK)
                foreach (var f in ofd.FileNames) AddTab(f);
    }

    void AddTab(string path)
    {
        var page   = new TabPage(Path.GetFileName(path)) { Padding = new Padding(0), BackColor = T.Bg };
        var viewer = new PdfViewer();
        viewer.StateChanged += (s, e) => { UpdateToolbarEnabled(); UpdateStatus(); };
        viewer.LoadFailed   += (s, e) => { _tabs.TabPages.Remove(page); viewer.Dispose(); };
        page.Controls.Add(viewer);
        _tabs.TabPages.Add(page);
        _tabs.SelectedTab = page;
        BindActive();
        viewer.LoadFile(path);
        if (_tabs.TabPages.Contains(page)) viewer.Focus();
    }

    // ── Theme ────────────────────────────────────────────────────────────────
    void ApplyTheme()
    {
        BackColor = T.Bg;
        ApplyTitleBarTheme();
        _themeSwitch.On = !T.Dark;
        _themeSwitch.Invalidate();
        _shell.BackColor = T.Bg;
        _shell.Invalidate();
        _toolbar.BackColor = T.Bg;
        _toolbar.Invalidate();
        _statusBar.BackColor = T.Bg;
        _statLeft.ForeColor = _statRight.ForeColor = T.TxtDim;
        _statusBar.Invalidate();
        _tabs.ApplyTheme();
        _tabs.BackColor = T.TabBg;
        _zoomPill.Invalidate();

        foreach (Control c in _toolbar.Controls)
        {
            { FlatBtn b = c as FlatBtn; if (b != null) b.Refresh2(); }
            { Label l = c as Label; if (l != null) { l.BackColor = Color.Transparent; l.ForeColor = T.Txt; } }
            Panel pnl = c as Panel;
            if (pnl != null)
            {
                pnl.BackColor = Color.Transparent;
                foreach (Control cc in pnl.Controls)
                {
                    { FlatBtn b2 = cc as FlatBtn; if (b2 != null) b2.Refresh2(); }
                    { Label l2 = cc as Label; if (l2 != null) l2.ForeColor = T.Txt; }
                }
            }
        }

        foreach (Control c in _zoomPill.Controls)
        {
            { FlatBtn b = c as FlatBtn; if (b != null) b.Refresh2(); }
            { Label l = c as Label; if (l != null) { l.BackColor = Color.Transparent; l.ForeColor = T.Txt; } }
        }

        _lblPage.ForeColor = _lblZoom.ForeColor = T.Txt;

        foreach (TabPage p in _tabs.TabPages)
        {
            p.BackColor = T.Bg;
            foreach (Control c in p.Controls)
            { PdfViewer pv = c as PdfViewer; if (pv != null) pv.ApplyTheme(); }
        }

        UpdateToolbarEnabled();
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O)) { OpenDialog(); return true; }
        if (keyData == (Keys.Control | Keys.W)) { if (_tabs.SelectedIndex >= 0) _tabs.RequestClose(_tabs.SelectedIndex); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ── Shutdown ─────────────────────────────────────────────────────────────
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (TabPage p in _tabs.TabPages)
            foreach (Control c in p.Controls)
            { PdfViewer pv = c as PdfViewer; if (pv != null) pv.Dispose(); }
        PdfNative.FPDF_DestroyLibrary();
        base.OnFormClosed(e);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Entry point
    // ════════════════════════════════════════════════════════════════════════
    [STAThread]
    static void Main()
    {
        ExtractPdfium();
        if (Environment.OSVersion.Version.Major >= 6) PdfNative.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MinimalPdfReader());
    }

    // Extract embedded pdfium.dll (standalone build only).
    // Falls back silently if the resource is not embedded (normal build).
    static void ExtractPdfium()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream("pdfium.dll"))
            {
                if (stream == null) return; // not embedded — external dll in use
                string dir  = Path.Combine(Path.GetTempPath(), "PdfReader_libs");
                string dest = Path.Combine(dir, "pdfium.dll");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(dest))
                {
                    using (var fs = File.Create(dest)) stream.CopyTo(fs);
                }
                PdfNative.LoadLibraryW(dest); // pre-load so DllImport finds it
            }
        }
        catch { }
    }
}
