namespace ExamSystem.Web.Exceptions;

public class ExamNotFoundException(string examId) : Exception($"Exam '{examId}' not found");
