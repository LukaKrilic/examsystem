namespace ExamSystem.Web.Domain;

public class ExamAccessCode
{
    public long Id { get; set; }
    public string ExamId { get; set; } = null!;          // external Infoeduka examId, no FK
    public string Group1Code { get; set; } = null!;
    public string Group2Code { get; set; } = null!;
}
