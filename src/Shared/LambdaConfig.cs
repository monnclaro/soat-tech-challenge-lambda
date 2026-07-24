namespace SoatTechChallenge.Lambda.Shared;

// Variáveis de ambiente injetadas pelo Terraform deste repositório (ver infra/lambda.tf),
// lidas a partir do SSM Parameter Store em tempo de apply — não em tempo de execução.
// Sem Secrets Manager nem chamada de rede pra buscar segredo: o valor já chega pronto
// como env var (criptografada em repouso pela AWS por padrão), o que também evita
// depender de o Lambda alcançar a API pública do Secrets Manager sem NAT Gateway na
// subnet privada. Ver ADR "Prioridade de custo e AWS Academy".
public static class LambdaConfig
{
    public static string DbHost => Env("DB_HOST");
    public static string DbPort => Env("DB_PORT");
    public static string DbName => Env("DB_NAME");
    public static string DbUsername => Env("DB_USERNAME");
    public static string DbPassword => Env("DB_PASSWORD");
    public static string JwtSecret => Env("JWT_SECRET");

    public static string BuildConnectionString() =>
        $"Host={DbHost};Port={DbPort};Database={DbName};" +
        $"Username={DbUsername};Password={DbPassword};" +
        "SSL Mode=Require;Trust Server Certificate=true";

    private static string Env(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Variável de ambiente '{name}' não configurada.");
}
