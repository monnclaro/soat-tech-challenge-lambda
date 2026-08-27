namespace SoatTechChallenge.Lambda.Shared;

public enum LoginCpfStatus
{
    CpfInvalido,
    NaoEncontrado,
    ClienteInativo,
    Sucesso
}

public record LoginCpfResult(LoginCpfStatus Status, string? Token = null);

// Lógica de negócio da autenticação por CPF, isolada de Function.cs (handler
// Lambda) para poder ser testada sem depender de API Gateway nem AWS SDK.
//
// Autentica Cliente (não Usuario): o token só abre as rotas que fazem
// sentido pro cliente da oficina (consultar as próprias OS, aprovar/
// reprovar o próprio orçamento) — ver RFC 0003 e OrdemServicosController no
// app. Funcionário continua logando por email/senha, sem relação com isto.
public class LoginCpfService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly string _jwtSecret;

    public LoginCpfService(IClienteRepository clienteRepository, string jwtSecret)
    {
        _clienteRepository = clienteRepository;
        _jwtSecret = jwtSecret;
    }

    public async Task<LoginCpfResult> AutenticarAsync(string? cpfBruto, CancellationToken ct = default)
    {
        if (!CpfValidator.TryNormalizar(cpfBruto, out var cpf))
            return new LoginCpfResult(LoginCpfStatus.CpfInvalido);

        var cliente = await _clienteRepository.BuscarPorDocumentoAsync(cpf, ct);

        if (cliente is null)
            return new LoginCpfResult(LoginCpfStatus.NaoEncontrado);

        if (!cliente.Ativo)
            return new LoginCpfResult(LoginCpfStatus.ClienteInativo);

        var token = JwtService.GerarTokenCliente(cliente.Id, cliente.Nome, cpf, _jwtSecret);
        return new LoginCpfResult(LoginCpfStatus.Sucesso, token);
    }
}
