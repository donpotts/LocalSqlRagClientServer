using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.RegularExpressions;

namespace LocalTextToSqlChat.Server.Services;

public class SmartSqlExecutor
{
    private readonly string _connectionString;

    public SmartSqlExecutor(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> ExecuteQueryAsync(string sqlQuery)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Fix common SQLite incompatibility issues
            sqlQuery = FixSqliteIncompatibilities(sqlQuery);

            var queryType = GetQueryType(sqlQuery);

            switch (queryType)
            {
                case SqlQueryType.Select:
                    return await ExecuteSelectQuery(connection, sqlQuery);

                case SqlQueryType.Insert:
                    return await ExecuteInsertQuery(connection, sqlQuery);

                case SqlQueryType.Update:
                    return await ExecuteUpdateQuery(connection, sqlQuery);

                case SqlQueryType.Delete:
                    return await ExecuteDeleteQuery(connection, sqlQuery);

                default:
                    return FormatResults("Unsupported query type.");
            }
        }
        catch (Exception ex)
        {
            return FormatResults($"Error executing query: {ex.Message}");
        }
    }

    private SqlQueryType GetQueryType(string sqlQuery)
    {
        var trimmed = sqlQuery.Trim().ToUpperInvariant();
        
        if (trimmed.StartsWith("SELECT"))
            return SqlQueryType.Select;
        if (trimmed.StartsWith("INSERT"))
            return SqlQueryType.Insert;
        if (trimmed.StartsWith("UPDATE"))
            return SqlQueryType.Update;
        if (trimmed.StartsWith("DELETE"))
            return SqlQueryType.Delete;
            
        return SqlQueryType.Unknown;
    }

    private async Task<string> ExecuteSelectQuery(SqliteConnection connection, string sqlQuery)
    {
        var command = connection.CreateCommand();
        command.CommandText = sqlQuery;

        using var reader = await command.ExecuteReaderAsync();
        return FormatSelectResults(reader);
    }

    private async Task<string> ExecuteInsertQuery(SqliteConnection connection, string sqlQuery)
    {
        // Validate and normalize any dates in the query before execution
        string validatedQuery = ValidateDateFormatsInQuery(sqlQuery);
        
        var command = connection.CreateCommand();
        command.CommandText = validatedQuery;

        var rowsAffected = await command.ExecuteNonQueryAsync();
        
        if (rowsAffected > 0)
        {
            // Get the inserted record ID
            var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid()";
            var result = await idCommand.ExecuteScalarAsync();
            
            if (result != null && long.TryParse(result.ToString(), out long insertedId))
            {
                return await GetInsertedRecord(connection, insertedId);
            }
        }

        return FormatResults("Record created successfully.");
    }

    private async Task<string> ExecuteUpdateQuery(SqliteConnection connection, string sqlQuery)
    {
        // Validate and normalize any dates in the query before execution
        string validatedQuery = ValidateDateFormatsInQuery(sqlQuery);
        
        // First, get the records that will be affected
        var selectQuery = ConvertUpdateToSelect(validatedQuery);
        var beforeUpdate = "";
        
        if (!string.IsNullOrEmpty(selectQuery))
        {
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = selectQuery;
            
            try
            {
                using var selectReader = await selectCommand.ExecuteReaderAsync();
                beforeUpdate = FormatSelectResults(selectReader);
            }
            catch
            {
                beforeUpdate = "Could not preview affected records.";
            }
        }

        // Execute the update
        var command = connection.CreateCommand();
        command.CommandText = validatedQuery;
        var rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            // Get the updated records to show the changes
            if (!string.IsNullOrEmpty(selectQuery))
            {
                try
                {
                    var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = selectQuery;
                    
                    using var selectReader = await selectCommand.ExecuteReaderAsync();
                    var afterUpdate = FormatSelectResults(selectReader);
                    
                    return FormatResults($"Successfully updated {rowsAffected} record(s).\n\nUpdated records:\n{afterUpdate}");
                }
                catch
                {
                    return FormatResults($"Successfully updated {rowsAffected} record(s).");
                }
            }

            return FormatResults($"Successfully updated {rowsAffected} record(s).");
        }

        return FormatResults("No records were updated. The specified record may not exist.");
    }

    private async Task<string> ExecuteDeleteQuery(SqliteConnection connection, string sqlQuery)
    {
        // First, get the records that will be deleted
        var selectQuery = ConvertDeleteToSelect(sqlQuery);
        var recordsToDelete = "";
        
        if (!string.IsNullOrEmpty(selectQuery))
        {
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = selectQuery;
            
            try
            {
                using var selectReader = await selectCommand.ExecuteReaderAsync();
                recordsToDelete = FormatSelectResults(selectReader);
            }
            catch
            {
                recordsToDelete = "Could not preview records to delete.";
            }
        }

        // Execute the delete
        var command = connection.CreateCommand();
        command.CommandText = sqlQuery;
        var rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return FormatResults($"Successfully deleted {rowsAffected} record(s).\n\nDeleted records were:\n{recordsToDelete}");
        }

        return FormatResults("No records were deleted. The specified record may not exist.");
    }

    private string ConvertUpdateToSelect(string updateQuery)
    {
        try
        {
            // Extract table name and WHERE clause from UPDATE statement
            var match = Regex.Match(updateQuery, @"UPDATE\s+(\w+)\s+SET\s+.*?(\s+WHERE\s+.*?)(\s*;?\s*)$", RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var tableName = match.Groups[1].Value;
                var whereClause = match.Groups[2].Value;
                return $"SELECT * FROM {tableName}{whereClause}";
            }
            
            return "";
        }
        catch
        {
            return "";
        }
    }

    private string ConvertDeleteToSelect(string deleteQuery)
    {
        try
        {
            // Extract table name and WHERE clause from DELETE statement
            var match = Regex.Match(deleteQuery, @"DELETE\s+FROM\s+(\w+)(\s+WHERE\s+.*?)(\s*;?\s*)$", RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var tableName = match.Groups[1].Value;
                var whereClause = match.Groups[2].Value;
                return $"SELECT * FROM {tableName}{whereClause}";
            }
            
            return "";
        }
        catch
        {
            return "";
        }
    }

    private async Task<string> GetInsertedRecord(SqliteConnection connection, long insertedId)
    {
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Employees WHERE Id = @id";
            command.Parameters.AddWithValue("@id", insertedId);

            using var reader = await command.ExecuteReaderAsync();
            return FormatResults($"Successfully created new record:\n{FormatSelectResults(reader)}");
        }
        catch
        {
            return FormatResults("Record created successfully.");
        }
    }

    private string FormatSelectResults(SqliteDataReader reader)
    {
        var resultBuilder = new StringBuilder();
        
        // Get column headers with improved formatting
        var columnNames = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var rawColumnName = reader.GetName(i);
            var formattedColumnName = FormatColumnHeader(rawColumnName);
            columnNames.Add(formattedColumnName);
        }
        
        // Create markdown table header
        resultBuilder.AppendLine("| " + string.Join(" | ", columnNames) + " |");
        resultBuilder.AppendLine("|" + string.Join("|", columnNames.Select(c => "----")) + "|");
        
        // Add data rows
        while (reader.Read())
        {
            var values = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                var columnName = columnNames[i];
                var formattedValue = FormatColumnValue(value, columnName);
                values.Add(formattedValue);
            }
            resultBuilder.AppendLine("| " + string.Join(" | ", values) + " |");
        }
        
        return resultBuilder.ToString();
    }

    private string FormatColumnValue(object? value, string columnName)
    {
        if (value == null || value == DBNull.Value)
            return "";
            
        string stringValue = value.ToString() ?? "";
        
        // Check if this is a salary-related column
        if (IsSalaryColumn(columnName) && decimal.TryParse(stringValue, out decimal salaryValue))
        {
            return salaryValue.ToString("C0"); // Currency format without cents
        }
        
        return stringValue;
    }
    
    private bool IsSalaryColumn(string columnName)
    {
        var lowerColumnName = columnName.ToLowerInvariant();
        return lowerColumnName.Contains("salary") || 
               lowerColumnName.Contains("wage") ||
               lowerColumnName.Contains("pay") ||
               lowerColumnName.Contains("income") ||
               lowerColumnName.Contains("average") && lowerColumnName.Contains("salary") ||
               lowerColumnName == "averageengineeringsalary" ||
               lowerColumnName.StartsWith("avg") && lowerColumnName.Contains("salary");
    }

    private string FormatColumnHeader(string columnName)
    {
        // Add spaces before capital letters in camelCase/PascalCase names
        // Example: "AverageSalesSalary" becomes "Average Sales Salary"
        if (string.IsNullOrEmpty(columnName))
            return columnName;
            
        var result = Regex.Replace(columnName, @"(?<!^)(?=[A-Z][a-z])|(?<=[a-z])(?=[A-Z])", " ");
        
        // Capitalize first letter if it isn't already
        if (result.Length > 0 && char.IsLower(result[0]))
            result = char.ToUpper(result[0]) + result.Substring(1);
            
        return result;
    }

    private string FormatResults(string message)
    {
        return message;
    }
    
    private string ValidateDateFormatsInQuery(string sqlQuery)
    {
        // For INSERT queries, find and validate/normalize any date values
        if (sqlQuery.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateInsertDates(sqlQuery);
        }
        
        // For UPDATE queries, find and validate/normalize any HireDate SET clauses
        if (sqlQuery.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateUpdateDates(sqlQuery);
        }
        
        return sqlQuery; // No validation needed for other query types
    }
    
    private string ValidateInsertDates(string sqlQuery)
    {
        try
        {
            // Look for HireDate values in INSERT statements
            // Pattern: INSERT INTO Employees (..., HireDate, ...) VALUES (..., 'date', ...)
            var pattern = @"INSERT\s+INTO\s+Employees\s*\([^)]*HireDate[^)]*\)\s*VALUES\s*\([^)]*\)";
            
            if (System.Text.RegularExpressions.Regex.IsMatch(sqlQuery, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                // Extract and validate dates in VALUES clause
                var valuesMatch = System.Text.RegularExpressions.Regex.Match(sqlQuery, @"VALUES\s*\(([^)]+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (valuesMatch.Success)
                {
                    var valuesClause = valuesMatch.Groups[1].Value;
                    var values = valuesClause.Split(',').Select(v => v.Trim().Trim('\'')).ToArray();
                    
                    // Assume last value is HireDate (based on schema order)
                    if (values.Length >= 4)
                    {
                        var dateValue = values[values.Length - 1];
                        var validatedDate = Data.DatabaseService.ValidateAndNormalizeDateForInsert(dateValue);
                        
                        // Replace the date in the original query
                        if (validatedDate != dateValue)
                        {
                            sqlQuery = sqlQuery.Replace($"'{dateValue}'", $"'{validatedDate}'");
                        }
                    }
                }
            }
            
            return sqlQuery;
        }
        catch (Exception ex)
        {
            // Console.WriteLine($"Error validating INSERT dates: {ex.Message}");
            throw new InvalidOperationException($"Invalid date format in INSERT query: {ex.Message}");
        }
    }
    
    private string ValidateUpdateDates(string sqlQuery)
    {
        try
        {
            // Look for HireDate = 'date' in UPDATE statements
            var hireDatePattern = @"HireDate\s*=\s*'([^']+)'";
            var matches = System.Text.RegularExpressions.Regex.Matches(sqlQuery, hireDatePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var originalDate = match.Groups[1].Value;
                var validatedDate = Data.DatabaseService.ValidateAndNormalizeDateForInsert(originalDate);
                
                if (validatedDate != originalDate)
                {
                    sqlQuery = sqlQuery.Replace($"'{originalDate}'", $"'{validatedDate}'");
                }
            }
            
            return sqlQuery;
        }
        catch (Exception ex)
        {
            // Console.WriteLine($"Error validating UPDATE dates: {ex.Message}");
            throw new InvalidOperationException($"Invalid date format in UPDATE query: {ex.Message}");
        }
    }
    
    private string FixSqliteIncompatibilities(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;
            
        // Fix YEAR() function calls - replace with LIKE pattern for yyyy-mm-dd dates
        var yearFunctionPattern = @"YEAR\s*\(\s*([^)]+)\s*\)\s*=\s*(\d{4})";
        sql = Regex.Replace(sql, yearFunctionPattern, "$1 LIKE '$2-%'", RegexOptions.IgnoreCase);
        
        // Fix MONTH() function calls with zero-padding
        var monthFunctionPattern = @"MONTH\s*\(\s*([^)]+)\s*\)\s*=\s*(\d{1,2})";
        sql = Regex.Replace(sql, monthFunctionPattern, match =>
        {
            string column = match.Groups[1].Value;
            int month = int.Parse(match.Groups[2].Value);
            string paddedMonth = month.ToString("D2");
            return $"{column} LIKE '____-{paddedMonth}-%'";
        }, RegexOptions.IgnoreCase);
        
        return sql;
    }
}

public enum SqlQueryType
{
    Select,
    Insert,
    Update,
    Delete,
    Unknown
}