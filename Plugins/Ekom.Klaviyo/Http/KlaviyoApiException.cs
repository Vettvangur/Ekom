namespace Ekom.Klaviyo.Http;

internal class KlaviyoApiException : Exception
{
    public KlaviyoApiException(string message, int statusCode, string path, string responseBody, string requestJson)
        : base(message)
    {
        StatusCode = statusCode;
        Path = path;
        ResponseBody = responseBody;
        RequestJson = requestJson;
    }

    public int StatusCode { get; }
    public string Path { get; }
    public string ResponseBody { get; }
    public string RequestJson { get; }
}
