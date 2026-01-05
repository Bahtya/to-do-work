using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Reflection;

namespace Todowork.Services
{
    public sealed class TrayService : IDisposable
    {
        private NotifyIcon _notifyIcon;

        public event EventHandler ShowMainRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler ToggleOverlayRequested;
        public event EventHandler ExitRequested;

        public void Start()
        {
            if (_notifyIcon != null) return;

            _notifyIcon = new NotifyIcon
            {
                Text = "Todowork",
                Visible = true,
                Icon = LoadIcon() ?? System.Drawing.SystemIcons.Application
            };

            var menu = new ContextMenuStrip();

            var autoStartService = new AutoStartService();
            var autoStartItem = new ToolStripMenuItem("开机自启")
            {
                CheckOnClick = true
            };

            try { autoStartItem.Checked = autoStartService.IsEnabled(); } catch { }
            autoStartItem.CheckedChanged += (s, e) =>
            {
                try { autoStartService.SetEnabled(autoStartItem.Checked); } catch { }
            };

            var showItem = new ToolStripMenuItem("显示面板");
            showItem.Click += (s, e) => ShowMainRequested?.Invoke(this, EventArgs.Empty);

            var settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

            var toggleOverlayItem = new ToolStripMenuItem("显示/隐藏置顶浮窗");
            toggleOverlayItem.Click += (s, e) => ToggleOverlayRequested?.Invoke(this, EventArgs.Empty);

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

            menu.Items.Add(autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(showItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(toggleOverlayItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainRequested?.Invoke(this, EventArgs.Empty);
        }

        private static System.Drawing.Icon LoadIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/TodoworkTD_gray.ico", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                var stream = streamInfo?.Stream;
                if (stream != null)
                {
                    using (stream)
                    {
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            ms.Position = 0;
                            return new System.Drawing.Icon(ms);
                        }
                    }
                }
            }
            catch { }

            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconPath = Path.Combine(baseDir, "Assets", "TodoworkTD_gray.ico");
                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            return null;
        }

        public void Dispose()
        {
            if (_notifyIcon == null) return;

            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch { }
            finally
            {
                _notifyIcon = null;
            }
        }

        public static void BringToFront(Window window)
        {
            if (window == null) return;
            if (!window.IsVisible) window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }
    }
}
