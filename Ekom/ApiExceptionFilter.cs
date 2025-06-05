using Ekom.Exceptions;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Ekom;

public class ApiExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is HttpResponseException httpEx)
        {
            context.Result = new ObjectResult(httpEx.Message)
            {
                StatusCode = (int)httpEx.StatusCode
            };
            context.ExceptionHandled = true;
            return;
        }

        var result = ExceptionHandler.Handle<IActionResult>(context.Exception);

        if (result != null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
        else
        {
            _logger.LogError(context.Exception, "Unhandled exception");
            context.Result = new ObjectResult("An unexpected error occurred")
            {
                StatusCode = 500
            };
            context.ExceptionHandled = true;
        }
    }
}
