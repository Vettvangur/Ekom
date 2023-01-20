#if NETCOREAPP
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace Ekom.Exceptions
{
    public class HttpResponseException : Exception
    {
        public HttpResponseException(HttpResponseMessage httpResponseMessage) =>
            (StatusCode, Value) = (httpResponseMessage.StatusCode, httpResponseMessage.Content);
        public HttpResponseException(HttpStatusCode statusCode) =>
            (StatusCode) = statusCode;

        public HttpStatusCode StatusCode { get; }

        public HttpContent? Value { get; }
    }

    public class HttpResponseExceptionFilter : IActionFilter, IOrderedFilter
    {
        public int Order => int.MaxValue - 10;

        public void OnActionExecuting(ActionExecutingContext context) { }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is HttpResponseException httpResponseException)
            {
                context.Result = new ContentResult()
                {
                    StatusCode = (int)httpResponseException.StatusCode,
                    Content = httpResponseException.Value?.ReadAsStringAsync().Result,
                    ContentType = httpResponseException.Value?.Headers.ContentType.MediaType,
                };

                context.ExceptionHandled = true;
            }
        }
    }
}
#endif
