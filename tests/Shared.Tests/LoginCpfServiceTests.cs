using SoatTechChallenge.Lambda.Shared;
using Xunit;

namespace Shared.Tests;

public class LoginCpfServiceTests
{
    private const string CpfValido = "52998224725";
    private const string JwtSecret = "chave-de-teste-com-32-caracteres!!";

    [Fact]
    public async Task AutenticarAsync_QuandoCpfInvalido_RetornaCpfInvalido()
    {
        var service = new LoginCpfService(new FakeClienteRepository(null), JwtSecret);

        var resultado = await service.AutenticarAsync("123");

        Assert.Equal(LoginCpfStatus.CpfInvalido, resultado.Status);
        Assert.Null(resultado.Token);
    }

    [Fact]
    public async Task AutenticarAsync_QuandoClienteNaoExiste_RetornaNaoEncontrado()
    {
        var service = new LoginCpfService(new FakeClienteRepository(null), JwtSecret);

        var resultado = await service.AutenticarAsync(CpfValido);

        Assert.Equal(LoginCpfStatus.NaoEncontrado, resultado.Status);
    }

    [Fact]
    public async Task AutenticarAsync_QuandoClienteInativo_RetornaClienteInativo()
    {
        var cliente = new ClienteAuthInfo(Guid.NewGuid(), "João", Ativo: false);
        var service = new LoginCpfService(new FakeClienteRepository(cliente), JwtSecret);

        var resultado = await service.AutenticarAsync(CpfValido);

        Assert.Equal(LoginCpfStatus.ClienteInativo, resultado.Status);
    }

    [Fact]
    public async Task AutenticarAsync_QuandoClienteAtivo_RetornaSucessoComToken()
    {
        var cliente = new ClienteAuthInfo(Guid.NewGuid(), "João", Ativo: true);
        var service = new LoginCpfService(new FakeClienteRepository(cliente), JwtSecret);

        var resultado = await service.AutenticarAsync(CpfValido);

        Assert.Equal(LoginCpfStatus.Sucesso, resultado.Status);
        Assert.NotNull(resultado.Token);

        var principal = JwtService.ValidarToken(resultado.Token!, JwtSecret);
        Assert.NotNull(principal);
        Assert.Equal("Cliente", principal!.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
    }

    private class FakeClienteRepository : IClienteRepository
    {
        private readonly ClienteAuthInfo? _cliente;

        public FakeClienteRepository(ClienteAuthInfo? cliente) => _cliente = cliente;

        public Task<ClienteAuthInfo?> BuscarPorDocumentoAsync(string documento, CancellationToken ct = default) =>
            Task.FromResult(_cliente);
    }
}
