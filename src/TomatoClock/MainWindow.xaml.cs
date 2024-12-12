using System.IO;
using System.Text.Json;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;

namespace TomatoClock;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private NotifyIcon? _icon;

    private readonly string[] _intervals = new [] { 15, 20, 25, 30, 35, 40, 45 }.Select(i => i.ToString()).ToArray();
    private readonly string[] _hours = Enumerable.Range(0, 24).Select(x => x.ToString("00")).ToArray();
    private readonly string[] _minutes = Enumerable.Range(0, 60).Select(x => x.ToString("00")).ToArray();

    private TomatoConfig _cfg;

    public MainWindow()
    {
        InitializeComponent();
        Init();
        AddEvents();

        _cfg = FetchUserConfig();

        Start();

        WindowState = WindowState.Minimized;
        ShowInTaskbar = false;
    }

    private static string GetUserConfigFile()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tomato.config.json");

    private static void Init()
    {
        var f = GetUserConfigFile();
        if (!File.Exists(f)) File.WriteAllText(f, JsonSerializer.Serialize(new TomatoConfig()));
    }

    private static void ShowBalloonTip(NotifyIcon? icon, string title, string text, int timeout)
    {
        if (icon is null) return;

        icon.BalloonTipTitle = title;
        icon.BalloonTipText = text;
        icon.ShowBalloonTip(timeout);
    }

    private void AddEvents()
    {
        _icon = new NotifyIcon();
        var uri = new Uri("pack://application:,,,/tomato.ico", UriKind.Absolute);
        var rs = Application.GetResourceStream(uri);
        if (rs is not null)
        {
            _icon.Icon = new Icon(rs.Stream);
            _icon.Visible = true;
        }

        _icon.Click += delegate { WindowState = WindowState.Normal; };
        _icon.DoubleClick += delegate { WindowState = WindowState.Normal; };

        StateChanged += delegate
        {
            if (WindowState is WindowState.Minimized)
            {
                ShowInTaskbar = false;
                ShowBalloonTip(_icon, "Tomato Minimized", "Click the tray icon to return.", 300);
            }
            else if (WindowState == WindowState.Normal)
            {
                ShowInTaskbar = true;
            }
        };

        IntervalBox.ItemsSource = _intervals;
        HourBox.ItemsSource = _hours;
        MinuteBox.ItemsSource = _minutes;

        ApplyButton.Click += delegate
        {
            StoreUserConfig();
            _cfg = FetchUserConfig();
            Start();
        };

        Closing += (_, e) =>
        {
            var r = MessageBox.Show("Close the tomato clock?", "Wait", MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (r == MessageBoxResult.No)
                e.Cancel = true;
        };

        Closed += delegate
        {
            StoreUserConfig();
        };
    }

    private Timer? _timer;
    private Timer? _timerCtrDwn;
    private TimeSpan _ctrDwnInterval;

    private void Start()
    {
        IntervalBox.SelectedIndex = (_cfg.Interval - 15) / 5;
        HourBox.SelectedIndex = _cfg.OffTimeHour;
        MinuteBox.SelectedIndex = _cfg.OffTimeMinute;

        DisplayTomatoNotification();

        _ctrDwnInterval = TimeSpan.FromMinutes(_cfg.Interval);
        DisplayCtrDown();

        _timer?.Close();
        _timer = new Timer(TimeSpan.FromMinutes(_cfg.Interval));
        _timer.Elapsed += delegate
        {
            DisplayTomatoNotification();
            _ctrDwnInterval = TimeSpan.FromMinutes(_cfg.Interval);
        };

        _timerCtrDwn?.Close();
        _timerCtrDwn = new Timer(TimeSpan.FromSeconds(1));
        _timerCtrDwn.Elapsed += delegate
        {
            _ctrDwnInterval -= TimeSpan.FromSeconds(1);
            DisplayCtrDown();
        };

        _timer.Start();
        _timerCtrDwn.Start();
    }

    private void DisplayCtrDown()
    {
        Dispatcher.Invoke(() =>
        {
            if (_ctrDwnInterval > TimeSpan.Zero)
            {
                CounterDown.Text = $"{_ctrDwnInterval.Hours:00}:{_ctrDwnInterval.Minutes:00}:{_ctrDwnInterval.Seconds:00}";
            }
        });
    }

    private static TomatoConfig FetchUserConfig()
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<TomatoConfig>(File.ReadAllText(GetUserConfigFile()))
                      ?? new TomatoConfig();

            return cfg;
        }
        catch (Exception)
        {
            return new TomatoConfig();
        }
    }

    private void StoreUserConfig()
    {
        try
        {
            var interval = int.Parse(IntervalBox.SelectedItem as string ?? throw new Exception("Interval parsing error."));
            var hour = int.Parse(HourBox.SelectedItem as string ?? throw new Exception("Hour parsing error."));
            var minute = int.Parse(MinuteBox.SelectedItem as string ?? throw new Exception("Minute parsing error."));

            var json = new TomatoConfig
            {
                Interval = interval,
                OffTimeHour = hour,
                OffTimeMinute = minute
            };

            File.WriteAllText(GetUserConfigFile(), JsonSerializer.Serialize(json));
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisplayTomatoNotification()
    {
        const string title = "Tomato Clock Is Here.";
        const string text = "Get up to drink some water!";
        var msg = text + Environment.NewLine + CalculateOff();

        Dispatcher.Invoke(delegate
        {
            ShowBalloonTip(_icon, title, msg, 1500);
        });
    }

    private string CalculateOff()
    {
        var now = DateTime.Now;
        var off = new DateTime(now.Year, now.Month, now.Day, _cfg.OffTimeHour, _cfg.OffTimeMinute, 0);

        var timeSpan = (off - now);

        if (timeSpan <= TimeSpan.Zero) return "OFF NOW.";

        var totalHours = timeSpan.TotalHours;
        var hours = (int)Math.Truncate(totalHours);
        var minutes = (int)Math.Truncate(timeSpan.TotalMinutes - hours * 60);

        var pm = minutes > 1 ? "minutes" : "minute";

        if (hours == 0)
            return $"{minutes} {pm} to OFF.";

        var ph = hours > 1 ? "hours" : "hour";
        return $"{hours} {ph} {minutes} {pm} to OFF.";
    }
}