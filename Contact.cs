namespace PhoneBook;

// Модел, който отговаря на един ред от таблица Contacts
public class Contact
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string GroupName { get; set; } = "";

    // Инициали за кръглия аватар в таблицата (напр. "Мария Иванова" -> "МИ")
    public string Initials => GetInitials(Name);

    public static string GetInitials(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";

        var first = char.ToUpper(parts[0][0]);
        if (parts.Length == 1)
            return first.ToString();

        var last = char.ToUpper(parts[^1][0]);
        return $"{first}{last}";
    }
}
