namespace ExamSystem.Web.Domain;

public class LockedExam
{
    public long Id { get; set; }
    public string StudentId { get; set; } = null!;       // external, no FK
    public string ExamId { get; set; } = null!;          // external, no FK
    public string Reason { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
