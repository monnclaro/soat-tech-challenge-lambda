using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using SoatTechChallenge.Lambda.Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SoatTechChallenge.Lambda.Authorizer;

// Lambda Authorizer (formato "simple response") do API Gateway HTTP API,
// plugado nas rotas /api/*. Valida o mesmo JWT HS256 aceito pelo app
// (emitido aqui pelo AuthFunction para clientes, ou pelo endpoint de login
// do app para usuários internos) e repassa role/sub como contexto para
// o app não precisar revalidar a assinatura.
public class Function
{
    public APIGatewayCustomAuthorizerV2SimpleResponse FunctionHandler(
        APIGatewayCustomAuthorizerV2Request request,
        ILambdaContext context)
    {
        var token = ExtrairBearerToken(request);

        if (token is null)
            return Negado();

        var principal = JwtService.ValidarToken(token, LambdaConfig.JwtSecret);

        if (principal is null)
            return Negado();

        var role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
        var sub = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        return new APIGatewayCustomAuthorizerV2SimpleResponse
        {
            IsAuthorized = true,
            Context = new Dictionary<string, object>
            {
                ["role"] = role,
                ["sub"] = sub
            }
        };
    }

    private static string? ExtrairBearerToken(APIGatewayCustomAuthorizerV2Request request)
    {
        if (request.Headers is null)
            return null;

        var header = request.Headers
            .FirstOrDefault(h => string.Equals(h.Key, "authorization", StringComparison.OrdinalIgnoreCase))
            .Value;

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return header["Bearer ".Length..].Trim();
    }

    private static APIGatewayCustomAuthorizerV2SimpleResponse Negado() => new() { IsAuthorized = false };
}
