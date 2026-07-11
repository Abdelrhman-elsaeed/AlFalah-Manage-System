namespace AlFalah.Shared.Models;

/// <summary>
/// Standard API response envelope used across all endpoints.
/// </summary>
public class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Success(T data, string message = "")
        => new() { IsSuccess = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string error, string message = "")
        => new() { IsSuccess = false, Errors = new List<string> { error }, Message = message };

    public static ApiResponse<T> Fail(List<string> errors, string message = "")
        => new() { IsSuccess = false, Errors = errors, Message = message };
}

/// <summary>
/// Non-generic version for commands/actions that return no data.
/// </summary>
public class ApiResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();

    public static ApiResponse Success(string message = "")
        => new() { IsSuccess = true, Message = message };

    public static ApiResponse Fail(string error, string message = "")
        => new() { IsSuccess = false, Errors = new List<string> { error }, Message = message };

    public static ApiResponse Fail(List<string> errors, string message = "")
        => new() { IsSuccess = false, Errors = errors, Message = message };
}
