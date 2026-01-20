using System.Net;

namespace Ekom.Exceptions;

public class EkomHttpException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public EkomHttpException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
