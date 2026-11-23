using Microsoft.Data.Sqlite;

namespace PhoneBook;

// Всички операции с SQLite базата (CRUD)
public static class Database
{
    private const string ConnectionString = "Data Source=contacts.db";

    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    // Създава таблицата при първо стартиране и добавя примерни контакти, ако е празна
    public static void Initialize()
    {
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Contacts (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL,
                Phone     TEXT NOT NULL,
                Email     TEXT,
                GroupName TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT COUNT(*) FROM Contacts";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        if (count == 0)
            Seed();
    }

    private static void Seed()
    {
        var samples = new[]
        {
            new Contact { Name = "Мария Иванова",     Phone = "0888 123 456",     Email = "maria.ivanova@mail.bg", GroupName = "Семейство" },
            new Contact { Name = "Георги Петров",     Phone = "+359 887 654 321", Email = "g.petrov@firma.bg",     GroupName = "Работа" },
            new Contact { Name = "Елена Димитрова",   Phone = "0899 111 222",     Email = "elena.d@gmail.com",     GroupName = "Приятели" },
            new Contact { Name = "Николай Стоянов",   Phone = "02 987 6543",      Email = "",                      GroupName = "Работа" },
            new Contact { Name = "Десислава Колева",  Phone = "0878 555 010",     Email = "desi.koleva@abv.bg",    GroupName = "Приятели" },
            new Contact { Name = "Автосервиз Тракия", Phone = "0885 300 400",     Email = "",                      GroupName = "Други" }
        };
        foreach (var c in samples)
            Add(c);
    }

    // READ
    public static List<Contact> GetAll()
    {
        var list = new List<Contact>();
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Phone, Email, GroupName FROM Contacts ORDER BY Name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Contact
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Phone = reader.GetString(2),
                Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                GroupName = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return list;
    }

    // CREATE – връща Id на новия запис (нужен е, за да се оцвети новият ред в таблицата)
    public static int Add(Contact c)
    {
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Contacts (Name, Phone, Email, GroupName) VALUES ($name, $phone, $email, $group)";
        AddParameters(cmd, c);
        cmd.ExecuteNonQuery();

        var idCmd = connection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    // UPDATE
    public static void Update(Contact c)
    {
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Contacts SET Name = $name, Phone = $phone, Email = $email, GroupName = $group WHERE Id = $id";
        AddParameters(cmd, c);
        cmd.Parameters.AddWithValue("$id", c.Id);
        cmd.ExecuteNonQuery();
    }

    // DELETE
    public static void Delete(int id)
    {
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Contacts WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand cmd, Contact c)
    {
        cmd.Parameters.AddWithValue("$name", c.Name);
        cmd.Parameters.AddWithValue("$phone", c.Phone);
        cmd.Parameters.AddWithValue("$email", c.Email);
        cmd.Parameters.AddWithValue("$group", c.GroupName);
    }
}
