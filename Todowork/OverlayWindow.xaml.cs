using System.ComponentModel;
using System.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Todowork
{
    public partial class OverlayWindow : Window
    {
        public static readonly DependencyProperty BackgroundOpacityProperty =
            DependencyProperty.Register(
                nameof(BackgroundOpacity),
                typeof(double),
                typeof(OverlayWindow),
                new PropertyMetadata(0.67));

        public static readonly DependencyProperty TextOpacityProperty =
            DependencyProperty.Register(
                nameof(TextOpacity),
                typeof(double),
                typeof(OverlayWindow),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty ShowBackgroundProperty =
            DependencyProperty.Register(
                nameof(ShowBackground),
                typeof(bool),
                typeof(OverlayWindow),
                new PropertyMetadata(false));

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(
                nameof(TextFontSize),
                typeof(double),
                typeof(OverlayWindow),
                new PropertyMetadata(16.0));

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(
                nameof(TextBrush),
                typeof(Brush),
                typeof(OverlayWindow),
                new PropertyMetadata(Brushes.White));

        public double BackgroundOpacity
        {
            get => (double)GetValue(BackgroundOpacityProperty);
            set => SetValue(BackgroundOpacityProperty, value);
        }

        public double TextOpacity
        {
            get => (double)GetValue(TextOpacityProperty);
            set => SetValue(TextOpacityProperty, value);
        }

        public bool ShowBackground
        {
            get => (bool)GetValue(ShowBackgroundProperty);
            set => SetValue(ShowBackgroundProperty, value);
        }

        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        public OverlayWindow()
        {
            InitializeComponent();
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try { ApplySizeConstraintsAndClampToWorkArea(); } catch { }
        }

        private void OverlayWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try { ApplySizeConstraintsAndClampToWorkArea(); } catch { }

            try
            {
                var app = Application.Current as App;
                app?.SaveUiState();
            }
            catch { }
        }

        private void ApplySizeConstraintsAndClampToWorkArea()
        {
            var wa = SystemParameters.WorkArea;

            MaxHeight = Math.Max(80, wa.Height * 0.9);

            if (double.IsNaN(Left) || double.IsNaN(Top)) return;

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var maxLeft = wa.Right - w;
            var maxTop = wa.Bottom - h;

            if (Left < wa.Left) Left = wa.Left;
            if (Left > maxLeft) Left = maxLeft;
            if (Top < wa.Top) Top = wa.Top;
            if (Top > maxTop) Top = maxTop;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                exStyle |= WS_EX_TRANSPARENT;
                exStyle |= WS_EX_LAYERED;
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            }
            catch { }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void OverlayWindow_Closing(object sender, CancelEventArgs e)
        {
            var app = Application.Current as App;

            if (Debugger.IsAttached && app != null)
            {
                e.Cancel = true;
                app.RequestExit();
                return;
            }

            if (app != null && !app.IsExiting)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
