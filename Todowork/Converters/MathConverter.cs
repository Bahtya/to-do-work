using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Todowork.Converters
{
    public sealed class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return DependencyProperty.UnsetValue;

            if (!TryToDouble(value, out var number)) return DependencyProperty.UnsetValue;

            var op = parameter as string;
            if (string.IsNullOrWhiteSpace(op)) return number;

            op = op.Trim();
            if (op.Length < 2) return number;

            var operation = op[0];
            if (!double.TryParse(op.Substring(1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var operand))
            {
                return number;
            }

            switch (operation)
            {
                case '+':
                    return number + operand;
                case '-':
                    return number - operand;
                case '*':
                    return number * operand;
                case '/':
                    return operand == 0 ? number : number / operand;
                default:
                    return number;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return DependencyProperty.UnsetValue;

            if (!TryToDouble(value, out var number)) return DependencyProperty.UnsetValue;

            var op = parameter as string;
            if (string.IsNullOrWhiteSpace(op)) return number;

            op = op.Trim();
            if (op.Length < 2) return number;

            var operation = op[0];
            if (!double.TryParse(op.Substring(1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var operand))
            {
                return number;
            }

            switch (operation)
            {
                case '+':
                    return number - operand;
                case '-':
                    return number + operand;
                case '*':
                    return operand == 0 ? number : number / operand;
                case '/':
                    return number * operand;
                default:
                    return number;
            }
        }

        private static bool TryToDouble(object value, out double result)
        {
            try
            {
                result = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }
    }
}
