using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class AccessCodeService(ExamDbContext db)
{
    public async Task<AccessCodesResponse> GetAsync(string examId)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.ExamId == examId)
            ?? throw new ExamNotFoundException(examId);

        var codes = await db.ExamAccessCodes.FirstOrDefaultAsync(c => c.ExamId == exam.Id)
            ?? throw new ExamNotFoundException(examId);

        return new AccessCodesResponse(examId, new AccessCodesDto(codes.Group1Code, codes.Group2Code));
    }
}
