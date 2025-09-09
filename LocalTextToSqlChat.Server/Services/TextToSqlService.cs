using Microsoft.SemanticKernel;
using LocalTextToSqlChat.Server.Data;
using System.Text.RegularExpressions;

#pragma warning disable SKEXP0070

namespace LocalTextToSqlChat.Server.Services;

public class TextToSqlService
{
    private readonly Kernel _kernel;
    private readonly DatabaseService _databaseService;
    private readonly KernelFunction _textToSqlFunction;
    private readonly KernelFunction _finalAnswerFunction;
    
    public TextToSqlService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _kernel = BuildKernel();
        
        var dbSchema = _databaseService.GetDatabaseSchema();
        _textToSqlFunction = CreateTextToSqlFunction(_kernel, dbSchema);
        _finalAnswerFunction = CreateFinalAnswerFunction(_kernel);
    }
    
    public async Task<(string response, string? sqlQuery)> ProcessQueryAsync(string userInput, bool isAdmin = false)
    {
        try
        {
            // Use different functions based on admin status
            var sqlFunction = isAdmin ? _textToSqlFunction : CreateReadOnlyTextToSqlFunction();
            var sqlResult = await sqlFunction.InvokeAsync(_kernel, new() { ["input"] = userInput });
            string rawSqlQuery = sqlResult.GetValue<string>()!.Trim();
            
            // Clean the SQL query (remove markdown formatting, etc.)
            string cleanedSqlQuery = CleanMarkdownFromSql(rawSqlQuery);
            
            // Fix common INSERT query issues (mismatched columns/values)
            cleanedSqlQuery = SqlQueryFixer.FixInsertQuery(cleanedSqlQuery);
            
            // Double-check validation for non-admin users (safety measure)
            if (!isAdmin && !SqlQueryValidator.IsReadOnlyQuery(cleanedSqlQuery))
            {
                string validationError = SqlQueryValidator.GetQueryValidationError(cleanedSqlQuery);
                return ($"Access Denied: {validationError}", cleanedSqlQuery);
            }
            
            string dbData = _databaseService.ExecuteQueryAndFormatResults(cleanedSqlQuery);
            Console.WriteLine($"Raw DB Data length: {dbData.Length} characters");
            Console.WriteLine($"First 500 chars of data: {dbData.Substring(0, Math.Min(500, dbData.Length))}");
            
            if (string.IsNullOrWhiteSpace(dbData))
            {
                return ("I couldn't find any data for that query.", cleanedSqlQuery);
            }
            
            // For queries that work better with raw data, bypass AI processing
            var lowerInput = userInput.ToLowerInvariant();
            if ((lowerInput.Contains("show") && lowerInput.Contains("all") && lowerInput.Contains("employee")) ||
                (lowerInput.Contains("list") && lowerInput.Contains("department")) ||
                (lowerInput.Contains("average") && lowerInput.Contains("salary")) ||
                (lowerInput.Contains("hired") && lowerInput.Contains("recently")) ||
                (lowerInput.Contains("recent") && lowerInput.Contains("hire")) ||
                (lowerInput.Contains("top") && lowerInput.Contains("employee")) ||
                (lowerInput.Contains("list") && lowerInput.Contains("employee")) ||
                (lowerInput.Contains("table") && lowerInput.Contains("employee")) ||
                (lowerInput.Contains("show") && lowerInput.Contains("employee") && lowerInput.Contains("salary")) ||
                (lowerInput.Contains("paid") && lowerInput.Contains("employee")) ||
                (lowerInput.Contains("order by") && lowerInput.Contains("salary")))
            {
                return (dbData, cleanedSqlQuery);
            }
            
            var finalAnswerResult = await _finalAnswerFunction.InvokeAsync(_kernel, new()
            {
                ["input"] = userInput,
                ["data"] = dbData
            });
            
            return (finalAnswerResult.GetValue<string>()!, cleanedSqlQuery);
        }
        catch (Exception ex)
        {
            return ($"An error occurred: {ex.Message}", null);
        }
    }
    
    private Kernel BuildKernel()
    {
        var builder = Kernel.CreateBuilder();
        
        builder.AddOllamaChatCompletion(
            modelId: "phi3:3.8b",
            endpoint: new Uri("http://localhost:11434")
        );
        
        return builder.Build();
    }
    
    private KernelFunction CreateTextToSqlFunction(Kernel kernel, string dbSchema)
    {
        const string prompt = @"
Generate SQL for employee database (SQLite). Columns: Id, Name, Department, Salary, HireDate
Rules:
- INSERT: Only specify columns with values. No Id column. Use defaults for unspecified fields.
- UPDATE: Change existing records
- DELETE: Remove records completely
- Do NOT guess or assume values not explicitly mentioned
- SQLite: Use DATETIME('now') not NOW() for current timestamp
- SQLite: Use DATE('now') for current date
- Use LIKE for date searches with format 'YYYY-MM-DD'

Q: Show me all employees
A: SELECT * FROM Employees

Q: List employees by department  
A: SELECT Name, Department, Salary FROM Employees ORDER BY Department, Name

Q: What's the average salary in Engineering?
A: SELECT AVG(Salary) AS AverageSalary FROM Employees WHERE Department = 'Engineering'

Q: Who was hired most recently?
A: SELECT Name, Department, HireDate FROM Employees ORDER BY HireDate DESC LIMIT 5

Q: Show employees hired in 2022
A: SELECT Name, Department, Salary, HireDate FROM Employees WHERE HireDate LIKE '2022-%'

Q: Find employees hired in 2023
A: SELECT Name, Department, HireDate FROM Employees WHERE HireDate LIKE '2023-%'

Q: Show employees hired this month
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'start of month') AND DATE('now')

