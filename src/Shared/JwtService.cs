using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SoatTechChallenge.Lambda.Shared;

// Mesmo formato de token aceito pelo AddJwtAuthentication do app (repo
// soat-tech-challenge): HS256, segredo compartilhado via SSM Parameter Store
// (ver LambdaConfig e infra-k8s/jwt.tf, que é quem gera o segredo).
//
// Claims: Name (nome), Role ("Cliente"), e NameIdentifier + "documento" — os
// dois últimos existem pra rotas que precisam confirmar que quem chama é
// dono do recurso (ex.: aprovar/reprovar orçamento da própria OS, listar
// OS pelo próprio documento) — ver OrdemServicosController no app.
public static class JwtService
{
    public static string GerarTokenCliente(Guid id, string nome, string documento, string jwtSecret, int expirationHours = 2)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, nome),
            new(ClaimTypes.Role, "Cliente"),
            new("documento", documento)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
