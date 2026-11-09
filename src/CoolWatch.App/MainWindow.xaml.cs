using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CoolWatch.Core;

namespace CoolWatch.App;

public partial class MainWindow : Window
{
    readonly HardwareMonitor? _hw;
    readonly AutoCooler _auto = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly HashSet<string> _touchedFans = new();
    string _fanKey = "";
    int _limit = CpuLimiter.Max;
    bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        try { _hw = new HardwareMonitor(); }
        catch (Exception ex) { Info.Text = "Brak dostępu do czujników: " + ex.Message; }
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    void Drag(object s, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not Slider) DragMove();
    }

    void Close_Click(object s, RoutedEventArgs e) => Close();

    void Tick()
    {
        if (_hw == null || _busy) return;
        _busy = true;
        try
        {
            var snap = _hw.Read();
            if (snap.CpuTempC is float t)
            {
                CpuTemp.Text = $"{t:0}°C";
                CpuTemp.Foreground = t >= 85 ? Brushes.OrangeRed : t >= 70 ? Brushes.Orange : Brushes.LightGreen;
                if (AutoBox.IsChecked == true && _auto.Evaluate(t, _limit) is int limit) SetLimit(limit, true);
            }
            else CpuTemp.Text = "--°C";

            Details.Text = $"Obciążenie CPU: {(snap.CpuLoad is float l ? $"{l:0}%" : "brak")}\n" +
                           $"Temperatura GPU: {(snap.GpuTempC is float g ? $"{g:0}°C" : "brak")}" +
                           (_auto.Active ? "\nAuto-chłodzenie: AKTYWNE" : "");
            UpdateFans(snap);
            Info.Text = snap.CpuTempC == null ? "Nie odczytano temperatury (czujnik niedostępny na tym sprzęcie)." : "";
        }
        catch (Exception ex) { Info.Text = "Błąd odczytu: " + ex.Message; }
        _busy = false;
    }

    void SetLimit(int percent, bool fromAuto)
    {
        _limit = CpuLimiter.Clamp(percent);
        LimitSlider.Value = _limit;
        if (!CpuLimiter.Apply(_limit)) Info.Text = "Nie udało się ustawić limitu procesora.";
        else if (fromAuto) Info.Text = $"Auto-chłodzenie: limit {_limit}%.";
    }

    readonly ProcessManager _procs = new();
    readonly DispatcherTimer _procTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    bool _procTimerHooked;

    void Proc_Expanded(object s, RoutedEventArgs e)
    {
        if (!_procTimerHooked) { _procTimer.Tick += (_, _) => RefreshProcs(); _procTimerHooked = true; }
        _procs.List(); // pierwszy odczyt ustawia punkt odniesienia dla CPU
        RefreshProcs();
        _procTimer.Start();
    }

    void Proc_Collapsed(object s, RoutedEventArgs e) => _procTimer.Stop();

    void ProcRefresh_Click(object s, RoutedEventArgs e) => RefreshProcs();

    void RefreshProcs()
    {
        ProcList.Children.Clear();
        foreach (var p in _procs.List().Take(25))
        {
            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var btn = new Button
            {
                Content = "Zakończ", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0),
                IsEnabled = !p.Protected, ToolTip = p.Protected ? "Proces systemowy – zablokowany" : "Zakończ proces",
            };
            DockPanel.SetDock(btn, Dock.Right);
            var info = p;
            btn.Click += (_, _) => KillProc(info);
            row.Children.Add(btn);
            row.Children.Add(new TextBlock
            {
                Text = $"{p.Name}  {p.CpuPercent:0}% CPU · {p.MemoryMb:0} MB",
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = p.Protected ? Brushes.Gray : Brushes.White,
            });
            ProcList.Children.Add(row);
        }
    }

    void KillProc(ProcInfo p)
    {
        var ask = MessageBox.Show(this,
            $"Zakończyć „{p.Name}”?\nNiezapisane dane w tym programie mogą zostać utracone.",
            "CoolWatch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ask != MessageBoxResult.Yes) return;
        Info.Text = ProcessManager.Kill(p.Id) ? $"Zakończono: {p.Name}." : $"Nie udało się zakończyć: {p.Name}.";
        RefreshProcs();
    }

    void Max_Changed(object s, RoutedEventArgs e)
    {
        if (_hw == null) return;
        bool on = MaxBox.IsChecked == true;
        bool policy = CpuLimiter.SetActiveCooling(on);
        int fans = _hw.SetAllFans(on);
        Info.Text = on
            ? $"MAX chłodzenie włączone. Polityka chłodzenia: {(policy ? "aktywna" : "błąd")}. " +
              (fans > 0 ? $"Wentylatory na 100%: {fans}." : "Ten laptop nie pozwala sterować wentylatorami – zadziałała tylko polityka systemu. Dla większego efektu obniż limit procesora.")
            : "MAX chłodzenie wyłączone.";
    }

    void Limit_Changed(object s, RoutedPropertyChangedEventArgs<double> e) =>
        LimitText.Text = $"{(int)LimitSlider.Value}%";

    void Limit_Released(object s, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        SetLimit((int)LimitSlider.Value, false);

    void UpdateFans(Snapshot snap)
    {
        var key = string.Join("|", snap.Fans.Select(f => f.Name + f.Controllable));
        if (key != _fanKey)
        {
            _fanKey = key;
            FanPanel.Children.Clear();
            foreach (var f in snap.Fans.Where(f => f.Controllable))
            {
                var name = f.Name;
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
                var auto = new Button { Content = "Auto", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0) };
                DockPanel.SetDock(auto, Dock.Right);
                var slider = new Slider { Minimum = 20, Maximum = 100, Value = 60, TickFrequency = 5, IsSnapToTickEnabled = true };
                slider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent, new System.Windows.Controls.Primitives.DragCompletedEventHandler((_, _) =>
                {
                    if (_hw!.SetFan(name, (int)slider.Value)) _touchedFans.Add(name);
                }));
                auto.Click += (_, _) => { _hw!.SetFan(name, null); _touchedFans.Remove(name); };
                row.Children.Add(auto);
                row.Children.Add(slider);
                FanPanel.Children.Add(new TextBlock { Text = name + " (obroty %)", FontSize = 11, Foreground = Brushes.LightGray });
                FanPanel.Children.Add(row);
            }
        }
        if (snap.Fans.Count > 0 && !snap.Fans.Any(f => f.Controllable))
            Info.Text += (Info.Text.Length > 0 ? "\n" : "") + "Wentylatorami nie da się tu sterować – ten laptop nie udostępnia regulacji.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _procTimer.Stop();
        if (_auto.Active) CpuLimiter.Apply(CpuLimiter.Max);
        if (MaxBox.IsChecked == true) { CpuLimiter.SetActiveCooling(false); _hw?.SetAllFans(false); }
        foreach (var f in _touchedFans) _hw?.SetFan(f, null);
        _hw?.Dispose();
        base.OnClosed(e);
    }
}
