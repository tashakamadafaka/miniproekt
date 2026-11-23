using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PhoneBook;

public enum AppTheme
{
    Dark,
    Light
}

// Смяна на темата: подменя първия MergedDictionary в App.xaml (Themes/Dark.xaml или Themes/Light.xaml).
// Всички цветове се ползват чрез {DynamicResource}, затова интерфейсът се прерисува веднага.
public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "theme.txt");

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    // Зарежда последно избраната тема (по подразбиране – тъмна)
    public static void Initialize()
    {
        var theme = AppTheme.Dark;
        try
        {
            if (File.Exists(SettingsPath) && Enum.TryParse(File.ReadAllText(SettingsPath).Trim(), out AppTheme saved))
                theme = saved;
        }
        catch
        {
            // Настройката не е критична – при грешка оставаме на тъмна тема
        }
        Apply(theme, save: false);
    }

    public static void Toggle() => Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    public static void Apply(AppTheme theme, bool save = true)
    {
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml", UriKind.Absolute)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0)
            merged.Add(dictionary);
        else
            merged[0] = dictionary;

        Current = theme;

        foreach (Window window in Application.Current.Windows)
            ApplyTitleBar(window);

        if (save)
        {
            try { File.WriteAllText(SettingsPath, theme.ToString()); }
            catch { /* без запазване, ако папката е само за четене */ }
        }
    }

    // ================= ЗАГЛАВНА ЛЕНТА НА WINDOWS =================
    // Без това лентата с бутоните „_ □ X“ остава бяла върху тъмния прозорец.

    private const int DwmUseImmersiveDarkMode = 20;       // Windows 10 20H1+ и Windows 11
    private const int DwmUseImmersiveDarkModeOld = 19;    // по-стари Windows 10
    private const int DwmCaptionColor = 35;               // Windows 11: цвят на лентата
    private const int DwmTextColor = 36;                  // Windows 11: цвят на заглавието

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return; // прозорецът още няма handle – ще се извика отново от SourceInitialized

        try
        {
            var dark = Current == AppTheme.Dark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeOld, ref dark, sizeof(int));

            // На Windows 11 лентата приема точно цвета на фона – прозорецът изглежда „безшевен“.
            // На Windows 10 тези извиквания просто връщат грешка и нищо не се случва.
            if (Application.Current.TryFindResource("CanvasBrush") is SolidColorBrush canvas)
            {
                var caption = ToColorRef(canvas.Color);
                DwmSetWindowAttribute(hwnd, DwmCaptionColor, ref caption, sizeof(int));
            }
            if (Application.Current.TryFindResource("TextPrimaryBrush") is SolidColorBrush text)
            {
                var textColor = ToColorRef(text.Color);
                DwmSetWindowAttribute(hwnd, DwmTextColor, ref textColor, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // Няма DWM (напр. стара система) – приложението работи и без това
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    // COLORREF е 0x00BBGGRR
    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
