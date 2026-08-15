using System.Drawing;
using System.Windows.Forms;

namespace TwentyTimer;

/// <summary>統計視窗。對應 mac 版 StatsView.swift。</summary>
sealed class StatsForm : Form
{
    private readonly StatsStore _stats;
    private readonly Label _todayValue = new();
    private readonly Label _streakValue = new();
    private readonly Label _totalValue = new();
    private readonly ListView _list = new();

    public StatsForm(StatsStore stats)
    {
        _stats = stats;

        Text = "統計";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 440);
        Font = new Font("Segoe UI", 9f);

        var summaryPanel = new Panel();
        summaryPanel.SetBounds(0, 0, 360, 60);
        summaryPanel.Controls.Add(SummaryTile(_todayValue, "今日", 16));
        summaryPanel.Controls.Add(SummaryTile(_streakValue, "連續天數", 130));
        summaryPanel.Controls.Add(SummaryTile(_totalValue, "累計輪數", 244));
        Controls.Add(summaryPanel);

        _list.SetBounds(0, 64, 360, 320);
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.GridLines = false;
        _list.HeaderStyle = ColumnHeaderStyle.None;
        _list.Columns.Add("日期", 220);
        _list.Columns.Add("輪數", 100, HorizontalAlignment.Right);
        Controls.Add(_list);

        var clearBtn = new Button { Text = "清除全部紀錄" };
        clearBtn.SetBounds(360 - 16 - 120, 396, 120, 28);
        clearBtn.Click += (_, _) => ConfirmClear();
        Controls.Add(clearBtn);

        Refresh_();
        _stats.Changed += Refresh_;
        FormClosed += (_, _) => _stats.Changed -= Refresh_;
    }

    private static Control SummaryTile(Label valueLabel, string caption, int x)
    {
        var panel = new Panel();
        panel.SetBounds(x, 0, 100, 60);

        valueLabel.AutoSize = false;
        valueLabel.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        valueLabel.SetBounds(0, 0, 100, 30);

        var captionLabel = new Label { Text = caption, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };
        captionLabel.AutoSize = false;
        captionLabel.SetBounds(0, 32, 100, 18);

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(captionLabel);
        return panel;
    }

    private void Refresh_()
    {
        _todayValue.Text = _stats.TodayCount.ToString();
        _streakValue.Text = _stats.CurrentStreak.ToString();
        _totalValue.Text = _stats.Daily.Values.Sum().ToString();

        _list.Items.Clear();
        var entries = _stats.RecentEntries();
        if (entries.Count == 0)
        {
            var item = new ListViewItem(new[] { "目前沒有紀錄", "" });
            item.ForeColor = Color.Gray;
            _list.Items.Add(item);
            return;
        }
        foreach (var (day, count) in entries)
        {
            _list.Items.Add(new ListViewItem(new[] { day, $"{count} 輪" }));
        }
    }

    private void ConfirmClear()
    {
        var result = MessageBox.Show(this, "這個動作無法復原。確定要清除所有統計紀錄嗎？", "確認",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;
        _stats.ClearAll();
        Refresh_();
    }
}
