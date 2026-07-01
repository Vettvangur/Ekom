using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Ekom.Exceptions;

public class EkomProblemDetailsException : Exception
{
    public ProblemDetails ProblemDetails { get; }

    public EkomProblemDetailsException(string title, string detail, HttpStatusCode statusCode)
        : base(detail)
    {
        ProblemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = (int)statusCode
        };
    }
}
