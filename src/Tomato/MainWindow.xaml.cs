using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;

namespace Tomato
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon _icon;

        private readonly string[] _intervals = new [] { 5, 10, 15, 20, 25, 30, 35, 40, 45 }.Select(i => i.ToString()).ToArray();
        private readonly string[] _hours = Enumerable.Range(0, 24).Select(x => x.ToString("00")).ToArray();
        private readonly string[] _minutes = Enumerable.Range(0, 60).Select(x => x.ToString("00")).ToArray();

        private TomatoConfig _cfg;
        private TomatoConfig Cfg
        {
            get => _cfg;
            set
            {
                _cfg = value;
                _ctrDwnInterval = TimeSpan.FromMinutes(value.Interval);
            }
        }

        private Timer _timerTomato;

        private TimeSpan _ctrDwnInterval;
        private Timer _timerCtrDwn;

        public MainWindow()
        {
            var p = Process.GetProcessesByName("TomatoClock"); 
            if (p.Length == 2)
            {
                Application.Current.Shutdown();
                return;
            }

            InitializeComponent();

            TomatoConfig.Create();

            AddEvents();

            FetchUserConfig();

            Start();

            DisplayCtrDown();
        }

        private void Notify(string title, string text)
        {
            _icon?.ShowBalloonTip(0, title, text, ToolTipIcon.Info);
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
                    Notify("Tomato Minimized", "Click the tray icon to the config window.");
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
                FetchUserConfig();
                Start();
            };

            Closing += PreventClosing;
        }

        private void PreventClosing(object sender, CancelEventArgs e)
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
            IntervalBox.SelectedIndex = Cfg.Interval / 5 - 1;
            HourBox.SelectedIndex = Cfg.OffTimeHour;
            MinuteBox.SelectedIndex = Cfg.OffTimeMinute;

            DisplayTomatoNotification();

            _timerTomato?.Close();
            _timerTomato = new Timer(TimeSpan.FromMinutes(Cfg.Interval).TotalMilliseconds);
            _timerTomato.Elapsed += delegate
            {
                FetchUserConfig();
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
                    if (_ctrDwnInterval > TimeSpan.Zero)
                        CounterDown.Text = GetCtrDwnString();
                });
            };

            _timerCtrDwn.Start();
        }

        private void FetchUserConfig()
        {
            try
            {
                Cfg = TomatoConfig.Deserialize();
            }
            catch (Exception)
            {
                Cfg = new TomatoConfig();
                const string m = "Error loading user config. Use default settings instead.";
                MessageBox.Show(m, "Oops", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            Dispatcher.Invoke(delegate { Notify(title, msg); });
        }

        private string GetCtrDwnString()
        {
            return $"{_ctrDwnInterval.Hours:00}:{_ctrDwnInterval.Minutes:00}:{_ctrDwnInterval.Seconds:00}";
        }

        private string CalculateOff()
        {
            var now = DateTime.Now;
            var off = new DateTime(now.Year, now.Month, now.Day, Cfg.OffTimeHour, Cfg.OffTimeMinute, 0);

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
}