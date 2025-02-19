using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Timer = System.Timers.Timer;

namespace Tomato;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private NotifyIcon? _icon;

    private TimeSpan _ctrDwnInterval = TimeSpan.Zero;
    private Timer? _timerCtrDwn;

    private Timer? _offReminderTimer;

    private Timer? _rePopTimer;

    private const string TomatoArgKey = "action";
    private const string AnotherTomatoValue = "start";
    private const string DelayValue = "delay";
    private const string ShowTomatoValue = "show";

    private const int DelayInterval = 10;

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

        Reset();

        InitializeIcon();

        AddEvents();

        Start();

        StartOffTimer();
    }

    private void InitializeIcon()
    {
        _icon = new NotifyIcon();

        var uri = new Uri("pack://application:,,,/tomato.ico", UriKind.Absolute);
        var rs = Application.GetResourceStream(uri);
        if (rs != null)
        {
            _icon.Icon = new Icon(rs.Stream);
            _icon.Visible = true;
        }

        var contextMenu = new ContextMenuStrip();

        var showMenu = new ToolStripMenuItem("Show");
        showMenu.Click += delegate { WindowState = WindowState.Normal; };
        contextMenu.Items.Add(showMenu);

        var resetMenu = new ToolStripMenuItem("Reset");
        resetMenu.Click += delegate
        {
            var r = MessageBox.Show("Reset timer now?", "Wait", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r is MessageBoxResult.Yes) Reset();
        };
        contextMenu.Items.Add(resetMenu);

        var exitMenu = new ToolStripMenuItem("Exit");
        exitMenu.Click += delegate { Close(); };
        contextMenu.Items.Add(exitMenu);

        _icon.ContextMenuStrip = contextMenu;

        _icon.DoubleClick += delegate
        {
            WindowState = WindowState.Normal;
        };

        _icon.MouseMove += delegate
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Next Tomato Clock in {GetCtrDwnString()}");
            sb.Append(FormatOff());
            _icon.Text = sb.ToString();
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                _icon.ContextMenuStrip.Show();
            }
        };
    }

    private static void AllowOnlyDigits(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Text = Regex.Replace(tb.Text, @"[^\d]", string.Empty);
            tb.CaretIndex = tb.Text.Length;
        }
    }

    private static void SetRange(object sender, string min, int max, string maxSubstitution)
    {
        if (sender is not TextBox tb) return;

        if (string.IsNullOrEmpty(tb.Text))
            tb.Text = min;

        var t = int.Parse(tb.Text);
        if (t >= max)
            tb.Text = maxSubstitution;
    }

    private static void PadZero(object sender)
    {
        if (sender is not TextBox tb) return;

        if (!string.IsNullOrEmpty(tb.Text))
            tb.Text = tb.Text.PadLeft(2, '0');
    }

    private void AddEvents()
    {
        IntervalBox.TextChanged += AllowOnlyDigits;
        IntervalBox.TextChanged += (o, _) => { SetRange(o, "45", 45, "45"); };
        IntervalBox.TextChanged += delegate
        {
            if (int.Parse(IntervalBox.Text) == 0)
                IntervalBox.Text = "45";
        };

        PadZero(HourBox);
        HourBox.TextChanged += AllowOnlyDigits;
        HourBox.TextChanged += (o, _) => { PadZero(o); };
        HourBox.TextChanged += (o, _) => { SetRange(o, "18", 24, "00"); };

        PadZero(MinuteBox);
        MinuteBox.TextChanged += AllowOnlyDigits;
        MinuteBox.TextChanged += (o, _) => { PadZero(o); }; 
        MinuteBox.TextChanged += (o, _) => { SetRange(o, "00", 60, "00"); };

        StateChanged += delegate
        {
            if (WindowState is WindowState.Minimized)
            {
                ShowInTaskbar = false;
                const string title = "Tomato Minimized";
                const string text = "Double click the tray icon to the config window.";
                _icon?.ShowBalloonTip(0, title, text, ToolTipIcon.Info);
            }
            else if (WindowState == WindowState.Normal)
            {
                ShowInTaskbar = true;
            }
        };

        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            var args = ToastArguments.Parse(toastArgs.Argument);

            if (args.TryGetValue(TomatoArgKey, out var value))
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    if (value is AnotherTomatoValue or DelayValue)
                    {
                        if (value is AnotherTomatoValue)
                            Reset();
                        else
                            _ctrDwnInterval += TimeSpan.FromMinutes(DelayInterval);

                        _timerCtrDwn?.Start();
                        ToastNotificationManagerCompat.History.RemoveGroup(TomatoArgKey);
                        _rePopTimer?.Stop();
                        _rePopTimer = null;
                    }
                    else if (value is ShowTomatoValue)
                    {
                        WindowState = WindowState.Normal;
                    }
                });
            }
        };

        TomatoNowButton.Click += delegate
        {
            _ctrDwnInterval = TimeSpan.Zero;
            CounterDown.Text = GetCtrDwnString();
            _timerCtrDwn?.Stop();

            DisplayTomatoNotification();

            _rePopTimer?.Stop();
            _rePopTimer = null;
        };

        ApplyButton.Click += delegate
        {
            StoreUserConfig();
            Reset();
        };

        Closing += (_, cancelEventArgs) =>
        {
            var r = MessageBox.Show("Close the tomato clock?", "Wait", MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (r == MessageBoxResult.No)
            {
                cancelEventArgs.Cancel = true;
            }
            else
            {
                StoreUserConfig();
                _icon?.Dispose();
                ToastNotificationManagerCompat.History.Clear();
            }
        };
    }

    private void Reset()
    {
        var cfg = TomatoConfig.Deserialize();

        DataContext = cfg;

        var dp = TextBox.TextProperty;
        IntervalBox.SetBinding(dp, new Binding("Interval") { Source = DataContext });
        HourBox.SetBinding(dp, new Binding("OffTimeHour") { Source = DataContext });
        MinuteBox.SetBinding(dp, new Binding("OffTimeMinute") { Source = DataContext });

        _ctrDwnInterval = TimeSpan.FromMinutes(cfg.Interval);
    }

    public static ToastContentBuilder GetToastContentBuilder(string title, string text)
    {
        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(text)
            .SetToastScenario(ToastScenario.Reminder)
            .SetBackgroundActivation();

        if (File.Exists(TomatoConfig.GetTomatoPicture()))
            builder.AddAppLogoOverride(new Uri("file:///" + TomatoConfig.GetTomatoPicture()),
                ToastGenericAppLogoCrop.Circle);

        return builder;
    }

    private void DisplayTomatoNotification()
    {
        const string title = "Tomato Clock Is Here.";
        const string text = "Get up to drink some water!";
        var msg = text + Environment.NewLine + FormatOff();

        GetToastContentBuilder(title, msg)
            .AddArgument(TomatoArgKey, AnotherTomatoValue)
            .AddButton(new ToastButton()
                .SetBackgroundActivation()
                .SetContent("Start Another Tomato")
                .AddArgument(TomatoArgKey, AnotherTomatoValue))
            .AddButton(new ToastButton()
                .SetBackgroundActivation()
                .SetContent($"Delay for {DelayInterval} minutes")
                .AddArgument(TomatoArgKey, DelayValue))
            .Show(toast =>
            {
                toast.Group = TomatoArgKey;
                toast.Dismissed += (_, dismissEventArgs) =>
                {
                    if (dismissEventArgs.Reason is ToastDismissalReason.UserCanceled)
                    {
                        Dispatcher.Invoke(delegate
                        {
                            Reset();
                            _timerCtrDwn?.Start();
                            ToastNotificationManagerCompat.History.RemoveGroup(TomatoArgKey);
                            _rePopTimer?.Stop();
                            _rePopTimer = null;
                        });
                    }
                };
            });
    }

    private bool _offReminderRaised;
    private void StartOffTimer()
    {
        var remindTs = TimeSpan.FromMinutes(5);
        _offReminderTimer = new Timer(TimeSpan.FromSeconds(30));
        _offReminderTimer.Elapsed += delegate
        {
            var ts = CalculateOff();

            if (ts < TimeSpan.Zero)
            {
                _offReminderRaised = false;
                return;
            }

            if (!_offReminderRaised)
            {
                if (ts < remindTs && ts > TimeSpan.Zero)
                {
                    GetToastContentBuilder("Time to prepare off!", "Go Go Go")
                        .AddArgument(TomatoArgKey, ShowTomatoValue)
                        .AddButton(new ToastButtonDismiss())
                        .Show();

                    _offReminderRaised = true;
                }
            }
        };

        _offReminderTimer.Start();
    }

    private void Start()
    {
        var ts = TimeSpan.FromSeconds(1);

        _timerCtrDwn = new Timer(ts);
        _timerCtrDwn.Elapsed += delegate
        {
            _ctrDwnInterval -= ts;
            Dispatcher.Invoke(delegate
            {
                try
                {
                    if (_ctrDwnInterval >= TimeSpan.Zero)
                        CounterDown.Text = GetCtrDwnString();
                    else
                    {
                        _timerCtrDwn.Stop();

                        DisplayTomatoNotification();

                        _rePopTimer = new Timer(TimeSpan.FromMinutes(2));
                        _rePopTimer.Elapsed += delegate
                        {
                            ToastNotificationManagerCompat.History.RemoveGroup(TomatoArgKey);
                            DisplayTomatoNotification();
                        };
                        _rePopTimer.Start();
                    }
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
            if (DataContext is TomatoConfig cfg)
                TomatoConfig.Serialize(cfg);
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetCtrDwnString()
    {
        return $"{_ctrDwnInterval.Hours:00}:{_ctrDwnInterval.Minutes:00}:{_ctrDwnInterval.Seconds:00}";
    }

    private static TimeSpan CalculateOff()
    {
        var cfg = TomatoConfig.Deserialize();
        var now = DateTime.Now;
        var off = new DateTime(now.Year, now.Month, now.Day, cfg.OffTimeHour, cfg.OffTimeMinute, 0);

        var timeSpan = off - now;

        return timeSpan;
    }

    private static string FormatOff()
    {
        var timeSpan = CalculateOff();

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