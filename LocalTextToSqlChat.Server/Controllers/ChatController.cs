using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LocalTextToSqlChat.Server.Models;
using LocalTextToSqlChat.Server.Data;
using LocalTextToSqlChat.Server.Services;

namespace LocalTextToSqlChat.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly TextToSqlService _textToSqlService;
    private readonly DatabaseService _databaseService;
    
    public ChatController(TextToSqlService textToSqlService, DatabaseService databaseService)
    {
        _textToSqlService = textToSqlService;
        _databaseService = databaseService;
    }
    
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> ProcessMessage(ChatRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized();
        }
        
        // Check if user is admin
        var isAdmin = User.IsInRole("Admin");
        
        // Fast rejection for non-admin write operations
        if (!isAdmin && QueryIntentAnalyzer.IsWriteIntent(request.Message))
        {
            var rejectMessage = QueryIntentAnalyzer.GetWriteOperationMessage();
            await _databaseService.SaveChatMessageAsync(userId, request.Message, rejectMessage, null);
            
            stopwatch.Stop();
            return Ok(new ChatResponse
            {
                Response = rejectMessage,
                SqlQuery = null,
                CreatedAt = DateTime.UtcNow,
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        
        var (response, sqlQuery) = await _textToSqlService.ProcessQueryAsync(request.Message, isAdmin);
        
        await _databaseService.SaveChatMessageAsync(userId, request.Message, response, sqlQuery);
        
        stopwatch.Stop();
        return Ok(new ChatResponse
        {
            Response = response,
            SqlQuery = sqlQuery,
            CreatedAt = DateTime.UtcNow,
            ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
        });
    }
}