Q: Find employees hired today
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) = DATE('now')

Q: Show employees hired this week
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'weekday 0', '-6 days') AND DATE('now')

Q: Find employees hired this year
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'start of year') AND DATE('now')

Q: Show employees hired in the last 30 days
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) >= DATE('now', '-30 days')

Q: Find employees hired in the last 3 years
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) >= DATE('now', '-3 years')

Q: Show employees hired last month
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'start of month', '-1 month') AND DATE('now', 'start of month', '-1 day')

Q: Add new employee John Smith to Engineering with salary 75000
A: INSERT INTO Employees (Name, Department, Salary) VALUES ('John Smith', 'Engineering', 75000)

Q: Add new employee Jane Doe to Marketing
A: INSERT INTO Employees (Name, Department) VALUES ('Jane Doe', 'Marketing')

Q: Add employee Mike Wilson
A: INSERT INTO Employees (Name) VALUES ('Mike Wilson')

Q: Assign Grace Wilson to the HR department and give her a salary of 51000
A: UPDATE Employees SET Department = 'HR', Salary = 51000 WHERE Name = 'Grace Wilson'

Q: Move Bob Smith to Engineering department
A: UPDATE Employees SET Department = 'Engineering' WHERE Name = 'Bob Smith'

Q: Give Alice Johnson a raise to 100000
A: UPDATE Employees SET Salary = 100000 WHERE Name = 'Alice Johnson'

Q: Promote Sarah Davis to manager with salary 95000
A: UPDATE Employees SET Salary = 95000 WHERE Name = 'Sarah Davis'

Q: Transfer Mike Johnson to Sales
A: UPDATE Employees SET Department = 'Sales' WHERE Name = 'Mike Johnson'

Q: Move Lisa Chen to Marketing team
A: UPDATE Employees SET Department = 'Marketing' WHERE Name = 'Lisa Chen'

Q: Reassign Tom Brown to Finance
A: UPDATE Employees SET Department = 'Finance' WHERE Name = 'Tom Brown'

Q: Transfer Kate Wilson to IT department
A: UPDATE Employees SET Department = 'IT' WHERE Name = 'Kate Wilson'

