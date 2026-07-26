using System.Security.Claims;
using ExamSystem.Web.Data;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Auth;

// Maps the SAML attributes AAI@EduHr (or the dev Keycloak realm) publishes as claims to a seeded
// Student row. Never auto-creates students — an unmatched identity is always an error.
public class SamlUserService(ExamDbContext db)
{
    public async Task<Student> ResolveStudentAsync(ClaimsPrincipal user)
    {
        var principal = user.FindFirstValue("hrEduPersonUniqueID");   // ime.prezime@algebra.hr
        var jmbag = user.FindFirstValue("hrEduPersonUniqueNumber");
        if (principal is null && jmbag is null)
            throw new UnknownStudentException("SAML assertion missing identity attributes");

        return await db.Students.FirstOrDefaultAsync(s => s.AaiPrincipal == principal)
            ?? await db.Students.FirstOrDefaultAsync(s => s.Jmbag == jmbag)
            ?? throw new UnknownStudentException(principal ?? jmbag!);
    }
}
