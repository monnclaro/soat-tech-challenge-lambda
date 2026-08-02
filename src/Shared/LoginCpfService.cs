namespace SoatTechChallenge.Lambda.Shared;

public enum LoginCpfStatus
{
    CpfInvalido,
    NaoEncontrado,
    UsuarioInativo,
    Sucesso
}

public record LoginCpfResult(LoginCpfStatus Status, string? Token = null);

// Lógica de negócio da autenticação por CPF, isolada de Function.cs (handler
// Lambda) para poder ser testada sem depender de API Gateway nem AWS SDK.
//
// Autentica Usuario (funcionário), não Cliente: esta função protege rotas
// sensíveis da aplicação (back-office) — ver RFC 0003. O funcionário
// continua podendo logar por email/senha também (POST /api/auth/login,
// já existente); esta é uma segunda forma de obter o mesmo tipo de token.
public class LoginCpfService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly string _jwtSecret;

    public LoginCpfService(IUsuarioRepository usuarioRepository, string jwtSecret)
    {
        _usuarioRepository = usuarioRepository;
        _jwtSecret = jwtSecret;
    }

    public async Task<LoginCpfResult> AutenticarAsync(string? cpfBruto, CancellationToken ct = default)
    {
        if (!CpfValidator.TryNormalizar(cpfBruto, out var cpf))
            return new LoginCpfResult(LoginCpfStatus.CpfInvalido);

        var usuario = await _usuarioRepository.BuscarPorCpfAsync(cpf, ct);

        if (usuario is null)
            return new LoginCpfResult(LoginCpfStatus.NaoEncontrado);

        if (!usuario.Ativo)
            return new LoginCpfResult(LoginCpfStatus.UsuarioInativo);

        var token = JwtService.GerarTokenUsuario(usuario.Nome, usuario.Roles, _jwtSecret);
        return new LoginCpfResult(LoginCpfStatus.Sucesso, token);
    }
}
