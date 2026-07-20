namespace ExamSystem.Web.Domain;

public class Student
{
    public long Id { get; set; }
    public string StudentId { get; set; } = null!;      // '2023001234'
    public string Jmbag { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AaiPrincipal { get; set; }            // hrEduPersonUniqueID
    public DateTimeOffset CreatedAt { get; set; }
}
