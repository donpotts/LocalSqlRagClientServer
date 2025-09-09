using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace LocalTextToSqlChat.Client.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _authService;
    
    public CustomAuthenticationStateProvider(AuthService authService)
    {
        _authService = authService;
    }
    
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var isAuthenticated = await _authService.IsAuthenticatedAsync();
        
        if (isAuthenticated)
        {
            var username = await _authService.GetUsernameAsync();
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username ?? "")
            };
            
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            
            return new AuthenticationState(user);
        }
        
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
    
    public void NotifyUserAuthentication()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
    
    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }
}