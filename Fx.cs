using System.Windows;
using System.Windows.Media;

namespace PhoneBook;

// Attached свойства, които стиловете подават на общия шаблон на бутоните.
// Така един ControlTemplate обслужва всички видове бутони, а всеки стил
// задава собствения си hover цвят и радиус.
public static class Fx
{
    // Цвят, към който бутонът плавно преминава при посочване (crossfade чрез Opacity)
    public static readonly DependencyProperty HoverBrushProperty =
        DependencyProperty.RegisterAttached("HoverBrush", typeof(Brush), typeof(Fx),
            new FrameworkPropertyMetadata(null));

    public static Brush? GetHoverBrush(DependencyObject o) => (Brush?)o.GetValue(HoverBrushProperty);
    public static void SetHoverBrush(DependencyObject o, Brush? value) => o.SetValue(HoverBrushProperty, value);

    // Рамка при посочване (за бутоните с контур)
    public static readonly DependencyProperty HoverBorderBrushProperty =
        DependencyProperty.RegisterAttached("HoverBorderBrush", typeof(Brush), typeof(Fx),
            new FrameworkPropertyMetadata(null));

    public static Brush? GetHoverBorderBrush(DependencyObject o) => (Brush?)o.GetValue(HoverBorderBrushProperty);
    public static void SetHoverBorderBrush(DependencyObject o, Brush? value) => o.SetValue(HoverBorderBrushProperty, value);

    // Закръгляне на ъглите (кръглият бутон за темата използва 19)
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius), typeof(Fx),
            new FrameworkPropertyMetadata(new CornerRadius(8)));

    public static CornerRadius GetCornerRadius(DependencyObject o) => (CornerRadius)o.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject o, CornerRadius value) => o.SetValue(CornerRadiusProperty, value);
}