Q: Fire Bob Smith
A: DELETE FROM Employees WHERE Name = 'Bob Smith'

Q: Let go of Diana Prince due to budget cuts
A: DELETE FROM Employees WHERE Name = 'Diana Prince'

Q: Terminate Charlie Brown
A: DELETE FROM Employees WHERE Name = 'Charlie Brown'

Q: John Wilson was laid off
A: DELETE FROM Employees WHERE Name = 'John Wilson'

Q: Dismiss Sarah Miller from the company
A: DELETE FROM Employees WHERE Name = 'Sarah Miller'

Q: {{$input}}
A: ";
        
        var executionSettings = new PromptExecutionSettings()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "temperature", 0.0 },
                { "timeout", 30 }  // 30 second timeout
            }
        };
        
        var promptConfig = new PromptTemplateConfig
        {
            Template = prompt.Replace("{{$schema}}", dbSchema),
            ExecutionSettings = new Dictionary<string, PromptExecutionSettings>
            {
                { "default", executionSettings }
            }
        };
        
        return KernelFunctionFactory.CreateFromPrompt(promptConfig);
    }
    
    private KernelFunction CreateReadOnlyTextToSqlFunction()
    {
        var dbSchema = _databaseService.GetDatabaseSchema();
        const string prompt = @"
Generate SQL for employee database (SQLite). Columns: Id, Name, Department, Salary, HireDate
Rules:
- INSERT: Only specify columns with values. No Id column. Use defaults for unspecified fields.
- UPDATE: Change existing records
- DELETE: Remove records completely
- Do NOT guess or assume values not explicitly mentioned
- SQLite: Use DATETIME('now') not NOW() for current timestamp
- SQLite: Use DATE('now') for current date
- Use LIKE for date searches with format 'YYYY-MM-DD'

Q: Show me all employees
A: SELECT * FROM Employees

Q: List employees by department  
A: SELECT Name, Department, Salary FROM Employees ORDER BY Department, Name

Q: What's the average salary in Engineering?
A: SELECT AVG(Salary) AS AverageSalary FROM Employees WHERE Department = 'Engineering'

Q: Who was hired most recently?
A: SELECT Name, Department, HireDate FROM Employees ORDER BY HireDate DESC LIMIT 5

Q: Show employees hired in 2022
A: SELECT Name, Department, Salary, HireDate FROM Employees WHERE HireDate LIKE '2022-%'

Q: Find employees hired in 2023
A: SELECT Name, Department, HireDate FROM Employees WHERE HireDate LIKE '2023-%'

Q: Show employees hired this month
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'start of month') AND DATE('now')

Q: Find employees hired today
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) = DATE('now')

Q: Show employees hired this week
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'weekday 0', '-6 days') AND DATE('now')

Q: Find employees hired this year
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'start of year') AND DATE('now')

Q: Show employees hired in the last 30 days
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) >= DATE('now', '-30 days')

Q: Find employees hired in the last 3 years
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) >= DATE('now', '-3 years')

Q: Show employees hired last month
A: SELECT Name, Department, HireDate FROM Employees WHERE DATE(HireDate) BETWEEN DATE('now', 'start of month', '-1 month') AND DATE('now', 'start of month', '-1 day')

Q: Add new employee John Smith to Engineering with salary 75000
A: INSERT INTO Employees (Name, Department, Salary) VALUES ('John Smith', 'Engineering', 75000)

Q: Add new employee Jane Doe to Marketing
A: INSERT INTO Employees (Name, Department) VALUES ('Jane Doe', 'Marketing')

Q: Add employee Mike Wilson
A: INSERT INTO Employees (Name) VALUES ('Mike Wilson')

Q: Assign Grace Wilson to the HR department and give her a salary of 51000
A: UPDATE Employees SET Department = 'HR', Salary = 51000 WHERE Name = 'Grace Wilson'

Q: Move Bob Smith to Engineering department
A: UPDATE Employees SET Department = 'Engineering' WHERE Name = 'Bob Smith'

