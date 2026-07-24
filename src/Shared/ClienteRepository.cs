using Npgsql;

namespace SoatTechChallenge.Lambda.Shared;

public interface IClienteRepository
{
    Task<ClienteAuthInfo?> BuscarPorDocumentoAsync(string documento, CancellationToken ct = default);
}

// Acesso direto via ADO.NET (Npgsql), sem EF Core: o Lambda só precisa de uma
// leitura pontual por CPF, e um ORM completo custaria cold start sem trazer
// benefício aqui (ver ADR "Lambda usa Npgsql direto, não EF Core").
public class ClienteRepository : IClienteRepository
{
    private readonly string _connectionString;

    public ClienteRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ClienteAuthInfo?> BuscarPorDocumentoAsync(string documento, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(
            "SELECT id, nome, ativo FROM cliente WHERE documento = @documento LIMIT 1",
            connection);

        command.Parameters.AddWithValue("documento", documento);

        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new ClienteAuthInfo(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetBoolean(2));
    }
}

public record ClienteAuthInfo(Guid Id, string Nome, bool Ativo);
