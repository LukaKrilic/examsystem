namespace ExamSystem.Web.Domain;

public class LearningOutcome
{
    public long Id { get; set; }
    public long CourseId { get; set; }
    public string OutcomeCode { get; set; } = null!;     // 'I1'..'I5'

    public Course Course { get; set; } = null!;
}
