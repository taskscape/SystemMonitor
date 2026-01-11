using System.Globalization;

namespace SystemMonitorMobile;

public sealed class BytesToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "0 B";
        }

        if (!double.TryParse(value.ToString(), out var bytes))
        {
            return "0 B";
        }

        return FormatBytes(bytes);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string FormatBytes(double bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unitIndex = 0;
        var value = Math.Max(0, bytes);
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex += 1;
        }

        return $"{value:0.0} {units[unitIndex]}";
    }
}
