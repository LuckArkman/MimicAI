using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Database.Postgres;
using Repositorys.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
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

        var user = new UserEntity
        {
            Name = name,
            Email = email.ToLower(),
            PasswordHash = $"{salt}.{passwordHash}",
            CreatedAt = DateTime.UtcNow
        };

        var createdUser = await _userRepository.CreateAsync(user);
        string token = GenerateJwtToken(createdUser);

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

        string token = GenerateJwtToken(user);

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

    private string GenerateJwtToken(UserEntity user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        // Use a secure key from configuration, or fallback to a hardcoded development key
        var keyString = _configuration["Jwt:Key"] ?? "MinhaChaveSuperSecretaDesenvolvimento12345!@#";
        var key = Encoding.ASCII.GetBytes(keyString);
        var issuer = _configuration["Jwt:Issuer"] ?? "MimicAI";

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = issuer,
            Audience = issuer,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
