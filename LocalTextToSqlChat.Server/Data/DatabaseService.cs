using Microsoft.Data.Sqlite;
using LocalTextToSqlChat.Server.Models;
using LocalTextToSqlChat.Server.Services;
using System.Text;

namespace LocalTextToSqlChat.Server.Data;

public class DatabaseService
{
    private const string AppDbPath = "app.db";
    private const string CompanyDbPath = "local_company.db";
    
    public DatabaseService()
    {
        InitializeAppDatabase();
        InitializeCompanyDatabase();
    }
    
    private void InitializeAppDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        connection.Open();
        
        var createUsersTable = connection.CreateCommand();
        createUsersTable.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                IsAdmin INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );";
        createUsersTable.ExecuteNonQuery();
        
        var addIsAdminColumn = connection.CreateCommand();
        addIsAdminColumn.CommandText = @"
            ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0;";
        try { addIsAdminColumn.ExecuteNonQuery(); } catch { }
        
        var createChatMessagesTable = connection.CreateCommand();
        createChatMessagesTable.CommandText = @"
            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Message TEXT NOT NULL,
                Response TEXT NOT NULL,
                SqlQuery TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users (Id)
            );";
        createChatMessagesTable.ExecuteNonQuery();
    }
    
    private void InitializeCompanyDatabase()
    {
        bool isNewDatabase = !File.Exists(CompanyDbPath);
        
        using var connection = new SqliteConnection($"Data Source={CompanyDbPath}");
        connection.Open();
        
        if (isNewDatabase)
        {
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE Employees (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Department TEXT NOT NULL DEFAULT 'Unknown',
                    Salary INTEGER NOT NULL DEFAULT 0,
                    HireDate TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d','now')) CHECK (HireDate GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]')
                );";
            createTableCommand.ExecuteNonQuery();
            
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Employees (Name, Department, Salary, HireDate) VALUES
                ('Alice Johnson', 'Engineering', 95000, '2022-01-15'),
                ('Bob Smith', 'Sales', 82000, '2021-11-30'),
                ('Charlie Brown', 'Engineering', 110000, '2020-05-20'),
                ('Diana Prince', 'Sales', 78000, '2022-08-01'),
                ('Eve Adams', 'HR', 65000, '2023-02-10');";
            insertCommand.ExecuteNonQuery();
        }
        else
        {
            // Migrate existing database to add CHECK constraint if missing
            MigrateExistingDatabase(connection);
        }
    }
    
    private void MigrateExistingDatabase(SqliteConnection connection)
    {
        try
        {
            // Check if CHECK constraint already exists
            var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Employees'";
            using var reader = schemaCommand.ExecuteReader();
            
            if (reader.Read())
            {
                string currentSchema = reader.GetString(0);
                
                if (!currentSchema.Contains("CHECK (HireDate GLOB"))
                {
                    reader.Close();
                    
                    // Create new table with CHECK constraint
                    var createNewTable = connection.CreateCommand();
                    createNewTable.CommandText = @"
                        CREATE TABLE Employees_new (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            Department TEXT NOT NULL DEFAULT 'Unknown',
                            Salary INTEGER NOT NULL DEFAULT 0,
                            HireDate TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d','now')) CHECK (HireDate GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]')
                        )";
                    createNewTable.ExecuteNonQuery();
                    
                    // Copy existing data (this will fail if dates don't match the constraint)
                    var copyData = connection.CreateCommand();
                    copyData.CommandText = "INSERT INTO Employees_new SELECT * FROM Employees";
                    
                    try
                    {
                        copyData.ExecuteNonQuery();
                        
                        // Drop old table and rename new one
                        var dropOld = connection.CreateCommand();
                        dropOld.CommandText = "DROP TABLE Employees";
                        dropOld.ExecuteNonQuery();
                        
                        var renameNew = connection.CreateCommand();
                        renameNew.CommandText = "ALTER TABLE Employees_new RENAME TO Employees";
                        renameNew.ExecuteNonQuery();
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Migration failed due to invalid date formats: {ex.Message}");
                        Console.WriteLine("Will clean up dates first, then retry migration...");
                        
                        // Drop the new table since copy failed
                        try
                        {
                            var dropNew = connection.CreateCommand();
                            dropNew.CommandText = "DROP TABLE Employees_new";
                            dropNew.ExecuteNonQuery();
                        }
                        catch { }
                        
                        // Clean up dates in existing table first
                        CleanupDatesInExistingTable(connection);
                        
                        // Retry migration after cleanup
                        MigrateExistingDatabase(connection);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during database migration: {ex.Message}");
        }
    }
    
    private void CleanupDatesInExistingTable(SqliteConnection connection)
    {
        try
        {
            // Get all employees with their current HireDate values
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, Name, HireDate FROM Employees";
            
            var updates = new List<(int id, string name, string newDate)>();
            
            using var reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                string name = reader.GetString(1);
                string hireDate = reader.GetString(2);
                
                string normalizedDate = NormalizeDateToYyyyMmDd(hireDate);
                if (normalizedDate != hireDate)
                {
                    updates.Add((id, name, normalizedDate));
                }
            }
            reader.Close();
            
            // Apply updates
            foreach (var (id, name, newDate) in updates)
            {
                var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Employees SET HireDate = @hireDate WHERE Id = @id";
                updateCommand.Parameters.AddWithValue("@hireDate", newDate);
                updateCommand.Parameters.AddWithValue("@id", id);
                updateCommand.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning up date formats: {ex.Message}");
        }
    }
    
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Email, PasswordHash, IsAdmin, CreatedAt FROM Users WHERE Username = @username";
        command.Parameters.AddWithValue("@username", username);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                IsAdmin = reader.GetInt32(4) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(5))
            };
        }
        
        return null;
    }
    
    public async Task<User?> CreateUserAsync(string username, string email, string passwordHash)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Users";
        var userCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        
        var isFirstUser = userCount == 0;
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (Username, Email, PasswordHash, IsAdmin, CreatedAt) 
            VALUES (@username, @email, @passwordHash, @isAdmin, @createdAt);
            SELECT last_insert_rowid()";
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@passwordHash", passwordHash);
        command.Parameters.AddWithValue("@isAdmin", isFirstUser);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        
        var id = await command.ExecuteScalarAsync();
        if (id != null)
        {
            return new User
            {
                Id = Convert.ToInt32(id),
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                IsAdmin = isFirstUser,
                CreatedAt = DateTime.UtcNow
            };
        }
        
        return null;
    }
    
    public async Task SaveChatMessageAsync(int userId, string message, string response, string? sqlQuery)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ChatMessages (UserId, Message, Response, SqlQuery, CreatedAt) 
            VALUES (@userId, @message, @response, @sqlQuery, @createdAt)";
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@message", message);
        command.Parameters.AddWithValue("@response", response);
        command.Parameters.AddWithValue("@sqlQuery", sqlQuery ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        
        await command.ExecuteNonQueryAsync();
    }
    
    public string ExecuteQueryAndFormatResults(string sqlQuery)
    {
        var smartExecutor = new SmartSqlExecutor($"Data Source={CompanyDbPath}");
        return smartExecutor.ExecuteQueryAsync(sqlQuery).Result;
    }
    
    public string GetDatabaseSchema()
    {
        using var connection = new SqliteConnection($"Data Source={CompanyDbPath}");
        connection.Open();
        
        var schemaBuilder = new StringBuilder();
        
        // Get table schema
        var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Employees';";
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            schemaBuilder.AppendLine(reader.GetString(0));
        }
        reader.Close();
        
        // Add critical data quality warnings
        schemaBuilder.AppendLine();
        schemaBuilder.AppendLine("CRITICAL DATA QUALITY NOTES:");
        schemaBuilder.AppendLine("- Some records may have corrupted or missing data");
        schemaBuilder.AppendLine("- Department names may be truncated or merged with other text");
        schemaBuilder.AppendLine("- Date formats are inconsistent (ISO dates vs text dates)");
        schemaBuilder.AppendLine("- Use WHERE clauses with COALESCE and IS NOT NULL for safety");
        schemaBuilder.AppendLine("- Always include error handling for missing data");
        
        // Add sample data context with error handling
        schemaBuilder.AppendLine();
        schemaBuilder.AppendLine("Sample Data Context:");
        
        try 
        {
            var dataCommand = connection.CreateCommand();
            dataCommand.CommandText = "SELECT DISTINCT Department FROM Employees WHERE Department IS NOT NULL AND Department != '' ORDER BY Department";
            using var dataReader = dataCommand.ExecuteReaderAsync().Result;
            
            schemaBuilder.Append("Available Departments: ");
            var departments = new List<string>();
            while (dataReader.Read())
            {
                var dept = dataReader.GetString(0);
                if (!string.IsNullOrWhiteSpace(dept) && dept.Length < 50) // Filter out corrupted long strings
                {
                    departments.Add($"'{dept}'");
                }
            }
            schemaBuilder.AppendLine(string.Join(", ", departments));
            dataReader.Close();
        }
        catch (Exception ex)
        {
            schemaBuilder.AppendLine($"Error reading departments: {ex.Message}");
        }
        
        // Add count information with error handling
        try
        {
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) as TotalEmployees FROM Employees WHERE Name IS NOT NULL AND Name != ''";
            using var countReader = countCommand.ExecuteReader();
            if (countReader.Read())
            {
                schemaBuilder.AppendLine($"Total Valid Employee Records: {countReader.GetInt32(0)}");
            }
        }
        catch (Exception ex)
        {
            schemaBuilder.AppendLine($"Error counting employees: {ex.Message}");
        }
        
        return schemaBuilder.ToString();
    }
    
    
    private string NormalizeDateToYyyyMmDd(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return "2023-01-01"; // Default date
            
        // Already in correct format
        if (System.Text.RegularExpressions.Regex.IsMatch(dateString, @"^\d{4}-\d{2}-\d{2}$"))
            return dateString;
            
        // Try to parse various formats and convert to yyyy-mm-dd
        var formats = new[]
        {
            "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "MMMM dd, yyyy", 
            "MMMM d, yyyy", "MMM dd, yyyy", "MMM d, yyyy",
            "dd-MM-yyyy", "MM-dd-yyyy", "yyyy/MM/dd"
        };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
        }
        
        // Try general parsing as fallback
        if (DateTime.TryParse(dateString, out DateTime fallbackDate))
        {
            return fallbackDate.ToString("yyyy-MM-dd");
        }
        
        // Console.WriteLine($"Could not parse date: {dateString}, using default");
        return "2023-01-01"; // Fallback date
    }
    
    public static bool IsValidDateFormat(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return false;
            
        // Check yyyy-mm-dd pattern
        if (!System.Text.RegularExpressions.Regex.IsMatch(date, @"^\d{4}-\d{2}-\d{2}$"))
            return false;
            
        // Validate it's actually a valid date
        return DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _);
    }
    
    public static string ValidateAndNormalizeDateForInsert(string date)
    {
        if (IsValidDateFormat(date))
            return date;
            
        // Try to convert to correct format
        string normalized = new DatabaseService().NormalizeDateToYyyyMmDd(date);
        
        if (!IsValidDateFormat(normalized))
            throw new ArgumentException($"Invalid date format: '{date}'. Must be yyyy-mm-dd format.");
            
        return normalized;
    }
    
    public async Task<List<User>> GetAllUsersAsync()
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Email, PasswordHash, IsAdmin, CreatedAt FROM Users ORDER BY CreatedAt DESC";
        
        var users = new List<User>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                IsAdmin = reader.GetInt32(4) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(5))
            });
        }
        
        return users;
    }
    
    public async Task<User?> GetUserByIdAsync(int id)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Email, PasswordHash, IsAdmin, CreatedAt FROM Users WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                IsAdmin = reader.GetInt32(4) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(5))
            };
        }
        
        return null;
    }
    
    public async Task<bool> UpdateUserAsync(User user)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Users 
            SET Username = @username, Email = @email, IsAdmin = @isAdmin 
            WHERE Id = @id";
        command.Parameters.AddWithValue("@id", user.Id);
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@email", user.Email);
        command.Parameters.AddWithValue("@isAdmin", user.IsAdmin ? 1 : 0);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
    
    public async Task<bool> DeleteUserAsync(int id)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
    
    public async Task<bool> SetUserAdminAsync(int id, bool isAdmin)
    {
        using var connection = new SqliteConnection($"Data Source={AppDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET IsAdmin = @isAdmin WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@isAdmin", isAdmin ? 1 : 0);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
    
    public async Task<Employee?> AddEmployeeAsync(string name, string? department = null, decimal? salary = null, DateTime? hireDate = null)
    {
        var employee = new Employee(name, department, salary, hireDate);
        return await AddEmployeeAsync(employee);
    }
    
    public async Task<Employee?> AddEmployeeAsync(Employee employee)
    {
        using var connection = new SqliteConnection($"Data Source={CompanyDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        
        // Build INSERT statement dynamically based on which fields have non-default values
        var columns = new List<string> { "Name" };
        var values = new List<string> { "@name" };
        
        command.Parameters.AddWithValue("@name", employee.Name);
        
        if (employee.Department != "Unknown")
        {
            columns.Add("Department");
            values.Add("@department");
            command.Parameters.AddWithValue("@department", employee.Department);
        }
        
        if (employee.Salary != 0)
        {
            columns.Add("Salary");
            values.Add("@salary");
            command.Parameters.AddWithValue("@salary", (int)employee.Salary);
        }
        
        if (employee.HireDate.Date != DateTime.Now.Date)
        {
            columns.Add("HireDate");
            values.Add("@hireDate");
            command.Parameters.AddWithValue("@hireDate", employee.HireDate.ToString("yyyy-MM-dd"));
        }
        
        command.CommandText = $@"
            INSERT INTO Employees ({string.Join(", ", columns)}) 
            VALUES ({string.Join(", ", values)});
            SELECT last_insert_rowid();";
            
        var id = await command.ExecuteScalarAsync();
        if (id != null)
        {
            employee.Id = Convert.ToInt32(id);
            return employee;
        }
        
        return null;
    }
    
    public async Task<List<Employee>> GetAllEmployeesAsync()
    {
        using var connection = new SqliteConnection($"Data Source={CompanyDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Department, Salary, HireDate FROM Employees ORDER BY Name";
        
        var employees = new List<Employee>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            employees.Add(new Employee
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Department = reader.GetString(2),
                Salary = reader.GetInt32(3),
                HireDate = DateTime.Parse(reader.GetString(4))
            });
        }
        
        return employees;
    }
    
    public async Task<Employee?> GetEmployeeByIdAsync(int id)
    {
        using var connection = new SqliteConnection($"Data Source={CompanyDbPath}");
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Department, Salary, HireDate FROM Employees WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Employee
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Department = reader.GetString(2),
                Salary = reader.GetInt32(3),
                HireDate = DateTime.Parse(reader.GetString(4))
            };
        }
        
        return null;
    }
}