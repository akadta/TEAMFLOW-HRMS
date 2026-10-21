namespace hrms.Common;

public class ServiceResult<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ServiceResult<T> Ok(T data) =>
        new() { Data = data, Success = true };

    public static ServiceResult<T> Fail(params string[] errors) =>
        new() { Success = false, Errors = errors.ToList() };
}

