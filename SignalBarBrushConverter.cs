using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WifiConnect;

/// <summary>
/// 電波強度の棒グラフ用。SignalBars が ConverterParameter（1〜4）以上なら濃い色にする。
/// </summary>
public sealed class SignalBarBrushConverter : IValueConverter
{
    private static readonly Brush Active = new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
    private static readonly Brush Inactive = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var bars = value is byte b ? b : 0;
        var level = int.TryParse(parameter?.ToString(), out var n) ? n : 1;
        return bars >= level ? Active : Inactive;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
