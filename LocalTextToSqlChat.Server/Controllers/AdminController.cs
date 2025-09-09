using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LocalTextToSqlChat.Server.Models;
using LocalTextToSqlChat.Server.Data;
using System.Security.Claims;

namespace LocalTextToSqlChat.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    
    public AdminController(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }
    
    private async Task<bool> IsCurrentUserAdminAsync()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var user = await _databaseService.GetUserByIdAsync(userId);
            return user?.IsAdmin == true;
        }
        return false;
    }
    
    [HttpGet("users")]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var users = await _databaseService.GetAllUsersAsync();
        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            IsAdmin = u.IsAdmin,
            CreatedAt = u.CreatedAt
        }).ToList();
        
        return Ok(userDtos);
    }
    
    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var user = await _databaseService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        
        var userDto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsAdmin = user.IsAdmin,
            CreatedAt = user.CreatedAt
        };
        
        return Ok(userDto);
    }
    
    [HttpPut("users/{id}")]
    public async Task<ActionResult> UpdateUser(int id, UpdateUserRequest request)
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var user = await _databaseService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        
        user.Username = request.Username;
        user.Email = request.Email;
        user.IsAdmin = request.IsAdmin;
        
        var success = await _databaseService.UpdateUserAsync(user);
        if (success)
        {
            return Ok();
        }
        
        return BadRequest(new { message = "Failed to update user" });
    }
    
    [HttpPost("users/{id}/toggle-admin")]
    public async Task<ActionResult> ToggleUserAdmin(int id)
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var user = await _databaseService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        
        var success = await _databaseService.SetUserAdminAsync(id, !user.IsAdmin);
        if (success)
        {
            return Ok(new { isAdmin = !user.IsAdmin });
        }
        
        return BadRequest(new { message = "Failed to toggle admin status" });
    }
    
    [HttpDelete("users/{id}")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out int currentUserId) && currentUserId == id)
        {
            return BadRequest(new { message = "Cannot delete your own account" });
        }
        
        var success = await _databaseService.DeleteUserAsync(id);
        if (success)
        {
            return Ok();
        }
        
        return BadRequest(new { message = "Failed to delete user" });
    }
    
    [HttpGet("employees")]
    public async Task<ActionResult<List<Employee>>> GetAllEmployees()
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var employees = await _databaseService.GetAllEmployeesAsync();
        return Ok(employees);
    }
    
    [HttpGet("employees/{id}")]
    public async Task<ActionResult<Employee>> GetEmployee(int id)
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        var employee = await _databaseService.GetEmployeeByIdAsync(id);
        if (employee == null)
        {
            return NotFound();
        }
        
        return Ok(employee);
    }
    
    [HttpPost("employees")]
    public async Task<ActionResult<Employee>> CreateEmployee(CreateEmployeeRequest request)
    {
        if (!await IsCurrentUserAdminAsync())
        {
            return Forbid();
        }
        
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required" });
        }
        
        var employee = await _databaseService.AddEmployeeAsync(
            request.Name,
            request.Department,
            request.Salary,
            request.HireDate
        );
        
        if (employee != null)
        {
            return Ok(employee);
        }
        
        return BadRequest(new { message = "Failed to create employee" });
    }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}

public class CreateEmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Department { get; set; }
    public decimal? Salary { get; set; }
    public DateTime? HireDate { get; set; }
}