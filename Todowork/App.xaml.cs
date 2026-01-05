using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Runtime.ExceptionServices;
using System.Windows.Media;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Todowork.Services;
using Todowork.ViewModels;

namespace Todowork
{
    public partial class App : Application
    {
        private const string InstanceId = "9D64E7A8-0E7F-4D21-B0D6-7A8C2B562E56";
        private static readonly string LogMutexName = "Local\\Todowork_LogMutex_" + InstanceId;
        private static readonly string InstanceMutexName = "Local\\Todowork_SingleInstance_" + InstanceId;
        private static readonly string ShowMainEventName = "Local\\Todowork_ShowMain_" + InstanceId;

        private static readonly Mutex LogMutex = new Mutex(false, LogMutexName);
        private Mutex _mutex;
        private EventWaitHandle _showMainEvent;
        private Thread _showMainWaitThread;
        private TrayService _trayService;
        private TodoStore _store;
        private MainWindow _mainWindow;
        private OverlayWindow _overlayWindow;
        private bool _overlayShouldShow = true;
        private bool _isExiting;
        private bool _startupInvoked;

        public App()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += (s, e) =>
            {
                try { LogException(e.Exception, "DispatcherUnhandledException"); } catch { }
                try
                {
                    MessageBox.Show(
                        "程序发生未处理异常，已记录到 %AppData%\\Todowork\\crash.log。\r\n\r\n" + e.Exception?.Message,
                        "Todowork",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch { }

                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { LogException(e.ExceptionObject as Exception, "UnhandledException"); } catch { }
            };
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (_startupInvoked)
            {
                return;
            }
            _startupInvoked = true;

            _mutex = new Mutex(true, InstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                var signaled = false;
                try
                {
                    using (var existing = EventWaitHandle.OpenExisting(ShowMainEventName))
                    {
                        existing.Set();
                        signaled = true;
                    }
                }
                catch { }

                try
                {
                    var msg = signaled
                        ? "Todowork 已在运行，已为你切换到现有窗口。\r\n\r\n提示：本次启动进程会自动退出，这是正常行为。"
                        : "Todowork 已在运行，但无法唤醒现有窗口。\r\n\r\n请在任务栏托盘(含隐藏图标)中找到 Todowork 并打开，或先退出后再启动。\r\n\r\n提示：本次启动进程会自动退出，这是正常行为。";

                    MessageBox.Show(
                        msg,
                        "Todowork",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch { }

                Shutdown();
                return;
            }

            _showMainEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowMainEventName);
            _showMainWaitThread = new Thread(ShowMainWaitLoop)
            {
                IsBackground = true,
                Name = "Todowork_ShowMainWaiter"
            };
            _showMainWaitThread.Start();

            var dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Todowork",
                "todo.json");

            var repository = new TodoRepository(dataPath);
            _store = new TodoStore(repository);
            _store.Load();

            var mainVm = new MainViewModel(_store);
            var overlayVm = new OverlayViewModel(_store);

            _mainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            MainWindow = _mainWindow;

            _overlayWindow = new OverlayWindow
            {
                DataContext = overlayVm
            };

            var overlayShouldShow = true;
            try
            {
                var ui = LoadUiState();
                if (ui != null)
                {
                    overlayShouldShow = ui.OverlayVisible;

                    _overlayShouldShow = overlayShouldShow;

                    if (!double.IsNaN(ui.OverlayLeft) && !double.IsNaN(ui.OverlayTop))
                    {
                        _overlayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                        _overlayWindow.Left = ui.OverlayLeft;
                        _overlayWindow.Top = ui.OverlayTop;
                    }

                    if (!double.IsNaN(ui.OverlayWidth) && ui.OverlayWidth > 0)
                    {
                        _overlayWindow.Width = ui.OverlayWidth;
                    }

                    try
                    {
                        var wa = SystemParameters.WorkArea;
                        if (_overlayWindow.Width > wa.Width * 0.95)
                        {
                            _overlayWindow.Width = 320;
                        }
                    }
                    catch { }

                    try
                    {
                        mainVm.OverlayShowBackground = ui.OverlayShowBackground;

                        if (!double.IsNaN(ui.OverlayBackgroundOpacity))
                        {
                            mainVm.OverlayBackgroundOpacity = ui.OverlayBackgroundOpacity;
                        }

                        if (!double.IsNaN(ui.OverlayTextOpacity))
                        {
                            mainVm.OverlayTextOpacity = ui.OverlayTextOpacity;
                        }

                        if (!double.IsNaN(ui.OverlayTextFontSize))
                        {
                            mainVm.OverlayTextFontSize = ui.OverlayTextFontSize;
                        }

                        if (!string.IsNullOrWhiteSpace(ui.OverlayTextColor))
                        {
                            mainVm.OverlayTextColor = ui.OverlayTextColor;
                        }

                        if (!double.IsNaN(ui.OverlayLeftRatio))
                        {
                            mainVm.SetOverlayLeftRatioSilently(ui.OverlayLeftRatio);
                        }

                        if (!double.IsNaN(ui.OverlayTopRatio))
                        {
                            mainVm.SetOverlayTopRatioSilently(ui.OverlayTopRatio);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            _trayService = new TrayService();
            _trayService.ShowMainRequested += (s1, e1) => TrayService.BringToFront(_mainWindow);
            _trayService.ToggleOverlayRequested += (s1, e1) => ToggleOverlay();
            _trayService.ExitRequested += (s1, e1) => ExitApp();
            _trayService.Start();

            try { _mainWindow.Hide(); } catch { }

            if (overlayShouldShow)
            {
                _overlayWindow.Show();
            }

            try
            {
                SetOverlayShowBackground(mainVm.OverlayShowBackground);
                SetOverlayBackgroundOpacity(mainVm.OverlayBackgroundOpacity);
                SetOverlayTextOpacity(mainVm.OverlayTextOpacity);
                SetOverlayTextFontSize(mainVm.OverlayTextFontSize);
                SetOverlayTextBrush((Brush)new BrushConverter().ConvertFromString(mainVm.OverlayTextColor));
            }
            catch { }

            try
            {
                mainVm.SetOverlayLeftRatioSilently(GetOverlayLeftRatio());
                mainVm.SetOverlayTopRatioSilently(GetOverlayTopRatio());
            }
            catch { }

            try { SaveUiState(); } catch { }
        }

        public void SetOverlayTopRatio(double ratio)
        {
            if (_overlayWindow == null) return;

            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            try
            {
                var wa = SystemParameters.WorkArea;
                var h = _overlayWindow.Height;
                if (double.IsNaN(h) || h <= 0) h = _overlayWindow.ActualHeight;
                if (h <= 0) h = _overlayWindow.RenderSize.Height;

                var maxTop = wa.Bottom - h;
                if (maxTop <= wa.Top)
                {
                    _overlayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _overlayWindow.Top = wa.Top;
                }
                else
                {
                    _overlayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _overlayWindow.Top = wa.Top + (maxTop - wa.Top) * ratio;
                }
            }
            catch { }

            try { SaveUiState(); } catch { }
        }

        private double GetOverlayLeftRatio()
        {
            if (_overlayWindow == null) return 0;

            try
            {
                var wa = SystemParameters.WorkArea;
                var w = _overlayWindow.Width;
                if (double.IsNaN(w) || w <= 0) w = _overlayWindow.ActualWidth;
                if (w <= 0) w = _overlayWindow.RenderSize.Width;

                var maxLeft = wa.Right - w;
                if (maxLeft <= wa.Left) return 0;

                var ratio = (_overlayWindow.Left - wa.Left) / (maxLeft - wa.Left);
                if (ratio < 0) ratio = 0;
                if (ratio > 1) ratio = 1;
                return ratio;
            }
            catch
            {
                return 0;
            }
        }

        private double GetOverlayTopRatio()
        {
            if (_overlayWindow == null) return 0;

            try
            {
                var wa = SystemParameters.WorkArea;
                var h = _overlayWindow.Height;
                if (double.IsNaN(h) || h <= 0) h = _overlayWindow.ActualHeight;
                if (h <= 0) h = _overlayWindow.RenderSize.Height;

                var maxTop = wa.Bottom - h;
                if (maxTop <= wa.Top) return 0;

                var ratio = (_overlayWindow.Top - wa.Top) / (maxTop - wa.Top);
                if (ratio < 0) ratio = 0;
                if (ratio > 1) ratio = 1;
                return ratio;
            }
            catch
            {
                return 0;
            }
        }

        public void SetOverlayLeftRatio(double ratio)
        {
            if (_overlayWindow == null) return;

            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            try
            {
                var wa = SystemParameters.WorkArea;
                var w = _overlayWindow.Width;
                if (double.IsNaN(w) || w <= 0) w = _overlayWindow.ActualWidth;
                if (w <= 0) w = _overlayWindow.RenderSize.Width;

                var maxLeft = wa.Right - w;
                if (maxLeft <= wa.Left)
                {
                    _overlayWindow.Left = wa.Left;
                }
                else
                {
                    _overlayWindow.Left = wa.Left + (maxLeft - wa.Left) * ratio;
                }

                _overlayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            }
            catch { }

            try { SaveUiState(); } catch { }
        }

        public void NudgeOverlayLeft(double dx)
        {
            if (_overlayWindow == null) return;

            try
            {
                var wa = SystemParameters.WorkArea;
                var newLeft = _overlayWindow.Left + dx;
                var maxLeft = wa.Right - _overlayWindow.Width;
                if (newLeft < wa.Left) newLeft = wa.Left;
                if (newLeft > maxLeft) newLeft = maxLeft;

                _overlayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                _overlayWindow.Left = newLeft;
            }
            catch { }

            try { SaveUiState(); } catch { }
        }

        [DataContract]
        private sealed class UiState
        {
            public UiState()
            {
                OverlayLeft = double.NaN;
                OverlayTop = double.NaN;
                OverlayWidth = double.NaN;

                OverlayBackgroundOpacity = double.NaN;
                OverlayTextOpacity = double.NaN;
                OverlayTextFontSize = double.NaN;

                OverlayLeftRatio = double.NaN;
                OverlayTopRatio = double.NaN;
            }

            [DataMember] public double OverlayLeft { get; set; }
            [DataMember] public double OverlayTop { get; set; }
            [DataMember] public double OverlayWidth { get; set; }
            [DataMember] public bool OverlayVisible { get; set; }

            [DataMember] public bool OverlayShowBackground { get; set; }
            [DataMember] public double OverlayBackgroundOpacity { get; set; }
            [DataMember] public double OverlayTextOpacity { get; set; }
            [DataMember] public double OverlayTextFontSize { get; set; }
            [DataMember] public string OverlayTextColor { get; set; }

            [DataMember] public double OverlayLeftRatio { get; set; }
            [DataMember] public double OverlayTopRatio { get; set; }
        }

        private static string GetUiStatePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Todowork");
            return Path.Combine(dir, "ui.json");
        }

        private UiState LoadUiState()
        {
            var path = GetUiStatePath();
            if (!File.Exists(path)) return null;

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(UiState));
                    return serializer.ReadObject(stream) as UiState;
                }
            }
            catch
            {
                return null;
            }
        }

        internal void SaveUiState()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Todowork");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var path = GetUiStatePath();
                var tmp = path + ".tmp";

                var vm = _mainWindow?.DataContext as Todowork.ViewModels.MainViewModel;

                var state = new UiState
                {
                    OverlayLeft = _overlayWindow?.Left ?? double.NaN,
                    OverlayTop = _overlayWindow?.Top ?? double.NaN,
                    OverlayWidth = _overlayWindow?.Width ?? double.NaN,
                    OverlayVisible = _overlayShouldShow,

                    OverlayShowBackground = vm?.OverlayShowBackground ?? false,
                    OverlayBackgroundOpacity = vm?.OverlayBackgroundOpacity ?? double.NaN,
                    OverlayTextOpacity = vm?.OverlayTextOpacity ?? double.NaN,
                    OverlayTextFontSize = vm?.OverlayTextFontSize ?? double.NaN,
                    OverlayTextColor = vm?.OverlayTextColor,

                    OverlayLeftRatio = vm?.OverlayLeftRatio ?? double.NaN,
                    OverlayTopRatio = vm?.OverlayTopRatio ?? double.NaN
                };

                using (var stream = File.Create(tmp))
                {
                    var serializer = new DataContractJsonSerializer(typeof(UiState));
                    serializer.WriteObject(stream, state);
                }

                if (File.Exists(path))
                {
                    File.Replace(tmp, path, null);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch { }
        }

        public void SetOverlayShowBackground(bool show)
        {
            if (_overlayWindow == null) return;
            try
            {
                if (Dispatcher != null && !Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { _overlayWindow.ShowBackground = show; } catch { }
                        try { SaveUiState(); } catch { }
                    }));
                    return;
                }
            }
            catch { }

