namespace ExamSystem.Web.Exceptions;

public class SessionAlreadyConfirmedException(string sessionId) : Exception($"Session '{sessionId}' is already confirmed");
