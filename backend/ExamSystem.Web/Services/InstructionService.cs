using ExamSystem.Web.Data;
using ExamSystem.Web.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class InstructionService(ExamDbContext db)
{
    public async Task<InstructionsHtmlResponse?> GetHtmlAsync(string instructionId)
    {
        var instruction = await db.Instructions
            .FirstOrDefaultAsync(i => i.InstructionId == instructionId);

        return instruction is null
            ? null
            : new InstructionsHtmlResponse(instruction.InstructionId, instruction.VersionNo,
                new InstructionsDto(instruction.HtmlHr, instruction.HtmlEn));
    }
}
