using ExamSystem.Web.Data;
using ExamSystem.Web.Domain;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

public class ScreenshotService(ExamDbContext db, IConfiguration config)
{
    public async Task<ScreenshotResponse> SaveAsync(ScreenshotRequest request)
    {
        var session = await db.ExamSessions.FirstOrDefaultAsync(s => s.SessionId == request.SessionId)
            ?? throw new NoActiveSessionException(request.SessionId);

        var dir = config["Exam:ScreenshotDir"] ?? "./screenshots";
        Directory.CreateDirectory(dir);

        var fileName = $"{request.SessionId}_{request.Timestamp:yyyyMMddHHmmssfff}.png";
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(request.Image));

        var screenshot = new Screenshot
        {
            ExamSessionId = session.Id,
            TakenAt = request.Timestamp,
            ImagePath = path,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Screenshots.Add(screenshot);
        await db.SaveChangesAsync();

        return new ScreenshotResponse(screenshot.Id, screenshot.ImagePath);
    }
}
