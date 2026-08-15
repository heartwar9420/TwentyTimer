using System.Drawing;
using System.Windows.Forms;

namespace TwentyTimer;

/// <summary>設定視窗。對應 mac 版 SettingsView.swift。</summary>
sealed class SettingsForm : Form
{
    private const int FormWidth = 460;
    private const int LabelWidth = 190;
    private const int FieldX = LabelWidth + 24;
    private const int FieldWidth = FormWidth - FieldX - 24;

    private readonly AppSettings _settings;
    private readonly Action _onDurationChange;
    private readonly Action _onResetPanelPosition;

    private int _y = 16;

    public SettingsForm(AppSettings settings, Action onDurationChange, Action onResetPanelPosition)
    {
        _settings = settings;
        _onDurationChange = onDurationChange;
        _onResetPanelPosition = onResetPanelPosition;

        Text = "TwentyTimer 設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(FormWidth, 200);
        AutoScroll = true;
        Font = new Font("Segoe UI", 9f);

        BuildTimeSection();
        BuildSoundSection();
        BuildDndSection();
        BuildAppearanceSection();

        ClientSize = new Size(FormWidth, Math.Min(_y + 16, 760));
    }

    // MARK: - 時間

    private void BuildTimeSection()
    {
        SectionHeader("時間");

        var work = Stepper(1, 120, _settings.WorkMinutes, v =>
        {
            _settings.WorkMinutes = v;
            _onDurationChange();
        });
        Row("工作時間", work, () => $"{_settings.WorkMinutes} 分鐘");

        var rest = Stepper(5, 300, _settings.RestSeconds, v =>
        {
            _settings.RestSeconds = v;
            _onDurationChange();
        }, increment: 5);
        Row("休息時間", rest, () => $"{_settings.RestSeconds} 秒");

        var idle = Stepper(30, 900, _settings.IdlePauseSeconds, v => _settings.IdlePauseSeconds = v, increment: 30);
        Row("閒置多久自動暫停", idle, () => $"{_settings.IdlePauseSeconds / 60} 分 {_settings.IdlePauseSeconds % 60} 秒");

        Note("閒置偵測使用 Windows 原生 API（GetLastInputInfo），不需要任何權限。");
    }

    // MARK: - 音效

    private void BuildSoundSection()
    {
        SectionHeader("音效");

        Toggle("休息開始時提示", _settings.SoundOnRestStart, v => _settings.SoundOnRestStart = v);
        Toggle("休息結束時提示", _settings.SoundOnRestEnd, v => _settings.SoundOnRestEnd = v);

        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(SoundPlayer.AvailableNames
            .Select(n => SoundPlayer.LoudnessLabel.TryGetValue(n, out var hint) ? $"{n}（{hint}）" : n)
            .Cast<object>().ToArray());
        var idx = Array.IndexOf(SoundPlayer.AvailableNames, _settings.SoundName);
        combo.SelectedIndex = idx >= 0 ? idx : 0;
        combo.SetBounds(FieldX, _y, FieldWidth - 70, 24);
        combo.SelectedIndexChanged += (_, _) =>
        {
            _settings.SoundName = SoundPlayer.AvailableNames[combo.SelectedIndex];
            PreviewSound();
        };
        var previewBtn = new Button { Text = "試聽" };
        previewBtn.SetBounds(FieldX + FieldWidth - 60, _y, 60, 24);
        previewBtn.Click += (_, _) => PreviewSound();
        Controls.Add(Labeled("提示音"));
        Controls.Add(combo);
        Controls.Add(previewBtn);
        _y += 32;

        Note("清單由響到小排序（未實測，僅供參考）。一邊聽音樂一邊工作的話，選最上面幾個比較不會被蓋過。");

        var gainLabel = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        gainLabel.SetBounds(FieldX + FieldWidth - 44, _y, 44, 24);
        var gainSlider = new TrackBar
        {
            Minimum = 30,
            Maximum = 300,
            TickFrequency = 10,
            Value = Math.Clamp((int)(_settings.SoundGain * 100), 30, 300),
        };
        gainSlider.SetBounds(FieldX, _y, FieldWidth - 50, 30);
        gainLabel.Text = $"{gainSlider.Value}%";
        gainSlider.Scroll += (_, _) => gainLabel.Text = $"{gainSlider.Value}%";
        gainSlider.MouseUp += (_, _) =>
        {
            _settings.SoundGain = gainSlider.Value / 100.0;
            PreviewSound();
        };
        Controls.Add(Labeled("音量"));
        Controls.Add(gainSlider);
        Controls.Add(gainLabel);
        _y += 34;
        Note("超過 100% 會放大到原始音量之上，用來蓋過背景音樂。放開滑桿即試聽。");

        var repeatStepper = Stepper(1, 5, _settings.SoundRepeat, v => _settings.SoundRepeat = v);
        Row("重複次數", repeatStepper, () => $"{_settings.SoundRepeat} 次");

        Note("在辦公室可以把兩個提示都關掉，純靠彈窗。");
    }

