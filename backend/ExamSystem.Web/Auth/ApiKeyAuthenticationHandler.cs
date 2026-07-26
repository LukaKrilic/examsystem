using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ExamSystem.Web.Auth;

// Authenticates machine clients (Electron main process, Infoeduka export consumer) via the
// X-Exam-Api-Key header. Returns NoResult() — never Fail() — on a missing/invalid key, so it can
// never block or downgrade a student's cookie authentication on requests that carry both.
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Exam-Api-Key", out var provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expected = config["Exam:ApiKey"]!;
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided.ToString()),
                Encoding.UTF8.GetBytes(expected)))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "machine-client"),
             new Claim(ClaimTypes.Role, "MACHINE")],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
