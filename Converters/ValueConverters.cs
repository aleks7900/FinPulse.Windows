using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Converters;

public class MoneyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Money money)
        {
            return money.Formatted();
        }
        return "$0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long hex)
        {
            byte a = (byte)((hex >> 24) & 0xFF);
            byte r = (byte)((hex >> 16) & 0xFF);
            byte g = (byte)((hex >> 8) & 0xFF);
            byte b = (byte)(hex & 0xFF);
            if (a == 0) a = 255;
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
        if (value is string hexStr && !string.IsNullOrWhiteSpace(hexStr))
        {
            hexStr = hexStr.TrimStart('#');
            if (hexStr.Length == 6)
            {
                byte r = byte.Parse(hexStr[..2], System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hexStr[2..4], System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hexStr[4..6], System.Globalization.NumberStyles.HexNumber);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            if (hexStr.Length == 8)
            {
                byte a = byte.Parse(hexStr[..2], System.Globalization.NumberStyles.HexNumber);
                byte r = byte.Parse(hexStr[2..4], System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hexStr[4..6], System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hexStr[6..8], System.Globalization.NumberStyles.HexNumber);
                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class RatioToPercentStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return $"{(int)Math.Round(d * 100)}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
