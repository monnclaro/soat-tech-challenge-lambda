namespace SoatTechChallenge.Lambda.Shared;

// Porta do algoritmo de validação de CPF de
// src/Domain/Common/ValueObjects/CpfChecksum.cs no repositório soat-tech-challenge
// (usado lá tanto por Cliente quanto por Usuario). Aqui valida o CPF de Usuario
// (funcionário) — é uma cópia deliberada, não uma referência de projeto: este
// Lambda é uma unidade de deploy independente (repositório, pipeline de CI/CD e
// runtime Lambda próprios, com ciclo de release desacoplado do monólito) — ver
// ADR "Split de repositórios por unidade de deploy". Se a regra de validação de
// CPF mudar, precisa mudar nos dois lugares.
public static class CpfValidator
{
    public static bool TryNormalizar(string? cpfBruto, out string cpfNormalizado)
    {
        cpfNormalizado = string.Empty;

        if (string.IsNullOrWhiteSpace(cpfBruto))
            return false;

        var digitos = new string(cpfBruto.Where(char.IsDigit).ToArray());

        if (digitos.Length != 11 || !EhValido(digitos))
            return false;

        cpfNormalizado = digitos;
        return true;
    }

    private static bool EhValido(string cpf)
    {
        if (cpf.Distinct().Count() == 1)
            return false;

        var soma = 0;
        for (var i = 0; i < 9; i++)
            soma += (cpf[i] - '0') * (10 - i);

        var resto = soma % 11;
        var digito1 = resto < 2 ? 0 : 11 - resto;
        if (digito1 != cpf[9] - '0') return false;

        soma = 0;
        for (var i = 0; i < 10; i++)
            soma += (cpf[i] - '0') * (11 - i);

        resto = soma % 11;
        var digito2 = resto < 2 ? 0 : 11 - resto;

        return digito2 == cpf[10] - '0';
    }
}
