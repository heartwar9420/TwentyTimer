using System.Drawing;
using System.Drawing.Drawing2D;

namespace TwentyTimer;

/// <summary>
/// 系統匣圖示是動態畫出來的（16×16），依 TimerEngine 目前的狀態切換符號與顏色。
/// 對應 mac 版用 SF Symbol 切換 menuBarSymbol：Windows 系統匣不支援在圖示裡疊字，
/// 詳見 mac/SPEC.md 的平台對照表，這裡改用形狀+顏色代表狀態，倒數文字則放在滑鼠停留提示裡。
/// </summary>
static class TrayIconFactory
{
    /// <summary>簡短的狀態符號，圖示與彈出面板共用。</summary>
    public static string Glyph(TimerEngine engine)
    {
        if (engine.IsManuallyPaused) return "⏸";
        if (engine.IsSnoozing) return "🌙";
        return engine.CurrentPhase switch
        {
            TimerEngine.Phase.Working => engine.IsIdlePaused ? "⏸" : "👁",
            TimerEngine.Phase.PendingBreak => "⏳",
            TimerEngine.Phase.Resting => "👀",
            TimerEngine.Phase.AwaitingContinue => "✅",
            _ => "👁",
        };
    }

    private static Color GlyphColor(TimerEngine engine)
    {
        if (engine.IsManuallyPaused || engine.IsIdlePaused) return Color.FromArgb(220, 38, 38);
        if (engine.IsSnoozing) return Color.MediumPurple;
        return engine.CurrentPhase switch
        {
            TimerEngine.Phase.Working => Color.FromArgb(0, 120, 212),
            TimerEngine.Phase.PendingBreak => Color.DarkOrange,
            TimerEngine.Phase.Resting => Color.FromArgb(0, 178, 148),
            TimerEngine.Phase.AwaitingContinue => Color.FromArgb(46, 160, 67),
            _ => Color.Gray,
        };
    }

    public static Icon Build(TimerEngine engine)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var color = GlyphColor(engine);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.FromArgb(160, 0, 0, 0), 1.5f);

            switch (engine.CurrentPhase)
            {
                case TimerEngine.Phase.Working when !engine.IsIdlePaused && !engine.IsManuallyPaused:
                    // 睜眼：一個圓
                    g.FillEllipse(brush, 6, 6, 20, 20);
                    break;
                case TimerEngine.Phase.Resting:
                    // 休息：圓環
                    g.DrawEllipse(new Pen(color, 4), 5, 5, 22, 22);
                    break;
                case TimerEngine.Phase.AwaitingContinue:
                    // 完成：勾勾
                    g.FillEllipse(brush, 4, 4, 24, 24);
                    using (var checkPen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLines(checkPen, new[] { new Point(9, 16), new Point(14, 21), new Point(23, 10) });
                    }
                    break;
                case TimerEngine.Phase.PendingBreak:
                    // 等待：半圓
                    g.FillPie(brush, 6, 6, 20, 20, 0, 180);
                    g.DrawEllipse(pen, 6, 6, 20, 20);
                    break;
                default:
                    // 暫停：紅燈（實心圓 + 深色外框，模擬警示燈，避免被忽略）
                    g.FillEllipse(brush, 6, 6, 20, 20);
                    g.DrawEllipse(pen, 6, 6, 20, 20);
                    break;
            }
        }
        var icon = Icon.FromHandle(bmp.GetHicon());
        return icon;
    }
}
