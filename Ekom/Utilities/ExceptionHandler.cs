using Ekom.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Ekom.Utilities;

static class ExceptionHandler
{
    /// <summary>
    /// Standardizes exception handling in Ekom Controllers for the most common exception types.
    /// </summary>
    /// <typeparam name="T">Only ActionResult and HttpResponseMessage are likely to make sense here</typeparam>
    /// <returns></returns>
    public static IActionResult? Handle(
        Exception exception,
        Func<IActionResult>? defaultHandler = null,
        Func<IActionResult>? preHandler = null)
    {
        if (preHandler != null)
        {
            var pre = preHandler();
            if (pre != null) return pre;
        }

        return exception switch
        {
            OrderLineNegativeException => new StatusCodeResult((int)HttpStatusCode.BadRequest),
            OrderLineNotFoundException => new NotFoundResult(),
            ProductNotFoundException => new NotFoundResult(),
            VariantNotFoundException => new NotFoundResult(),
            NotEnoughStockException => new StatusCodeResult((int)HttpStatusCode.Conflict),
            DiscountNotFoundException => new NotFoundResult(),
            DiscountHasNoUsageException => new StatusCodeResult((int)HttpStatusCode.NotAcceptable),
            DiscountUnableToFindCouponException => new NotFoundResult(),
            StoreNotFoundException => new NotFoundResult(),

            EkomHttpException httpEx => new ObjectResult(new { message = httpEx.Message })
            {
                StatusCode = (int)httpEx.StatusCode
            },

            EkomProblemDetailsException pdEx => new ObjectResult(pdEx.ProblemDetails)
            {
                StatusCode = pdEx.ProblemDetails.Status
            },

            _ when defaultHandler != null => defaultHandler(),
            _ => null
        };
    }
}
