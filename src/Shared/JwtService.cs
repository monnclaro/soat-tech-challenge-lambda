using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SoatTechChallenge.Lambda.Shared;

// Mesmo formato de token emitido por src/Infrastructure/Security/Jwt/JwtTokenProvider.cs
// no app para login por email/senha: HS256, claims Name + uma Role por role do
// usuário, expiração em horas. O segredo é compartilhado via SSM Parameter Store
// (ver LambdaConfig) para que o token emitido aqui seja aceito pelo
// AddJwtAuthentication do app sem nenhuma mudança na validação existente — os
// dois caminhos de login (email/senha e CPF) produzem o mesmo tipo de token.
public static class JwtService
{
    public static string GerarTokenUsuario(string nome, IReadOnlyList<string> roles, string jwtSecret, int expirationHours = 2)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new(ClaimTypes.Name, nome) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
