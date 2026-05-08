namespace Services.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string name, string email, string password);
    Task<AuthResult> LoginAsync(string email, string password);
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}
