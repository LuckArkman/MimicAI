using System.Security.Cryptography;
using System.Text;
using Database.Postgres;
using Repositorys.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthResult> RegisterAsync(string name, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult { Success = false, Message = "Preencha todos os campos obrigatórios." };
        }

        if (await _userRepository.EmailExistsAsync(email))
        {
            return new AuthResult { Success = false, Message = "Este e-mail já está cadastrado no sistema." };
        }

        string salt = Guid.NewGuid().ToString("N");
        string passwordHash = HashPassword(password, salt);

        // We store the salt alongside the hash in the entity (we can append it or use a simple format "salt.hash")
        var user = new UserEntity
        {
            Name = name,
            Email = email.ToLower(),
            PasswordHash = $"{salt}.{passwordHash}",
            CreatedAt = DateTime.UtcNow
        };

        var createdUser = await _userRepository.CreateAsync(user);
        string token = GenerateSimpleToken(createdUser);

        return new AuthResult
        {
            Success = true,
            Message = "Usuário cadastrado com sucesso!",
            Token = token,
            UserId = createdUser.Id,
            Name = createdUser.Name
        };
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult { Success = false, Message = "E-mail e senha são obrigatórios." };
        }

        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return new AuthResult { Success = false, Message = "E-mail ou senha incorretos." };
        }

        var parts = user.PasswordHash.Split('.', 2);
        if (parts.Length != 2)
        {
            return new AuthResult { Success = false, Message = "Erro interno de validação de credenciais." };
        }

        string salt = parts[0];
        string expectedHash = parts[1];
        string incomingHash = HashPassword(password, salt);

        if (incomingHash != expectedHash)
        {
            return new AuthResult { Success = false, Message = "E-mail ou senha incorretos." };
        }

        string token = GenerateSimpleToken(user);

        return new AuthResult
        {
            Success = true,
            Message = "Login efetuado com sucesso!",
            Token = token,
            UserId = user.Id,
            Name = user.Name
        };
    }

    private string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt));
        var builder = new StringBuilder();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

    private string GenerateSimpleToken(UserEntity user)
    {
        // Simple token generation (can be replaced by standard JwtSecurityTokenHandler)
        // Format: Base64(UserId:Email:Name:Timestamp)
        var tokenContent = $"{user.Id}:{user.Email}:{user.Name}:{DateTime.UtcNow.Ticks}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenContent));
    }
}
