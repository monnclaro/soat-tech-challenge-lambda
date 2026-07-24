using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SoatTechChallenge.Lambda.Shared;

// Mesmo formato de token emitido por src/Infrastructure/Security/Jwt/JwtTokenProvider.cs
// no app: HS256, claims Name/Role, expiração em horas. O segredo é compartilhado via
// Secrets Manager (ver SecretsManagerClient) para que o token emitido aqui seja aceito
// pelo AddJwtAuthentication do app sem nenhuma mudança na validação existente.
public static class JwtService
{
    public static string GerarTokenCliente(Guid clienteId, string nome, string jwtSecret, int expirationHours = 2)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, clienteId.ToString()),
            new(ClaimTypes.Name, nome),
            new(ClaimTypes.Role, "Cliente")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static ClaimsPrincipal? ValidarToken(string token, string jwtSecret)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
