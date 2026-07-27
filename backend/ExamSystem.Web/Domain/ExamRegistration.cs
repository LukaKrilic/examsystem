namespace ExamSystem.Web.Domain;

public class ExamRegistration
{
    public long Id { get; set; }
    public long StudentId { get; set; }
    public long ExamId { get; set; }

    public Student Student { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
}
