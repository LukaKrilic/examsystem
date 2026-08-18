using System.Security.Claims;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Infoeduka;

namespace ExamSystem.Web.Auth;

// Maps the SAML attributes AAI@EduHr (or the dev Keycloak realm) publishes as claims to a student
// record held by Infoeduka. Never auto-creates students — an unmatched identity is always an error,
// and we couldn't create one anyway: that table isn't ours.
//
// The mapping is resolved ONCE, when the cookie is issued (see AttachStudentClaimsAsync, wired to the
// cookie scheme's OnSigningIn), and carried in the cookie from then on. Otherwise every page load —
// including the during-exam page of an already-confirmed session — would need Infoeduka to be up,
// which would defeat the whole point of the confirm-time snapshot (domain rule 10).
public class SamlUserService(IInfoedukaClient infoeduka, ILogger<SamlUserService> logger)
{
    private const string StudentIdClaim = "exam:studentId";
    private const string FullNameClaim = "exam:studentFullName";
    private const string JmbagClaim = "exam:studentJmbag";

    public async Task<InfoedukaStudent> ResolveStudentAsync(ClaimsPrincipal user)
    {
        if (TryReadFromClaims(user) is { } cached)
            return cached;

        // No claims on the cookie: either it predates this mechanism, or Infoeduka could not be
        // reached / did not know the identity at sign-in time. Resolve live so the outcome — and in
        // particular the friendly "not registered for exams" page — is exactly what it always was.
        var (principal, jmbag) = ReadIdentity(user);
        if (principal is null && jmbag is null)
            throw new UnknownStudentException("SAML assertion missing identity attributes");

        return await infoeduka.ResolveStudentByIdentityAsync(principal, jmbag)
            ?? throw new UnknownStudentException(principal ?? jmbag!);
    }

    // Called while the authentication cookie is being issued. Deliberately never throws: an unknown
    // identity or an Infoeduka outage simply leaves the claims off, and the first page load then takes
    // the live path above and produces the normal UnknownStudentException → friendly error page.
    public async Task AttachStudentClaimsAsync(ClaimsPrincipal user)
    {
        if (user.Identity is not ClaimsIdentity identity || TryReadFromClaims(user) is not null)
            return;

        var (principal, jmbag) = ReadIdentity(user);
        if (principal is null && jmbag is null)
            return;

        InfoedukaStudent? student;
        try
        {
            student = await infoeduka.ResolveStudentByIdentityAsync(principal, jmbag);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Infoeduka unreachable while issuing the cookie for {Identity}; " +
                                  "the identity will be resolved on the next request instead",
                principal ?? jmbag);
            return;
        }

        if (student is null)
        {
            logger.LogWarning("Infoeduka does not know identity {Identity}", principal ?? jmbag);
            return;
        }

        identity.AddClaim(new Claim(StudentIdClaim, student.StudentId));
        identity.AddClaim(new Claim(FullNameClaim, student.FullName));
        identity.AddClaim(new Claim(JmbagClaim, student.Jmbag));
    }

    private static (string? Principal, string? Jmbag) ReadIdentity(ClaimsPrincipal user)
        => (user.FindFirstValue("hrEduPersonUniqueID"),        // ime.prezime@algebra.hr
            user.FindFirstValue("hrEduPersonUniqueNumber"));

    private static InfoedukaStudent? TryReadFromClaims(ClaimsPrincipal user)
    {
        var studentId = user.FindFirstValue(StudentIdClaim);
        var fullName = user.FindFirstValue(FullNameClaim);
        var jmbag = user.FindFirstValue(JmbagClaim);

        return studentId is null || fullName is null || jmbag is null
            ? null
            : new InfoedukaStudent(studentId, fullName, jmbag);
    }
}
