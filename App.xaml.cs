using System.Windows;

namespace PhoneBook;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Database.Initialize();     // създава contacts.db и таблицата, ако липсват
        ThemeManager.Initialize(); // зарежда последната избрана тема (по подразбиране – тъмна)
        base.OnStartup(e);
    }
}
