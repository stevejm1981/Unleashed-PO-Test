namespace UnleashedPOTest;

/// <summary>
/// Thrown when the Unleashed API returns a non-success HTTP status code.
/// </summary>
public sealed class UnleashedApiException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public UnleashedApiException(int statusCode, string? responseBody)
        : base($"Unleashed API returned {statusCode}. Body: {responseBody}")
    {
        StatusCode   = statusCode;
        ResponseBody = responseBody;
    }
}
