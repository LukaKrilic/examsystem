namespace ExamSystem.Web.Domain;

public class StudentOutcomePoints
{
    public long Id { get; set; }
    public long StudentId { get; set; }
    public long LearningOutcomeId { get; set; }
    public decimal TotalPointsEarned { get; set; }
    public decimal TotalPointsMax { get; set; }
    public decimal ExamPointsEarned { get; set; }
    public decimal ExamPointsMax { get; set; }
}
