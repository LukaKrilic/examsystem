namespace ExamSystem.Web.Exceptions;

public class ExamLockedException(string examId) : Exception($"Exam '{examId}' is locked for this student");
