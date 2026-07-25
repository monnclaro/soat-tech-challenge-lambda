using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SoatTechChallenge.Lambda.Shared;
using Xunit;

namespace Shared.Tests;

public class JwtServiceTests
{
    private const string JwtSecret = "chave-de-teste-com-32-caracteres!!";

    [Fact]
    public void GerarTokenUsuario_EmiteClaimsNomeERoles()
    {
        var token = JwtService.GerarTokenUsuario("Maria", ["Admin", "Gerente"], JwtSecret);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        Assert.Equal("Maria", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal(["Admin", "Gerente"], roles);
    }

    [Fact]
    public void GerarTokenUsuario_AssinaComOSegredoInformado()
    {
        var token = JwtService.GerarTokenUsuario("Maria", ["Admin"], JwtSecret);

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret))
        };

        // Não deve lançar — assinatura válida contra o mesmo segredo.
        handler.ValidateToken(token, parameters, out _);
    }

    [Fact]
    public void GerarTokenUsuario_AssinadoComSegredoDiferente_FalhaNaValidacao()
    {
        var token = JwtService.GerarTokenUsuario("Maria", ["Admin"], JwtSecret);

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("outro-segredo-completamente-diferente!!"))
        };

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => handler.ValidateToken(token, parameters, out _));
    }

    [Fact]
    public void GerarTokenUsuario_QuandoExpirationHoursNegativo_GeraTokenJaExpirado()
    {
        var token = JwtService.GerarTokenUsuario("Maria", ["Admin"], JwtSecret, expirationHours: -1);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.True(jwt.ValidTo < DateTime.UtcNow);
    }
}
