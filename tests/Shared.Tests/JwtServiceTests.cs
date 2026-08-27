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
    private static readonly Guid ClienteId = Guid.NewGuid();

    [Fact]
    public void GerarTokenCliente_EmiteClaimsNomeRoleDocumentoENameIdentifier()
    {
        var token = JwtService.GerarTokenCliente(ClienteId, "Maria", "52998224725", JwtSecret);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("Maria", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Cliente", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(ClienteId.ToString(), jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("52998224725", jwt.Claims.Single(c => c.Type == "documento").Value);
    }

    [Fact]
    public void GerarTokenCliente_AssinaComOSegredoInformado()
    {
        var token = JwtService.GerarTokenCliente(ClienteId, "Maria", "52998224725", JwtSecret);

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
    public void GerarTokenCliente_AssinadoComSegredoDiferente_FalhaNaValidacao()
    {
        var token = JwtService.GerarTokenCliente(ClienteId, "Maria", "52998224725", JwtSecret);

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
    public void GerarTokenCliente_QuandoExpirationHoursNegativo_GeraTokenJaExpirado()
    {
        var token = JwtService.GerarTokenCliente(ClienteId, "Maria", "52998224725", JwtSecret, expirationHours: -1);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.True(jwt.ValidTo < DateTime.UtcNow);
    }
}
