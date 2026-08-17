using System.Security.Claims;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Infoeduka;

namespace ExamSystem.Web.Auth;

// Maps the SAML attributes AAI@EduHr (or the dev Keycloak realm) publishes as claims to a student
// record held by Infoeduka. Never auto-creates students — an unmatched identity is always an error,
// and we couldn't create one anyway: that table isn't ours.
public class SamlUserService(IInfoedukaClient infoeduka)
{
    public async Task<InfoedukaStudent> ResolveStudentAsync(ClaimsPrincipal user)
    {
        var principal = user.FindFirstValue("hrEduPersonUniqueID");   // ime.prezime@algebra.hr
        var jmbag = user.FindFirstValue("hrEduPersonUniqueNumber");
        if (principal is null && jmbag is null)
            throw new UnknownStudentException("SAML assertion missing identity attributes");

        // A null result means Infoeduka answered 404 — a known, expected outcome that becomes the
        // friendly "account not registered for exams" page, not a 500.
        return await infoeduka.ResolveStudentByIdentityAsync(principal, jmbag)
            ?? throw new UnknownStudentException(principal ?? jmbag!);
    }
}
