using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;

namespace Tomato;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private NotifyIcon? _icon;

    private readonly string[] _intervals = new[] { 5, 10, 15, 20, 25, 30, 35, 40, 45 }.Select(i => i.ToString()).ToArray();
    private readonly string[] _hours = Enumerable.Range(0, 24).Select(x => x.ToString("00")).ToArray();
    private readonly string[] _minutes = Enumerable.Range(0, 60).Select(x => x.ToString("00")).ToArray();

    private Timer? _timerTomato;

    private TimeSpan _ctrDwnInterval = TimeSpan.Zero;
    private Timer? _timerCtrDwn;

    public MainWindow()
    {
        var p = Process.GetProcessesByName("Tomato");
        if (p.Length == 2)
        {
            Application.Current.Shutdown();
            return;
        }

        InitializeComponent();

        TomatoConfig.Create();

        AddEvents();

        Start();

        DisplayCtrDown();
    }

    private void AddEvents()
    {
        _icon = new NotifyIcon();
        var uri = new Uri("pack://application:,,,/tomato.ico", UriKind.Absolute);
        var rs = Application.GetResourceStream(uri);
        if (rs != null)
        {
            _icon.Icon = new Icon(rs.Stream);
            _icon.Visible = true;
        }

        _icon.DoubleClick += delegate
        {
            WindowState = WindowState.Normal;
        };

        _icon.MouseMove += delegate
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Next Tomato Clock in {GetCtrDwnString()}");
            sb.Append(CalculateOff());
            _icon.Text = sb.ToString();
        };

        StateChanged += delegate
        {
            if (WindowState is WindowState.Minimized)
            {
                ShowInTaskbar = false;
                const string title = "Tomato Minimized";
                const string text = "Click the tray icon to the config window.";
                _icon?.ShowBalloonTip(0, title, text, ToolTipIcon.Info);
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
            Start();
        };

        Closing += PreventClosing;
    }

    private void PreventClosing(object? sender, CancelEventArgs e)
    {
        var r = MessageBox.Show("Close the tomato clock?", "Wait", MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (r == MessageBoxResult.No)
        {
            e.Cancel = true;
        }
        else
        {
            StoreUserConfig();
            _icon?.Dispose();
        }
    }

    private void Start()
    {
        var cfg = TomatoConfig.Deserialize();
        IntervalBox.SelectedIndex = cfg.Interval / 5 - 1;
        HourBox.SelectedIndex = cfg.OffTimeHour;
        MinuteBox.SelectedIndex = cfg.OffTimeMinute;

        _ctrDwnInterval = TimeSpan.FromMinutes(cfg.Interval);
        DisplayTomatoNotification();

        _timerTomato?.Close();
        _timerTomato = new Timer(TimeSpan.FromMinutes(cfg.Interval));
        _timerTomato.Elapsed += delegate
        {
            _ctrDwnInterval = TimeSpan.FromMinutes(TomatoConfig.Deserialize().Interval);
            DisplayTomatoNotification();
        };

        _timerTomato.Start();
    }

    private void DisplayCtrDown()
    {
        var ts = TimeSpan.FromSeconds(1);
        _timerCtrDwn = new Timer(ts.TotalMilliseconds);

        _timerCtrDwn.Elapsed += delegate
        {
            _ctrDwnInterval -= ts;
            Dispatcher.Invoke(delegate
            {
                try
                {
                    if (_ctrDwnInterval > TimeSpan.Zero)
                        CounterDown.Text = GetCtrDwnString();
                }
                catch (Exception)
                {
                    // pass
                }
            });
        };

        _timerCtrDwn.Start();
    }

    private void StoreUserConfig()
    {
        try
        {
            var interval = int.Parse(IntervalBox.SelectedItem as string ?? throw new Exception("Interval parsing error."));
            var hour = int.Parse(HourBox.SelectedItem as string ?? throw new Exception("Hour parsing error."));
            var minute = int.Parse(MinuteBox.SelectedItem as string ?? throw new Exception("Minute parsing error."));

            var cfg = new TomatoConfig
            {
                Interval = interval,
                OffTimeHour = hour,
                OffTimeMinute = minute
            };

            TomatoConfig.Serialize(cfg);
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
        var logo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tomato_je.jpg");

        Dispatcher.Invoke(delegate
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(msg)
                .SetToastScenario(ToastScenario.Reminder)
                .AddAppLogoOverride(new Uri("file:///" + logo), ToastGenericAppLogoCrop.Circle)
                .AddButton(new ToastButtonDismiss())
                .Show();
        });
    }

    private string GetCtrDwnString()
    {
        return $"{_ctrDwnInterval.Hours:00}:{_ctrDwnInterval.Minutes:00}:{_ctrDwnInterval.Seconds:00}";
    }

    private static string CalculateOff()
    {
        var cfg = TomatoConfig.Deserialize();
        var now = DateTime.Now;
        var off = new DateTime(now.Year, now.Month, now.Day, cfg.OffTimeHour, cfg.OffTimeMinute, 0);

        var timeSpan = off - now;

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