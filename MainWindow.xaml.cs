using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PhoneBook;

public partial class MainWindow : Window
{
    private const string AllGroups = "Всички";
    private static readonly string[] Groups = { "Семейство", "Работа", "Приятели", "Други" };

    // Телефон: по желание + в началото, след това цифри, интервали, тирета и скоби
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9 ()\-]{6,20}$");
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private List<Contact> _all = new();      // всички контакти от базата
    private string _groupFilter = AllGroups; // избраната група във филтъра
    private Contact? _editing;               // null = добавяме нов контакт
    private bool _suppressSelection;         // спира SelectionChanged при програмна смяна
    private bool _animateRows;               // true само веднага след смяна на списъка (филтър/търсене)
    private bool _deleteArmed;               // първо натискане на „Изтрий“ → „Потвърди“
    private bool _themeAnimating;            // пази от двойно натискане по време на смяната на темата
    private readonly DispatcherTimer _disarmTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public MainWindow()
    {
        InitializeComponent();

        cbGroup.ItemsSource = Groups;
        _disarmTimer.Tick += (_, _) => DisarmDelete();

        // Тъмна/светла заглавна лента на Windows, щом прозорецът получи handle
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        UpdateThemeToggle(animate: false);

        LoadData();
        ResetForm();
    }

    // ================= ДАННИ И ФИЛТРИРАНЕ =================

    private void LoadData(bool animateRows = true)
    {
        _all = Database.GetAll();
        ApplyFilter(animateRows);
    }

    private void ApplyFilter(bool animateRows = true)
    {
        // Събитията на филтъра може да дойдат още по време на InitializeComponent
        if (dgContacts == null || txtSearch == null)
            return;

        IEnumerable<Contact> query = _all;

        if (_groupFilter != AllGroups)
            query = query.Where(c => c.GroupName == _groupFilter);

        var search = txtSearch.Text.Trim();
        if (search.Length > 0)
        {
            query = query.Where(c =>
                c.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                c.Phone.Contains(search) ||
                c.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();

        // Редовете, създадени веднага след смяната на списъка, се появяват каскадно.
        // След като WPF ги изгради, флагът се изключва, за да не се анимират при скролиране.
        _animateRows = animateRows;
        _suppressSelection = true;
        dgContacts.ItemsSource = list;
        if (_editing != null)
            dgContacts.SelectedItem = list.FirstOrDefault(c => c.Id == _editing.Id);
        _suppressSelection = false;
        Dispatcher.InvokeAsync(() => _animateRows = false, DispatcherPriority.ContextIdle);

        var countText = list.Count == 1 ? "1 контакт" : $"{list.Count} контакта";
        if (txtCount.Text != countText)
        {
            txtCount.Text = countText;
            if (IsLoaded)
                Motion.Pop(countBadge);
        }
        emptyState.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        _groupFilter = (sender as RadioButton)?.Content as string ?? AllGroups;
        ApplyFilter();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    // Каскадно появяване на редовете (до 10 реда със стъпка 22 ms)
    private void DgContacts_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (_animateRows)
            Motion.FadeUp(e.Row, Math.Min(e.Row.GetIndex(), 10) * 22);
        else
            Motion.ClearFadeUp(e.Row);
    }

    // Кратко зелено оцветяване на добавения/редактирания ред
    private void FlashRow(int id)
    {
        if (dgContacts.ItemsSource is not List<Contact> list)
            return;

        var item = list.FirstOrDefault(c => c.Id == id);
        if (item == null)
            return;

        dgContacts.ScrollIntoView(item);
        Dispatcher.InvokeAsync(() =>
        {
            if (dgContacts.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
                return;

            var from = ((SolidColorBrush)FindResource("FlashBrush")).Color;
            var to = row.IsSelected
                ? ((SolidColorBrush)FindResource("RowSelectedBrush")).Color
                : Color.FromArgb(0, from.R, from.G, from.B);
            Motion.Flash(row, from, to);
        }, DispatcherPriority.Loaded);
    }

    // ================= ФОРМА =================

    private void DgContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
            return;

        if (dgContacts.SelectedItem is Contact contact)
        {
            LoadIntoForm(contact);
            Motion.SlideRefresh(editorContent);
        }
    }

    private void LoadIntoForm(Contact c)
    {
        _editing = c;
        txtName.Text = c.Name;
        txtPhone.Text = c.Phone;
        txtEmail.Text = c.Email;
        cbGroup.SelectedItem = Groups.Contains(c.GroupName) ? c.GroupName : "Други";

        txtFormTitle.Text = "Редактиране на контакт";
        txtFormSubtitle.Text = $"Контакт № {c.Id}. Променете данните и натиснете „Запази промените“.";
        btnSave.Content = "Запази промените";
        btnSave.Style = (Style)FindResource("SaveButton");
        btnDelete.IsEnabled = true;
        DisarmDelete();
        ClearErrors();
    }

    private void ResetForm()
    {
        _editing = null;
        txtName.Text = "";
        txtPhone.Text = "";
        txtEmail.Text = "";
        cbGroup.SelectedItem = "Други";

        txtFormTitle.Text = "Нов контакт";
        txtFormSubtitle.Text = "Попълнете данните и натиснете „Добави контакт“.";
        btnSave.Content = "Добави контакт";
        btnSave.Style = (Style)FindResource("PrimaryButton");
        btnDelete.IsEnabled = false;
        DisarmDelete();
        ClearErrors();

        _suppressSelection = true;
        dgContacts.SelectedItem = null;
        _suppressSelection = false;

        txtName.Focus();
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
        Motion.SlideRefresh(editorContent);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateForm())
            return;

        var contact = new Contact
        {
            Id = _editing?.Id ?? 0,
            Name = txtName.Text.Trim(),
            Phone = txtPhone.Text.Trim(),
            Email = txtEmail.Text.Trim(),
            GroupName = cbGroup.SelectedItem as string ?? "Други"
        };

        if (_editing == null)
        {
            var newId = Database.Add(contact);
            ResetForm();
            LoadData(animateRows: false);
            ShowToast("Контактът е добавен!");
            FlashRow(newId);
        }
        else
        {
            Database.Update(contact);
            LoadData(animateRows: false);
            // Зареждаме обновения обект, за да остане избран
            var updated = _all.FirstOrDefault(c => c.Id == contact.Id);
            if (updated != null)
                LoadIntoForm(updated);
            ApplyFilter(animateRows: false);
            ShowToast("Контактът е запазен!");
            FlashRow(contact.Id);
        }
    }

    // Изтриване на две стъпки (вместо системен MessageBox, който е бял и чупи тъмната тема):
    // 1-во натискане → бутонът става червен „Потвърди“ за 3 секунди; 2-ро натискане → изтрива.
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null)
            return;

        if (!_deleteArmed)
        {
            _deleteArmed = true;
            btnDelete.Content = "Потвърди";
            btnDelete.Style = (Style)FindResource("DangerSolidButton");
            btnDelete.ToolTip = "Натиснете отново, за да изтриете контакта";
            Motion.Pop(btnDelete, 1.08);
            _disarmTimer.Stop();
            _disarmTimer.Start();
            return;
        }

        DisarmDelete();
        Database.Delete(_editing.Id);
        ResetForm();
        LoadData(animateRows: false);
        Motion.SlideRefresh(editorContent);
        ShowToast("Контактът е изтрит.");
    }

    private void DisarmDelete()
    {
        _disarmTimer.Stop();
        if (!_deleteArmed)
            return;

        _deleteArmed = false;
        btnDelete.Content = "Изтрий";
        btnDelete.Style = (Style)FindResource("DangerButton");
        btnDelete.ToolTip = null;
    }

    // ================= ВАЛИДАЦИЯ =================

    private bool ValidateForm()
    {
        ClearErrors();
        var errors = new List<string>();
        var invalid = new List<TextBox>();

        var name = txtName.Text.Trim();
        if (name.Length < 2)
        {
            errors.Add("Въведете име (поне 2 символа).");
            invalid.Add(txtName);
        }

        var phone = txtPhone.Text.Trim();
        if (phone.Length == 0)
        {
            errors.Add("Въведете телефон.");
            invalid.Add(txtPhone);
        }
        else if (!PhoneRegex.IsMatch(phone) || phone.Count(char.IsDigit) < 6)
        {
            errors.Add("Невалиден телефон. Пример: 0888 123 456 или +359 888 123 456.");
            invalid.Add(txtPhone);
        }

        var email = txtEmail.Text.Trim();
        if (email.Length > 0 && !EmailRegex.IsMatch(email))
        {
            errors.Add("Имейл адресът не е валиден.");
            invalid.Add(txtEmail);
        }

        if (errors.Count == 0)
            return true;

        // Червено поле + поклащане + плавно появяване на лентата с грешката
        foreach (var box in invalid)
        {
            box.Tag = "Error";
            Motion.Shake(box);
        }
        txtError.Text = string.Join(Environment.NewLine, errors);
        Motion.FadeIn(errorBanner);
        return false;
    }

    // Грешката изчезва плавно, щом потребителят оправи всички полета
    private void Field_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box && box.Tag as string == "Error")
            box.Tag = null;

        if (txtName?.Tag == null && txtPhone?.Tag == null && txtEmail?.Tag == null && errorBanner != null)
            Motion.FadeOutCollapse(errorBanner);
    }

    private void ClearErrors()
    {
        txtName.Tag = null;
        txtPhone.Tag = null;
        txtEmail.Tag = null;
        Motion.HideNow(errorBanner);
    }

    // ================= TOAST =================

    private void ShowToast(string message)
    {
        txtToast.Text = message;
        ((Storyboard)FindResource("ToastStoryboard")).Begin(this);
    }

    // ================= ТЕМА =================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        if (_themeAnimating)
            return;

        _themeAnimating = true;
        var origin = btnTheme.TranslatePoint(new Point(btnTheme.ActualWidth / 2, btnTheme.ActualHeight / 2), shell);
        Motion.CircularReveal(shell, themeOverlay, origin,
            swap: () =>
            {
                ThemeManager.Toggle();
                UpdateThemeToggle(animate: true);
            },
            done: () => _themeAnimating = false);
    }

    // Тъмна тема → луна; светла тема → слънце
    private void UpdateThemeToggle(bool animate)
    {
        var dark = ThemeManager.Current == AppTheme.Dark;
        Motion.SwapIcon(show: dark ? moonIcon : sunIcon, hide: dark ? sunIcon : moonIcon, animate);
        txtThemeLabel.Text = dark ? "Тъмна тема" : "Светла тема";
        btnTheme.ToolTip = dark ? "Превключи към светла тема" : "Превключи към тъмна тема";
    }
}
