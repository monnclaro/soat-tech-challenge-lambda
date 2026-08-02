using Npgsql;

namespace SoatTechChallenge.Lambda.Shared;

public interface IUsuarioRepository
{
    Task<UsuarioAuthInfo?> BuscarPorCpfAsync(string cpf, CancellationToken ct = default);
}

// Acesso direto via ADO.NET (Npgsql), sem EF Core: o Lambda só precisa de uma
// leitura pontual por CPF, e um ORM completo custaria cold start sem trazer
// benefício aqui (ver ADR "Lambda usa Npgsql direto, não EF Core").
public class UsuarioRepository : IUsuarioRepository
{
    private readonly string _connectionString;

    public UsuarioRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<UsuarioAuthInfo?> BuscarPorCpfAsync(string cpf, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(
            """
            SELECT u.id, u.nome, u.ativo, r.role
            FROM usuario u
            LEFT JOIN usuario_role r ON r.id_usuario = u.id
            WHERE u.cpf = @cpf
            """,
            connection);

        command.Parameters.AddWithValue("cpf", cpf);

        await using var reader = await command.ExecuteReaderAsync(ct);

        var encontrado = false;
        Guid id = Guid.Empty;
        var nome = string.Empty;
        var ativo = false;
        var roles = new List<string>();

        while (await reader.ReadAsync(ct))
        {
            encontrado = true;
            id = reader.GetGuid(0);
            nome = reader.GetString(1);
            ativo = reader.GetBoolean(2);

            if (!reader.IsDBNull(3))
                roles.Add(reader.GetString(3));
        }

        return encontrado ? new UsuarioAuthInfo(id, nome, ativo, roles) : null;
    }
}

public record UsuarioAuthInfo(Guid Id, string Nome, bool Ativo, IReadOnlyList<string> Roles);
