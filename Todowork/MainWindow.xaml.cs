using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Todowork.Models;
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F2) return;
            if (!(DataContext is MainViewModel vm)) return;
            if (vm.SelectedItem == null) return;
            BeginEdit(vm.SelectedItem);
            e.Handled = true;
        }

        private void TodoText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (!(sender is FrameworkElement fe)) return;
            if (!(fe.DataContext is TodoItem item)) return;
            BeginEdit(item);
            e.Handled = true;
        }

        private void BeginEdit(TodoItem item)
        {
            if (item == null) return;
            item.EditText = item.Text;
            item.IsEditing = true;
        }

        private void CommitEdit(TodoItem item)
        {
            if (item == null) return;
            var text = item.EditText;
            if (string.IsNullOrWhiteSpace(text))
            {
                CancelEdit(item);
                return;
            }

            item.Text = text.Trim();
            item.IsEditing = false;
        }

        private void CancelEdit(TodoItem item)
        {
            if (item == null) return;
            item.IsEditing = false;
        }

        private void EditTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe)) return;
            if (!(fe.DataContext is TodoItem item)) return;
            if (!item.IsEditing) return;
            CommitEdit(item);
        }

        private void EditTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (!(tb.DataContext is TodoItem item)) return;

            var isEnter = e.Key == Key.Enter || e.Key == Key.Return || e.ImeProcessedKey == Key.Enter;
            if (isEnter && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                CommitEdit(item);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelEdit(item);
                e.Handled = true;
            }
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