            try { _overlayWindow.ShowBackground = show; } catch { }
            try { SaveUiState(); } catch { }
        }

        public void SetOverlayBackgroundOpacity(double opacity)
        {
            if (_overlayWindow == null) return;
            if (opacity < 0.0) opacity = 0.0;
            if (opacity > 1.0) opacity = 1.0;

            try
            {
                if (Dispatcher != null && !Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { _overlayWindow.BackgroundOpacity = opacity; } catch { }
                        try { SaveUiState(); } catch { }
                    }));
                    return;
                }
            }
            catch { }

            try { _overlayWindow.BackgroundOpacity = opacity; } catch { }
            try { SaveUiState(); } catch { }
        }

        public void SetOverlayTextOpacity(double opacity)
        {
            if (_overlayWindow == null) return;
            if (opacity < 0.0) opacity = 0.0;
            if (opacity > 1.0) opacity = 1.0;

            try
            {
                if (Dispatcher != null && !Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { _overlayWindow.TextOpacity = opacity; } catch { }
                        try { SaveUiState(); } catch { }
                    }));
                    return;
                }
            }
            catch { }

            try { _overlayWindow.TextOpacity = opacity; } catch { }
            try { SaveUiState(); } catch { }
        }

        public void SetOverlayTextFontSize(double fontSize)
        {
            if (_overlayWindow == null) return;
            if (fontSize < 6) fontSize = 6;
            if (fontSize > 72) fontSize = 72;

            try
            {
                if (Dispatcher != null && !Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { _overlayWindow.TextFontSize = fontSize; } catch { }
                        try { SaveUiState(); } catch { }
                    }));
                    return;
                }
            }
            catch { }

            try { _overlayWindow.TextFontSize = fontSize; } catch { }
            try { SaveUiState(); } catch { }
        }

        public void SetOverlayTextBrush(Brush brush)
        {
            if (_overlayWindow == null) return;
            if (brush == null) return;

            try
            {
                if (Dispatcher != null && !Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { _overlayWindow.TextBrush = brush; } catch { }
                        try { SaveUiState(); } catch { }
                    }));
                    return;
                }
            }
            catch { }

            try { _overlayWindow.TextBrush = brush; } catch { }
            try { SaveUiState(); } catch { }
        }

        private void ShowMainWaitLoop()
        {
            while (!_isExiting)
            {
                try
                {
                    _showMainEvent?.WaitOne();
                }
                catch
                {
                    return;
                }

                if (_isExiting) return;

                if (Dispatcher == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                {
                    continue;
                }

                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_isExiting) return;

                        if (_mainWindow != null)
                        {
                            TrayService.BringToFront(_mainWindow);
                        }
                    }));
                }
                catch { }
            }
        }


        private void LogException(Exception ex, string source)
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Todowork");

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var logPath = Path.Combine(logDir, "crash.log");
            var sb = new StringBuilder();
            sb.AppendLine("----");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(source + " | pid=" + Process.GetCurrentProcess().Id);
            sb.AppendLine(ex?.ToString() ?? "<null exception>");

            var acquired = false;
            try
            {
                try
                {
                    acquired = LogMutex.WaitOne(250);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(50);
                    }
                }
            }
            finally
            {
                if (acquired)
                {
                    try { LogMutex.ReleaseMutex(); } catch { }
                }
            }
        }

        private void ToggleOverlay()
        {
            if (_overlayWindow == null) return;

            if (_overlayWindow.IsVisible)
            {
                _overlayWindow.Hide();
                _overlayShouldShow = false;
            }
            else
            {
                _overlayWindow.Show();
                _overlayWindow.Activate();
                _overlayShouldShow = true;
            }

            try { SaveUiState(); } catch { }
        }

        private void ExitApp()
        {
            _isExiting = true;

            try { _showMainEvent?.Set(); } catch { }

            try
            {
                _store?.Dispose();
            }
            catch { }

            try
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.Close();
                }

                if (_mainWindow != null)
                {
                    _mainWindow.Close();
                }
            }
            catch { }

            Shutdown();
        }

        public void RequestExit()
        {
            ExitApp();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                try { _showMainEvent?.Set(); } catch { }

                _store?.Dispose();
            }
            catch { }

            try
            {
                _store?.Save();
            }
            catch { }

            try { SaveUiState(); } catch { }

            try
            {
                _trayService?.Dispose();
            }
            catch { }

            try
            {
                _showMainEvent?.Dispose();
            }
            catch { }

            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { }
        }

        public bool IsExiting => _isExiting;
    }
}
