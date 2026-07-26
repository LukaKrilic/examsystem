namespace ExamSystem.Web.Exceptions;

public class NoActiveSessionException(string studentOrSessionId) : Exception($"No active session for '{studentOrSessionId}'");
