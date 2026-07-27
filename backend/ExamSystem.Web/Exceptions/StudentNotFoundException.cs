namespace ExamSystem.Web.Exceptions;

public class StudentNotFoundException(string studentId) : Exception($"Student '{studentId}' not found");
