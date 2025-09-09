using System.Text.RegularExpressions;

namespace LocalTextToSqlChat.Server.Services;

public static class QueryIntentAnalyzer
{
    private static readonly HashSet<string> _writeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "delete", "remove", "drop", "update", "modify", "change", "insert", "add", 
        "create", "alter", "truncate", "replace", "merge", "edit"
    };
    
    private static readonly HashSet<string> _writePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "delete from", "remove from", "drop table", "drop database", "update set",
        "insert into", "add to", "create table", "alter table", "truncate table",
        "modify column", "change column", "edit record", "replace into"
    };
    
    public static bool IsWriteIntent(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return false;
            
        string cleanInput = userInput.Trim().ToLowerInvariant();
        
        // Check for explicit write patterns first
        foreach (var pattern in _writePatterns)
        {
            if (cleanInput.Contains(pattern))
                return true;
        }
        
        // Check for write keywords at the beginning or after common phrases
        var words = Regex.Split(cleanInput, @"\W+").Where(w => !string.IsNullOrEmpty(w)).ToArray();
        
        for (int i = 0; i < words.Length; i++)
        {
            if (_writeKeywords.Contains(words[i]))
            {
                // Check if it's likely a write operation by context
                if (i == 0) // First word is write keyword
                    return true;
                    
                if (i > 0 && (words[i-1] == "to" || words[i-1] == "can" || 
                             words[i-1] == "please" || words[i-1] == "i" || 
                             words[i-1] == "want" || words[i-1] == "need"))
                    return true;
                    
                // Look for object patterns after write keywords
                if (i < words.Length - 1 && (words[i+1] == "employee" || words[i+1] == "user" ||
                                           words[i+1] == "record" || words[i+1] == "row" ||
                                           words[i+1] == "from" || words[i+1] == "table"))
                    return true;
            }
        }
        
        return false;
    }
    
    public static string GetWriteOperationMessage()
    {
        return "I understand you want to modify data, but as a non-admin user, you only have read-only access. " +
               "I can help you query and view data using SELECT statements. " +
               "For data modifications, please contact an administrator.";
    }

    public static bool IsCreateIntent(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return false;
            
        string cleanInput = userInput.Trim().ToLowerInvariant();
        
        return cleanInput.Contains("create") || cleanInput.Contains("add") || 
               cleanInput.Contains("insert") || cleanInput.Contains("new employee") ||
               cleanInput.Contains("hire") || cleanInput.Contains("register");
    }

    public static bool IsUpdateIntent(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return false;
            
        string cleanInput = userInput.Trim().ToLowerInvariant();
        
        return cleanInput.Contains("update") || cleanInput.Contains("change") || 
               cleanInput.Contains("modify") || cleanInput.Contains("edit") ||
               cleanInput.Contains("set salary") || cleanInput.Contains("promote");
    }

    public static bool IsDeleteIntent(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return false;
            
        string cleanInput = userInput.Trim().ToLowerInvariant();
        
        return cleanInput.Contains("delete") || cleanInput.Contains("remove") || 
               cleanInput.Contains("fire") || cleanInput.Contains("terminate") ||
               cleanInput.Contains("drop");
    }
}