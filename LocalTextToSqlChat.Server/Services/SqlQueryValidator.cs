using System.Text.RegularExpressions;

namespace LocalTextToSqlChat.Server.Services;

public class SqlQueryValidator
{
    private static readonly HashSet<string> _readOnlyCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "WITH", "SHOW", "DESCRIBE", "EXPLAIN", "PRAGMA"
    };
    
    private static readonly HashSet<string> _writeCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", "REPLACE", "MERGE"
    };
    
    public static bool IsReadOnlyQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return false;
            
        // Clean markdown formatting and normalize whitespace
        string cleanQuery = CleanSqlQuery(sqlQuery);
        
        // Split by semicolon to handle multiple statements
        var statements = cleanQuery.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var statement in statements)
        {
            if (!IsStatementReadOnly(statement.Trim()))
                return false;
        }
        
        return true;
    }
    
    private static bool IsStatementReadOnly(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return true;
            
        // Get the first word (command)
        var words = statement.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return true;
            
        string firstCommand = words[0].ToUpperInvariant();
        
        // Check if it's a known write command
        if (_writeCommands.Contains(firstCommand))
            return false;
            
        // Check if it's a known read-only command
        if (_readOnlyCommands.Contains(firstCommand))
            return true;
            
        // Be conservative - if we don't recognize the command, treat it as potentially unsafe
        return false;
    }
    
    private static string CleanSqlQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;
            
        // Remove markdown code block formatting
        sql = Regex.Replace(sql, @"```(?:sql|SQL)?\s*", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"```\s*", "");
        
        // Remove comments
        sql = RemoveComments(sql.Trim());
        
        return sql.Trim();
    }
    
    private static string RemoveComments(string sql)
    {
        // Remove single-line comments (-- style)
        sql = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline);
        
        // Remove multi-line comments (/* */ style)
        sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);
        
        return sql;
    }
    
    public static string GetQueryValidationError(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return "Empty query is not allowed.";
            
        string cleanQuery = CleanSqlQuery(sqlQuery);
        var statements = cleanQuery.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var statement in statements)
        {
            var trimmedStatement = statement.Trim();
            if (string.IsNullOrWhiteSpace(trimmedStatement))
                continue;
                
            var words = trimmedStatement.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                continue;
                
            string firstCommand = words[0].ToUpperInvariant();
            
            if (_writeCommands.Contains(firstCommand))
            {
                return $"Write operations ({firstCommand}) are not allowed. Only SELECT queries are permitted for non-admin users.";
            }
            
            if (!_readOnlyCommands.Contains(firstCommand))
            {
                return $"Command '{firstCommand}' is not recognized as a safe read-only operation. Only SELECT queries are permitted for non-admin users.";
            }
        }
        
        return string.Empty; // No validation errors
    }
}