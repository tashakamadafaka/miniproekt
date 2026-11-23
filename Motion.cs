using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PhoneBook;

// Малки C# помощници за анимациите, които се пускат от кода.
// Анимират се само Opacity, RenderTransform и SolidColorBrush.Color, никога Width/Height/Margin,
// за да не се преизчислява layout на всеки кадър.
public static class Motion
{
    private static readonly IEasingFunction CubicOut = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction CubicInOut = Frozen(new CubicEase { EasingMode = EasingMode.EaseInOut });
    private static readonly IEasingFunction QuadOut = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction SineOut = Frozen(new SineEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction BackOut = Frozen(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 });

    // Пази коя е последната пусната анимация за даден елемент,
    // за да не скрие стар Completed елемент, който междувременно е показан отново.
    private static readonly ConditionalWeakTable<DependencyObject, object> Tokens = new();

    // ================= ВАЛИДАЦИЯ =================

    // Хоризонтално „поклащане“ с отслабваща амплитуда (≈300 ms)
    public static void Shake(UIElement target)
    {
        var t = EnsureTranslate(target);
        double[] offsets = { -7, 7, -5, 5, -2, 0 };
        var anim = new DoubleAnimationUsingKeyFrames { Duration = Ms(300) };
        for (var i = 0; i < offsets.Length; i++)
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50 * (i + 1))), SineOut));
        t.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    // Показва елемент с плавно появяване и леко спускане отгоре
    public static void FadeIn(UIElement el, int ms = 180, double fromY = -4)
    {
        NewToken(el);
        el.Visibility = Visibility.Visible;
        el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Ms(ms)) { EasingFunction = QuadOut });
        EnsureTranslate(el).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromY, 0, Ms(ms + 60)) { EasingFunction = CubicOut });
    }

    // Плавно скриване, след което елементът се свива (Collapsed)
    public static void FadeOutCollapse(UIElement el, int ms = 140)
    {
        if (el.Visibility != Visibility.Visible)
            return;

        var token = NewToken(el);
        var anim = new DoubleAnimation(0, Ms(ms)) { EasingFunction = QuadOut };
        anim.Completed += (_, _) =>
        {
            if (IsCurrent(el, token))
                el.Visibility = Visibility.Collapsed;
        };
        el.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // Скрива веднага, без анимация (и спира текуща)
    public static void HideNow(UIElement el)
    {
        NewToken(el);
        el.BeginAnimation(UIElement.OpacityProperty, null);
        el.Visibility = Visibility.Collapsed;
    }

    // ================= РЕДАКТОР =================

    // Кратко „плъзгане“ на съдържанието при смяна на контакт (SPA усещане)
    public static void SlideRefresh(UIElement el)
    {
        el.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.65, 1, Ms(200)) { EasingFunction = CubicOut });
        EnsureTranslate(el).BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(10, 0, Ms(200)) { EasingFunction = CubicOut });
    }

    // Леко „подскачане“ (напр. брояча при промяна или бутона „Потвърди“)
    public static void Pop(UIElement el, double from = 1.12, int ms = 260)
    {
        var s = EnsureScale(el);
        var anim = new DoubleAnimation(from, 1, Ms(ms)) { EasingFunction = BackOut };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        s.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    // ================= ТАБЛИЦА =================

    // Появяване на ред с изчакване (за каскаден ефект). Ключовите кадри държат реда
    // невидим през изчакването, без да се пипа локалната стойност на Opacity.
    public static void FadeUp(UIElement el, int delayMs, int ms = 200, double fromY = 8)
    {
        var opacity = new DoubleAnimationUsingKeyFrames();
        opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs))));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs + ms)), QuadOut));

        var y = new DoubleAnimationUsingKeyFrames();
        y.KeyFrames.Add(new DiscreteDoubleKeyFrame(fromY, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        y.KeyFrames.Add(new DiscreteDoubleKeyFrame(fromY, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs))));
        y.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs + ms + 60)), CubicOut));

        el.BeginAnimation(UIElement.OpacityProperty, opacity);
        EnsureTranslate(el).BeginAnimation(TranslateTransform.YProperty, y);
    }

    // Премахва анимацията на ред (при скролиране редовете се показват веднага)
    public static void ClearFadeUp(UIElement el)
    {
        el.BeginAnimation(UIElement.OpacityProperty, null);
        if (el.RenderTransform is TranslateTransform { IsFrozen: false } t)
            t.BeginAnimation(TranslateTransform.YProperty, null);
    }

    // Кратко оцветяване на ред след запис, което избледнява към нормалния му фон
    public static void Flash(Control el, Color from, Color to, int ms = 650)
    {
        var token = NewToken(el);
        var brush = new SolidColorBrush(from);
        el.Background = brush;

        var anim = new ColorAnimation(to, Ms(ms)) { BeginTime = TimeSpan.FromMilliseconds(140), EasingFunction = QuadOut };
        anim.Completed += (_, _) =>
        {
            if (IsCurrent(el, token))
                el.ClearValue(Control.BackgroundProperty); // връща фона към стила (hover/избран)
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    // ================= ТЕМА =================

    // Смяна на иконата луна/слънце: старата се завърта навън, новата влиза с лек отскок
    public static void SwapIcon(FrameworkElement show, FrameworkElement hide, bool animate)
    {
        var (showScale, showRotate) = EnsureScaleRotate(show);
        var (hideScale, hideRotate) = EnsureScaleRotate(hide);

        if (!animate)
        {
            show.BeginAnimation(UIElement.OpacityProperty, null);
            hide.BeginAnimation(UIElement.OpacityProperty, null);
            show.Opacity = 1;
            hide.Opacity = 0;
            SetScaleRotate(showScale, showRotate, 1, 0);
            SetScaleRotate(hideScale, hideRotate, 0.4, -90);
            return;
        }

        hide.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, Ms(160)) { EasingFunction = QuadOut });
        AnimateScale(hideScale, new DoubleAnimation(0.4, Ms(240)) { EasingFunction = CubicOut });
        hideRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-90, Ms(280)) { EasingFunction = CubicOut });

        show.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Ms(220)) { EasingFunction = QuadOut });
        AnimateScale(showScale, new DoubleAnimation(0.4, 1, Ms(360)) { EasingFunction = BackOut });
        showRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(90, 0, Ms(380)) { EasingFunction = CubicOut });
    }

    // Кръгово разкриване на новата тема от бутона:
    // 1) снимка на прозореца в старата тема се слага най-отгоре;
    // 2) темата се сменя отдолу;
    // 3) в снимката се отваря разширяваща се кръгла „дупка“, през която се вижда новата тема.
    public static void CircularReveal(FrameworkElement shell, Image overlay, Point origin, Action swap, Action done)
    {
        double w = shell.ActualWidth, h = shell.ActualHeight;
        if (w < 1 || h < 1)
        {
            swap();
            done();
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(shell);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(w * dpi.DpiScaleX), (int)Math.Ceiling(h * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        // Рисуваме през VisualBrush, за да не влияе отместването на елемента
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var brush = new VisualBrush(shell)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, w, h)
            };
            dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
        }
        bitmap.Render(visual);
        bitmap.Freeze();

        var hole = new EllipseGeometry(origin, 0, 0);
        overlay.Source = bitmap;
        overlay.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, w, h)), hole);
        overlay.Visibility = Visibility.Visible;

        swap();

        // Радиусът трябва да стигне до най-далечния ъгъл
        var radius = new[] { new Point(0, 0), new Point(w, 0), new Point(0, h), new Point(w, h) }
            .Max(p => (p - origin).Length) + 2;

        var growX = new DoubleAnimation(0, radius, Ms(440)) { EasingFunction = CubicInOut };
        var growY = new DoubleAnimation(0, radius, Ms(440)) { EasingFunction = CubicInOut };
        growX.Completed += (_, _) =>
        {
            overlay.Visibility = Visibility.Collapsed;
            overlay.Source = null;
            overlay.Clip = null;
            done();
        };
        hole.BeginAnimation(EllipseGeometry.RadiusXProperty, growX);
        hole.BeginAnimation(EllipseGeometry.RadiusYProperty, growY);
    }

    // ================= ПОМОЩНИ =================

    // Гарантира TranslateTransform, който може да се анимира (вградената Identity е замразена)
    private static TranslateTransform EnsureTranslate(UIElement el)
    {
        switch (el.RenderTransform)
        {
            case TranslateTransform { IsFrozen: false } t:
                return t;
            case TransformGroup { IsFrozen: false } g:
                var existing = g.Children.OfType<TranslateTransform>().FirstOrDefault(x => !x.IsFrozen);
                if (existing != null)
                    return existing;
                var added = new TranslateTransform();
                g.Children.Add(added);
                return added;
            default:
                var created = new TranslateTransform();
                el.RenderTransform = created;
                return created;
        }
    }

    private static ScaleTransform EnsureScale(UIElement el)
    {
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        switch (el.RenderTransform)
        {
            case ScaleTransform { IsFrozen: false } s:
                return s;
            case TransformGroup { IsFrozen: false } g:
                var existing = g.Children.OfType<ScaleTransform>().FirstOrDefault(x => !x.IsFrozen);
                if (existing != null)
                    return existing;
                var added = new ScaleTransform();
                g.Children.Add(added);
                return added;
            default:
                var created = new ScaleTransform();
                el.RenderTransform = created;
                return created;
        }
    }

    private static (ScaleTransform Scale, RotateTransform Rotate) EnsureScaleRotate(UIElement el)
    {
        if (el.RenderTransform is TransformGroup { IsFrozen: false } group && group.Children.Count == 2 &&
            group.Children[0] is ScaleTransform foundScale && group.Children[1] is RotateTransform foundRotate)
            return (foundScale, foundRotate);

        var scale = new ScaleTransform();
        var rotate = new RotateTransform();
        var newGroup = new TransformGroup();
        newGroup.Children.Add(scale);
        newGroup.Children.Add(rotate);
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = newGroup;
        return (scale, rotate);
    }

    private static void SetScaleRotate(ScaleTransform s, RotateTransform r, double scale, double angle)
    {
        s.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        s.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        r.BeginAnimation(RotateTransform.AngleProperty, null);
        s.ScaleX = scale;
        s.ScaleY = scale;
        r.Angle = angle;
    }

    private static void AnimateScale(ScaleTransform s, DoubleAnimation anim)
    {
        s.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        s.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private static object NewToken(DependencyObject el)
    {
        var token = new object();
        Tokens.AddOrUpdate(el, token);
        return token;
    }

    private static bool IsCurrent(DependencyObject el, object token)
        => Tokens.TryGetValue(el, out var current) && ReferenceEquals(current, token);

    private static Duration Ms(int ms) => new(TimeSpan.FromMilliseconds(ms));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
