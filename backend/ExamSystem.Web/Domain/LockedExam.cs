namespace ExamSystem.Web.Domain;

public class LockedExam
{
    public long Id { get; set; }
    public long StudentId { get; set; }
    public long ExamId { get; set; }
    public string Reason { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
}
