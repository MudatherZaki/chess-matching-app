namespace ChessApp.Backend.DTOs;

public class ErrorResponse
{
    public string Error { get; set; } = null!;
    public string Message { get; set; } = null!;
    public List<FieldError>? Details { get; set; }
}

public class FieldError
{
    public string Field { get; set; } = null!;
    public string Message { get; set; } = null!;
}

public class ValidationErrorResponse
{
    public string Error { get; set; } = "ValidationError";
    public string Message { get; set; } = "Validation failed";
    public List<FieldError> Details { get; set; } = new();
}