Q: Give Alice Johnson a raise to 100000
A: UPDATE Employees SET Salary = 100000 WHERE Name = 'Alice Johnson'

Q: Promote Sarah Davis to manager with salary 95000
A: UPDATE Employees SET Salary = 95000 WHERE Name = 'Sarah Davis'

Q: Transfer Mike Johnson to Sales
A: UPDATE Employees SET Department = 'Sales' WHERE Name = 'Mike Johnson'

Q: Move Lisa Chen to Marketing team
A: UPDATE Employees SET Department = 'Marketing' WHERE Name = 'Lisa Chen'

Q: Reassign Tom Brown to Finance
A: UPDATE Employees SET Department = 'Finance' WHERE Name = 'Tom Brown'

Q: Transfer Kate Wilson to IT department
A: UPDATE Employees SET Department = 'IT' WHERE Name = 'Kate Wilson'

Q: Fire Bob Smith
A: DELETE FROM Employees WHERE Name = 'Bob Smith'

Q: Let go of Diana Prince due to budget cuts
A: DELETE FROM Employees WHERE Name = 'Diana Prince'

Q: Terminate Charlie Brown
A: DELETE FROM Employees WHERE Name = 'Charlie Brown'

Q: John Wilson was laid off
A: DELETE FROM Employees WHERE Name = 'John Wilson'

Q: Dismiss Sarah Miller from the company
A: DELETE FROM Employees WHERE Name = 'Sarah Miller'

Q: {{$input}}
A: ";
        
        var executionSettings = new PromptExecutionSettings()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "temperature", 0.0 },
                { "timeout", 30 }  // 30 second timeout
            }
        };
        
        var promptConfig = new PromptTemplateConfig
        {
            Template = prompt.Replace("{{$schema}}", dbSchema),
            ExecutionSettings = new Dictionary<string, PromptExecutionSettings>
            {
                { "default", executionSettings }
            }
        };
        
        return KernelFunctionFactory.CreateFromPrompt(promptConfig);
    }
    
    private KernelFunction CreateFinalAnswerFunction(Kernel kernel)
    {
        const string prompt = @"
Answer the user's question based on the employee data below. Be specific and include relevant details like department names, employee names, etc. from the original question.

Examples:
Question: What is the average salary in the Sales department?
Data: Average annual salary: $80,000
Answer: The average annual salary in the Sales department is $80,000.

Question: Who are the highest paid employees?
Data: Alice Johnson - $95,000, Charlie Brown - $110,000
Answer: The highest paid employees are Charlie Brown ($110,000) and Alice Johnson ($95,000).

Question: Show me all employees in Engineering
Data: [Employee records for Engineering department]
Answer: Here are all employees in the Engineering department: [details]

User Question: {{$input}}

Employee Data:
{{$data}}

Answer:
";
        
        var executionSettings = new PromptExecutionSettings()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "temperature", 0.3 }
            }
        };
        
        var promptConfig = new PromptTemplateConfig
        {
            Template = prompt,
            ExecutionSettings = new Dictionary<string, PromptExecutionSettings>
            {
                { "default", executionSettings }
            }
        };
        
        return KernelFunctionFactory.CreateFromPrompt(promptConfig);
    }
    
    private static string CleanMarkdownFromSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;
            
        // Remove markdown code block formatting
        sql = Regex.Replace(sql, @"```(?:sql|SQL)?\s*", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"```\s*", "");
        
        // Aggressive cleaning for explanatory text
        var lines = sql.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var cleanLine = line.Trim();
            // Look for lines that start with SQL keywords
            if (cleanLine.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                cleanLine.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                cleanLine.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                cleanLine.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                // Extract just the SQL statement (stop at semicolon or end)
                int semicolonIndex = cleanLine.IndexOf(';');
                if (semicolonIndex > 0)
                    return cleanLine.Substring(0, semicolonIndex + 1);
                return cleanLine;
            }
        }
        
        return sql.Trim();
    }
}