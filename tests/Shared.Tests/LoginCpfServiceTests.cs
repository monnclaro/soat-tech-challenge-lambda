using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        var service = new LoginCpfService(new FakeUsuarioRepository(null), JwtSecret);

        var resultado = await service.AutenticarAsync("123");

        Assert.Equal(LoginCpfStatus.CpfInvalido, resultado.Status);
        Assert.Null(resultado.Token);
    }

    [Fact]
    public async Task AutenticarAsync_QuandoUsuarioNaoExiste_RetornaNaoEncontrado()
    {
        var service = new LoginCpfService(new FakeUsuarioRepository(null), JwtSecret);

        var resultado = await service.AutenticarAsync(CpfValido);

        Assert.Equal(LoginCpfStatus.NaoEncontrado, resultado.Status);
    }

    [Fact]
    public async Task AutenticarAsync_QuandoUsuarioInativo_RetornaUsuarioInativo()
    {
        var usuario = new UsuarioAuthInfo(Guid.NewGuid(), "João", Ativo: false, Roles: ["Admin"]);
        var service = new LoginCpfService(new FakeUsuarioRepository(usuario), JwtSecret);

        var resultado = await service.AutenticarAsync(CpfValido);

        Assert.Equal(LoginCpfStatus.UsuarioInativo, resultado.Status);
    }

    [Fact]
    public async Task AutenticarAsync_QuandoUsuarioAtivo_RetornaSucessoComTokenComAsRoles()
    {
        var usuario = new UsuarioAuthInfo(Guid.NewGuid(), "João", Ativo: true, Roles: ["Admin", "Gerente"]);
        var service = new LoginCpfService(new FakeUsuarioRepository(usuario), JwtSecret);

        var resultado = await service.AutenticarAsync(CpfValido);

        Assert.Equal(LoginCpfStatus.Sucesso, resultado.Status);
        Assert.NotNull(resultado.Token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(resultado.Token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        Assert.Equal("João", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Contains("Admin", roles);
        Assert.Contains("Gerente", roles);
    }

    private class FakeUsuarioRepository : IUsuarioRepository
    {
        private readonly UsuarioAuthInfo? _usuario;

        public FakeUsuarioRepository(UsuarioAuthInfo? usuario) => _usuario = usuario;

        public Task<UsuarioAuthInfo?> BuscarPorCpfAsync(string cpf, CancellationToken ct = default) =>
            Task.FromResult(_usuario);
    }
}
