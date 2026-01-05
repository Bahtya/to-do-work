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
        public MainWindow()
        {
            InitializeComponent();
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
