using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.ActionFilters;

/// <summary>
/// Restricts the search options of every product query bound from a public request.
/// See <see cref="SearchRequestGuard.RestrictToPublic"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class PublicSearchInputAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        IReadOnlyCollection<string>? allowedFields = null;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is not ProductQueryBase query)
            {
                continue;
            }

            allowedFields ??= context.HttpContext.RequestServices.GetRequiredService<Configuration>().PublicSearchFields;
            SearchRequestGuard.RestrictToPublic(query, allowedFields);
        }
    }
}
