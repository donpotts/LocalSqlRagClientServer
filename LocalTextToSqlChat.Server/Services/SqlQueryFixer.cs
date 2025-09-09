using System.Text.RegularExpressions;

namespace LocalTextToSqlChat.Server.Services;

public static class SqlQueryFixer
{
    public static string FixInsertQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return sqlQuery;
            
        // Fix malformed INSERT queries where column count doesn't match value count
        var insertPattern = @"INSERT\s+INTO\s+Employees\s*\(([^)]+)\)\s*VALUES\s*\(([^)]*)\)";
        var match = Regex.Match(sqlQuery, insertPattern, RegexOptions.IgnoreCase);
        
        if (!match.Success)
            return sqlQuery;
            
        var columnsText = match.Groups[1].Value.Trim();
        var valuesText = match.Groups[2].Value.Trim();
        
        // Parse columns and values
        var columns = columnsText.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
        var values = ParseValues(valuesText);
        
        // If column count matches value count, query is fine
        if (columns.Count == values.Count)
            return sqlQuery;
            
        Console.WriteLine($"SqlQueryFixer: Found mismatch - {columns.Count} columns, {values.Count} values");
        Console.WriteLine($"Columns: [{string.Join(", ", columns)}]");
        Console.WriteLine($"Values: [{string.Join(", ", values)}]");
        
        // Fix the query by matching columns to values
        if (values.Count == 1 && columns.Count > 1)
        {
            // Case: Multiple columns but only one value - assume it's just Name
            var fixedQuery = $"INSERT INTO Employees (Name) VALUES ({values[0]})";
            Console.WriteLine($"Fixed to: {fixedQuery}");
            return fixedQuery;
        }
        else if (values.Count > 1 && columns.Count > values.Count)
        {
            // Case: More columns than values - trim columns to match values
            var trimmedColumns = columns.Take(values.Count);
            var fixedQuery = $"INSERT INTO Employees ({string.Join(", ", trimmedColumns)}) VALUES ({string.Join(", ", values)})";
            Console.WriteLine($"Fixed to: {fixedQuery}");
            return fixedQuery;
        }
        else if (columns.Count > 0 && values.Count == 0)
        {
            // Case: Columns but no values - this is completely broken, default to name-only insert
            var fixedQuery = "INSERT INTO Employees (Name) VALUES ('Unknown')";
            Console.WriteLine($"Fixed to: {fixedQuery}");
            return fixedQuery;
        }
        
        // If we can't fix it, return the original
        Console.WriteLine("SqlQueryFixer: Could not fix query, returning original");
        return sqlQuery;
    }
    
    private static List<string> ParseValues(string valuesText)
    {
        if (string.IsNullOrWhiteSpace(valuesText))
            return new List<string>();
            
        var values = new List<string>();
        var current = "";
        var inQuotes = false;
        var quoteChar = '\'';
        
        for (int i = 0; i < valuesText.Length; i++)
        {
            char c = valuesText[i];
            
            if (!inQuotes && (c == '\'' || c == '"'))
            {
                inQuotes = true;
                quoteChar = c;
                current += c;
            }
            else if (inQuotes && c == quoteChar)
            {
                inQuotes = false;
                current += c;
            }
            else if (!inQuotes && c == ',')
            {
                if (!string.IsNullOrWhiteSpace(current))
                {
                    values.Add(current.Trim());
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(current))
        {
            values.Add(current.Trim());
        }
        
        return values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
    }
}