    private void PreviewSound() =>
        SoundPlayer.Play(_settings.SoundName, (float)_settings.SoundGain, _settings.SoundRepeat);

    // MARK: - 勿擾

    private void BuildDndSection()
    {
        SectionHeader("勿擾");
        Toggle("偵測到麥克風使用中時延後提醒", _settings.DeferWhenMicInUse, v => _settings.DeferWhenMicInUse = v);
        Note("有 App 正在讀取麥克風時（開會、通話）會先不跳彈窗，等結束後才提醒。");
    }

    // MARK: - 外觀與啟動

    private void BuildAppearanceSection()
    {
        SectionHeader("外觀與啟動");

        Toggle("在系統匣顯示倒數時間（滑鼠停留提示）", _settings.ShowTimeInMenuBar, v => _settings.ShowTimeInMenuBar = v);

        CheckBox restoreCursorBox = null!;
        Toggle("休息結束時自動把滑鼠移到「繼續工作」上", _settings.MoveMouseToButton, v =>
        {
            _settings.MoveMouseToButton = v;
            restoreCursorBox.Enabled = v;
        });
        Note("回座後不用找按鈕，直接按左鍵即可。若正在拖曳東西會自動跳過，不會搶走游標。");

        restoreCursorBox = Toggle("按下「繼續工作」後把游標移回原本的位置",
            _settings.RestoreCursorAfterClick, v => _settings.RestoreCursorAfterClick = v);
        restoreCursorBox.Enabled = _settings.MoveMouseToButton;
        Note("若你在按鈕出現後自己把游標移開了，就不會再搬動它。");

        Toggle("登入時自動啟動", _settings.LaunchAtLogin, v => _settings.LaunchAtLogin = v);

        var resetBtn = new Button { Text = "重設回右上角" };
        resetBtn.SetBounds(FieldX, _y, 120, 26);
        resetBtn.Click += (_, _) => _onResetPanelPosition();
        Controls.Add(Labeled("休息彈窗位置"));
        Controls.Add(resetBtn);
        _y += 34;
        Note("彈窗可以直接拖曳到你喜歡的位置，會自動記住。");
    }

    // MARK: - 版面小工具

    private void SectionHeader(string text)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
        };
        label.AutoSize = false;
        label.SetBounds(16, _y, FormWidth - 32, 22);
        Controls.Add(label);
        _y += 26;
    }

    private Label Labeled(string text)
    {
        var label = new Label { Text = text, AutoSize = false };
        label.SetBounds(16, _y + 3, LabelWidth, 20);
        return label;
    }

    private void Row(string label, Control field, Func<string> valueText)
    {
        var l = Labeled(label);
        field.SetBounds(FieldX, _y, 70, 24);
        var value = new Label { AutoSize = false, ForeColor = Color.Gray };
        value.SetBounds(FieldX + 78, _y + 3, FieldWidth - 78, 20);
        value.Text = valueText();

        if (field is NumericUpDown nud)
        {
            nud.ValueChanged += (_, _) => value.Text = valueText();
        }

        Controls.Add(l);
        Controls.Add(field);
        Controls.Add(value);
        _y += 30;
    }

    private NumericUpDown Stepper(int min, int max, int value, Action<int> onChange, int increment = 1)
    {
        var nud = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Increment = increment,
        };
        nud.ValueChanged += (_, _) => onChange((int)nud.Value);
        return nud;
    }

    private CheckBox Toggle(string text, bool value, Action<bool> onChange)
    {
        var box = new CheckBox { Text = text, Checked = value, AutoSize = false };
        box.SetBounds(16, _y, FormWidth - 32, 22);
        box.CheckedChanged += (_, _) => onChange(box.Checked);
        Controls.Add(box);
        _y += 26;
        return box;
    }

    private void Note(string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = false,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f),
        };
        label.SetBounds(16, _y, FormWidth - 32, 30);
        Controls.Add(label);
        _y += 32;
    }
}
