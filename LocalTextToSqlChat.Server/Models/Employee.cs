using System.ComponentModel.DataAnnotations;

namespace LocalTextToSqlChat.Server.Models;

public class Employee
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Department { get; set; } = "Unknown";
    
    public decimal Salary { get; set; } = 0;
    
    public DateTime HireDate { get; set; } = DateTime.Now;
    
    public Employee() { }
    
    public Employee(string name, string? department = null, decimal? salary = null, DateTime? hireDate = null)
    {
        Name = name;
        Department = department ?? "Unknown";
        Salary = salary ?? 0;
        HireDate = hireDate ?? DateTime.Now;
    }
}