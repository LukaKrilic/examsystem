namespace ExamSystem.Web.Domain;

public class Exam
{
    public long Id { get; set; }
    public string ExamId { get; set; } = null!;          // 'EXAM-1001'
    public long CourseId { get; set; }
    public DateTimeOffset ExamDateTime { get; set; }
    public string Classroom { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public Course Course { get; set; } = null!;
}
