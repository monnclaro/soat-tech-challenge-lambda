using SoatTechChallenge.Lambda.Shared;
using Xunit;

namespace Shared.Tests;

public class JwtServiceTests
{
    private const string JwtSecret = "chave-de-teste-com-32-caracteres!!";

    [Fact]
    public void ValidarToken_QuandoTokenGeradoComMesmoSegredo_RetornaPrincipalValido()
    {
        var clienteId = Guid.NewGuid();
        var token = JwtService.GerarTokenCliente(clienteId, "Maria", JwtSecret);

        var principal = JwtService.ValidarToken(token, JwtSecret);

        Assert.NotNull(principal);
        Assert.Equal(clienteId.ToString(), principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Cliente", principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
    }

    [Fact]
    public void ValidarToken_QuandoSegredoDiferente_RetornaNull()
    {
        var token = JwtService.GerarTokenCliente(Guid.NewGuid(), "Maria", JwtSecret);

        var principal = JwtService.ValidarToken(token, "outro-segredo-completamente-diferente!!");

        Assert.Null(principal);
    }

    [Fact]
    public void ValidarToken_QuandoTokenExpirado_RetornaNull()
    {
        var token = JwtService.GerarTokenCliente(Guid.NewGuid(), "Maria", JwtSecret, expirationHours: -1);

        var principal = JwtService.ValidarToken(token, JwtSecret);

        Assert.Null(principal);
    }
}
