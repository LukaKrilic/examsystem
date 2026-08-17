using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

// Purely local: access codes are the exam system's own data, keyed by the external exam id. No
// Infoeduka call is needed to translate an id any more — the column IS the external id.
public class AccessCodeService(ExamDbContext db)
{
    public async Task<AccessCodesResponse> GetAsync(string examId)
    {
        var codes = await db.ExamAccessCodes.FirstOrDefaultAsync(c => c.ExamId == examId)
            ?? throw new ExamNotFoundException(examId);

        return new AccessCodesResponse(examId, new AccessCodesDto(codes.Group1Code, codes.Group2Code));
    }
}
