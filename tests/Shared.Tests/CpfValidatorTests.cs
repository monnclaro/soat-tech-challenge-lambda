using SoatTechChallenge.Lambda.Shared;
using Xunit;

namespace Shared.Tests;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void TryNormalizar_QuandoCpfValido_RetornaTrueENormaliza(string cpf)
    {
        var ok = CpfValidator.TryNormalizar(cpf, out var normalizado);

        Assert.True(ok);
        Assert.Equal("52998224725", normalizado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("00000000000")]
    [InlineData("12345678900")]
    public void TryNormalizar_QuandoCpfInvalido_RetornaFalse(string? cpf)
    {
        var ok = CpfValidator.TryNormalizar(cpf, out var normalizado);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalizado);
    }
}
