using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Todowork.ViewModels;

namespace Todowork
{
    public partial class MainWindow : Window
    {
        private SettingsWindow _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow
                {
                    Owner = this,
                    DataContext = DataContext
                };

                _settingsWindow.Closed += (s, _) => { _settingsWindow = null; };
            }
            else
            {
                _settingsWindow.DataContext = DataContext;
            }

            _settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _settingsWindow.ShowInTaskbar = false;
            _settingsWindow.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch { }
        }

        private void NewTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var isEnter = e.Key == Key.Enter || e.Key == Key.Return || e.ImeProcessedKey == Key.Enter;
            if (!isEnter) return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) return;

            if (sender is TextBox tb)
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (DataContext is MainViewModel vm)
            {
                if (vm.AddCommand.CanExecute(null))
                {
                    vm.AddCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
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
