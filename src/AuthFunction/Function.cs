using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using SoatTechChallenge.Lambda.Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SoatTechChallenge.Lambda.Auth;

public class Function
{
    // Requisição: POST /auth/login-cpf { "cpf": "12345678909" }
    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        LoginCpfRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<LoginCpfRequest>(request.Body ?? string.Empty);
        }
        catch (JsonException)
        {
            return Response(400, new { erro = "Corpo da requisição inválido." });
        }

        var repository = new ClienteRepository(LambdaConfig.BuildConnectionString());
        var service = new LoginCpfService(repository, LambdaConfig.JwtSecret);

        var resultado = await service.AutenticarAsync(body?.Cpf);

        return resultado.Status switch
        {
            LoginCpfStatus.CpfInvalido => Response(400, new { erro = "CPF inválido." }),
            LoginCpfStatus.NaoEncontrado => Response(404, new { erro = "Cliente não encontrado." }),
            LoginCpfStatus.ClienteInativo => Response(403, new { erro = "Cliente inativo." }),
            LoginCpfStatus.Sucesso => Response(200, new { token = resultado.Token }),
            _ => Response(500, new { erro = "Erro inesperado." })
        };
    }

    private static APIGatewayHttpApiV2ProxyResponse Response(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };
}

public record LoginCpfRequest([property: JsonPropertyName("cpf")] string? Cpf);
