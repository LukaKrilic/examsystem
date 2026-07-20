namespace ExamSystem.Web.Domain;

public class Screenshot
{
    public long Id { get; set; }
    public long ExamSessionId { get; set; }
    public DateTimeOffset TakenAt { get; set; }
    public string ImagePath { get; set; } = null!;       // stored on server disk, not in DB
    public DateTimeOffset CreatedAt { get; set; }
}
