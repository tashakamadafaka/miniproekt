using System.Globalization;
using System.Windows.Data;

namespace PhoneBook;

// Име -> инициали (за прегледа на живо във формата)
// Цветовете на групите вече не са в конвертор, а в стиловете GroupTint / GroupDot (App.xaml),
// защото трябва да се сменят заедно с темата.
public class InitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Contact.GetInitials(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
