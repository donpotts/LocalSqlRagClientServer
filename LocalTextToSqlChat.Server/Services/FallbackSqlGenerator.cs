using System.Text.RegularExpressions;

namespace LocalTextToSqlChat.Server.Services;

public static class FallbackSqlGenerator
{
    public static (bool handled, string sql) TryGenerateInsertSql(string userInput)
    {
        var input = userInput.ToLowerInvariant().Trim();
        
        // Pattern: "add employee [name]" or "add new employee [name]"
        var addEmployeePattern = @"add\s+(?:new\s+)?employee\s+([a-zA-Z\s]+?)(?:\s+to\s+([a-zA-Z\s]+?))?(?:\s+with\s+salary\s+(\d+))?$";
        var match = Regex.Match(input, addEmployeePattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            var department = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
            var salary = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            
            // Build INSERT statement
            var columns = new List<string> { "Name" };
            var values = new List<string> { $"'{name}'" };
            
            if (!string.IsNullOrEmpty(department))
            {
                columns.Add("Department");
                values.Add($"'{department}'");
            }
            
            if (!string.IsNullOrEmpty(salary))
            {
                columns.Add("Salary");
                values.Add(salary);
            }
            
            var sql = $"INSERT INTO Employees ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
            Console.WriteLine($"FallbackSqlGenerator: Generated {sql}");
            return (true, sql);
        }
        
        return (false, string.Empty);
    }
    
    public static (bool handled, string sql) TryGenerateUpdateSql(string userInput)
    {
        var input = userInput.ToLowerInvariant().Trim();
        
        // Pattern: "assign [name] to [department]" or "move [name] to [department]" etc.
        var assignPattern = @"(?:assign|move|transfer)\s+([a-zA-Z\s]+?)\s+to\s+(?:the\s+)?([a-zA-Z\s]+?)(?:\s+department)?(?:\s+and\s+give\s+(?:him|her|them)\s+a\s+salary\s+of\s+(\d+))?$";
        var match = Regex.Match(input, assignPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            var department = match.Groups[2].Value.Trim();
            var salary = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            
            var setParts = new List<string> { $"Department = '{department}'" };
            if (!string.IsNullOrEmpty(salary))
            {
                setParts.Add($"Salary = {salary}");
            }
            
            var sql = $"UPDATE Employees SET {string.Join(", ", setParts)} WHERE Name = '{name}'";
            Console.WriteLine($"FallbackSqlGenerator: Generated {sql}");
            return (true, sql);
        }
        
        // Pattern: "give [name] a raise to [amount]"
        var raisePattern = @"give\s+([a-zA-Z\s]+?)\s+a\s+raise\s+to\s+(\d+)$";
        match = Regex.Match(input, raisePattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            var salary = match.Groups[2].Value.Trim();
            
            var sql = $"UPDATE Employees SET Salary = {salary} WHERE Name = '{name}'";
            Console.WriteLine($"FallbackSqlGenerator: Generated {sql}");
            return (true, sql);
        }
        
        return (false, string.Empty);
    }
    
    public static (bool handled, string sql) TryGenerateDeleteSql(string userInput)
    {
        var input = userInput.ToLowerInvariant().Trim();
        
        // Pattern: "fire [name]" or "[name] has been fired" etc.
        var deletePatterns = new[]
        {
            @"(?:fire|terminate|dismiss)\s+([a-zA-Z\s]+?)$",
            @"([a-zA-Z\s]+?)\s+(?:has\s+been\s+)?(?:fired|terminated|dismissed|laid\s+off|let\s+go)(?:\s+.*)?$",
            @"(?:fire|terminate|dismiss|remove)\s+employee\s+([a-zA-Z\s]+?)$"
        };
        
        foreach (var pattern in deletePatterns)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                var sql = $"DELETE FROM Employees WHERE Name = '{name}'";
                Console.WriteLine($"FallbackSqlGenerator: Generated {sql}");
                return (true, sql);
            }
        }
        
        return (false, string.Empty);
    }
}