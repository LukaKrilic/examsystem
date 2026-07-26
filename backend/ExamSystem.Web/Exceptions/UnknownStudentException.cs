namespace ExamSystem.Web.Exceptions;

// Thrown when a validated SAML assertion carries no identity attribute that matches a seeded
// Student row (or no identity attribute at all). Caught by UnknownStudentExceptionFilter.
public class UnknownStudentException(string message) : Exception(message);
