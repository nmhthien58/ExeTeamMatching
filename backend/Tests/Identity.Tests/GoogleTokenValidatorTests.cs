using BuildingBlocks.Errors;
using Identity.Services.Google;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Tests;

/// <summary>Token Google sai định dạng → 401 invalid-token (không phải 500). Không gọi mạng.</summary>
public sealed class GoogleTokenValidatorTests
{
    [Theory]
    [InlineData("abc.def.ghi")]
    [InlineData("khong-phai-jwt")]
    [InlineData("eyJhbGciOiJSUzI1NiJ9.e30")]
    public async Task Malformed_token_is_invalid_token(string idToken)
    {
        var validator = new GoogleTokenValidator(
            Options.Create(new GoogleAuthOptions { ClientId = "client.apps.googleusercontent.com" }),
            NullLogger<GoogleTokenValidator>.Instance);

        var error = await Assert.ThrowsAsync<HttpStatusException>(() => validator.ValidateAsync(idToken, CancellationToken.None));

        Assert.Equal(401, error.Status);
        Assert.Equal("invalid-token", error.Code);
    }
}
