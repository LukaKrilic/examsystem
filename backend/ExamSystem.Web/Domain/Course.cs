namespace ExamSystem.Web.Domain;

public class Course
{
    public long Id { get; set; }
    public string CourseId { get; set; } = null!;        // '2876'
    public string CourseNameHr { get; set; } = null!;
    public string CourseNameEn { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
