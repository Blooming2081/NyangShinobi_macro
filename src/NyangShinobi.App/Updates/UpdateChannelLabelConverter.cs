using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NyangShinobi.App.Updates;

// 저장된 Development=0/Stable=1은 유지하고 사용자에게 보이는 이름만 바꾼다.
public sealed class UpdateChannelLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        UpdateChannel.Development => "Pre-release (테스트)",
        UpdateChannel.Stable => "Stable",
        _ => DependencyProperty.UnsetValue
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
