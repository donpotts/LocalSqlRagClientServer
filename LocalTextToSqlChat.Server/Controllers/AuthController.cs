using Microsoft.AspNetCore.Mvc;
using LocalTextToSqlChat.Server.Models;
using LocalTextToSqlChat.Server.Data;
using LocalTextToSqlChat.Server.Services;
using BCrypt.Net;

namespace LocalTextToSqlChat.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly JwtService _jwtService;
    
    public AuthController(DatabaseService databaseService, JwtService jwtService)
    {
        _databaseService = databaseService;
        _jwtService = jwtService;
    }
    
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _databaseService.GetUserByUsernameAsync(request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }
        
        var token = _jwtService.GenerateToken(user.Username, user.Id, user.IsAdmin);
        
        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            Expires = DateTime.UtcNow.AddHours(24)
        });
    }
    
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existingUser = await _databaseService.GetUserByUsernameAsync(request.Username);
        if (existingUser != null)
        {
            return BadRequest(new { message = "Username already exists" });
        }
        
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = await _databaseService.CreateUserAsync(request.Username, request.Email, passwordHash);
        
        if (user == null)
        {
            return BadRequest(new { message = "Failed to create user" });
        }
        
        var token = _jwtService.GenerateToken(user.Username, user.Id, user.IsAdmin);
        
        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            Expires = DateTime.UtcNow.AddHours(24)
        });
    }
}