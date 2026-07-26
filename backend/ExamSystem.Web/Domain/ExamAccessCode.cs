namespace ExamSystem.Web.Domain;

public class ExamAccessCode
{
    public long Id { get; set; }
    public long ExamId { get; set; }
    public string Group1Code { get; set; } = null!;
    public string Group2Code { get; set; } = null!;

    public Exam Exam { get; set; } = null!;
}
