using System.Drawing.Drawing2D;

namespace EasyClipStash;

/// <summary>알림이 뜨는 화면 위치. 3x3 아홉 방향.</summary>
public enum ToastPosition
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,
}

public enum ToastKind { Info, Warning, Error }

/// <summary>
/// 앱이 직접 그리는 알림 창.
///
/// 윈도우 토스트는 시스템 전체에서 한 번에 하나만 표시되어, 다른 앱 알림이 떠 있으면
/// 우리 알림이 큐에서 몇 초씩 기다린다. 이 앱의 주 사용 흐름(캡처 → 저장)에서는
/// 캡처 도구가 항상 직전에 알림을 띄우므로 그 충돌이 기본 경로가 된다.
/// 그래서 OS 큐를 타지 않는 자체 창으로 즉시 보여준다.
/// </summary>
public sealed class ToastWindow : Form
{
    private const int ScreenEdgeGap = 24;   // 화면 가장자리와의 간격
    private const int Inset = 14;
    private const int IconSize = 32;
    private const int MaxWidth = 380;

    private static ToastWindow? _current;   // 동시에 하나만 띄운다

    private readonly System.Windows.Forms.Timer _life = new();
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 15 };
    private readonly Icon _icon;
    private readonly string _title;
    private readonly string _message;
    private readonly Action? _onClick;
    private bool _closing;

    /// <summary>알림을 띄운다. 이미 떠 있으면 즉시 교체한다.</summary>
    public static void Show(string title, string message, ToastKind kind,
                            ToastPosition position, int durationMs, Action? onClick = null)
    {
        _current?.Dismiss(immediate: true);
        _current = new ToastWindow(title, message, kind, durationMs, onClick);
        _current.ShowAt(position);
    }

    /// <summary>앱 종료 시 남은 알림을 정리한다.</summary>
    public static void CloseCurrent() => _current?.Dismiss(immediate: true);

    private ToastWindow(string title, string message, ToastKind kind, int durationMs, Action? onClick)
    {
        _title = title;
        _message = message;
        _onClick = onClick;
        _icon = kind switch
        {
            ToastKind.Warning => SystemIcons.Warning,
            ToastKind.Error => SystemIcons.Error,
            _ => TrayApplicationContext.LoadAppIcon(),
        };

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Background;
        Opacity = 0;
        Cursor = onClick is not null ? Cursors.Hand : Cursors.Default;
        DoubleBuffered = true;

        Size = Measure();

        _life.Interval = Math.Max(durationMs, 800);
        _life.Tick += (_, _) => { _life.Stop(); Dismiss(immediate: false); };

        Click += (_, _) => RunClickAction();
        Paint += OnPaint;
    }

    /// <summary>내용에 맞춰 창 크기를 정한다.</summary>
    private Size Measure()
    {
        using var titleFont = TitleFont();
        using var bodyFont = BodyFont();
        int textWidth = MaxWidth - Inset * 2 - IconSize - 12;

        var titleSize = TextRenderer.MeasureText(_title, titleFont, new Size(textWidth, 0), TextFormatFlags.WordBreak);
        var bodySize = TextRenderer.MeasureText(_message, bodyFont, new Size(textWidth, 0), TextFormatFlags.WordBreak);

        int height = Inset * 2 + titleSize.Height + 4 + bodySize.Height;
        return new Size(MaxWidth, Math.Max(height, Inset * 2 + IconSize));
    }

    private static Font TitleFont() => new(DefaultFont.FontFamily, DefaultFont.Size, FontStyle.Bold);
    private static Font BodyFont() => new(DefaultFont.FontFamily, DefaultFont.Size);

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var border = new Pen(Theme.Line))
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        g.DrawIcon(new Icon(_icon, IconSize, IconSize), new Rectangle(Inset, Inset, IconSize, IconSize));

        int textLeft = Inset + IconSize + 12;
        int textWidth = Width - textLeft - Inset;

        using var titleFont = TitleFont();
        using var bodyFont = BodyFont();
        var titleSize = TextRenderer.MeasureText(_title, titleFont, new Size(textWidth, 0), TextFormatFlags.WordBreak);

        TextRenderer.DrawText(g, _title, titleFont,
            new Rectangle(textLeft, Inset, textWidth, titleSize.Height),
            Theme.Accent, TextFormatFlags.WordBreak);

        TextRenderer.DrawText(g, _message, bodyFont,
            new Rectangle(textLeft, Inset + titleSize.Height + 4, textWidth, Height - Inset * 2),
            Theme.Muted, TextFormatFlags.WordBreak);
    }

    /// <summary>선택한 방향에 맞춰 위치를 잡고 서서히 나타난다.</summary>
    private void ShowAt(ToastPosition position)
    {
        // 마우스가 있는 화면에 띄운다. 다중 모니터에서 다른 화면에 떠 못 보는 일을 막는다.
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;

        // 괄호 필수: `a % 3 switch {...}` 는 `a % (3 switch {...})` 로 파싱된다.
        int column = (int)position % 3;   // 0=왼쪽 1=가운데 2=오른쪽
        int row = (int)position / 3;      // 0=위   1=중간   2=아래

        int x = column switch
        {
            0 => area.Left + ScreenEdgeGap,
            1 => area.Left + (area.Width - Width) / 2,
            _ => area.Right - Width - ScreenEdgeGap,
        };
        int y = row switch
        {
            0 => area.Top + ScreenEdgeGap,
            1 => area.Top + (area.Height - Height) / 2,
            _ => area.Bottom - Height - ScreenEdgeGap,
        };
        Location = new Point(x, y);

        _fade.Tick += (_, _) =>
        {
            double step = _closing ? -0.12 : 0.18;
            double next = Opacity + step;
            if (next >= 1 && !_closing) { Opacity = 1; _fade.Stop(); }
            else if (next <= 0 && _closing) { _fade.Stop(); CloseNow(); }
            else Opacity = Math.Clamp(next, 0, 1);
        };

        Show();
        _fade.Start();
        _life.Start();
    }

    private void RunClickAction()
    {
        var action = _onClick;
        Dismiss(immediate: true);
        try { action?.Invoke(); }
        catch { /* 폴더가 사라진 경우 등 — 알림 클릭이 앱을 죽이지 않게 한다 */ }
    }

    private void Dismiss(bool immediate)
    {
        if (_closing) return;
        _closing = true;
        _life.Stop();

        if (immediate) { _fade.Stop(); CloseNow(); }
        else _fade.Start();
    }

    private void CloseNow()
    {
        if (ReferenceEquals(_current, this)) _current = null;
        if (!IsDisposed) Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _life.Dispose(); _fade.Dispose(); }
        base.Dispose(disposing);
    }

    /// <summary>포커스를 빼앗지 않는다. 타이핑 중에 떠도 방해가 없어야 한다.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;   // 클릭 전까지 활성화되지 않음
            const int WS_EX_TOOLWINDOW = 0x00000080;   // Alt+Tab 목록에 뜨지 않음
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
