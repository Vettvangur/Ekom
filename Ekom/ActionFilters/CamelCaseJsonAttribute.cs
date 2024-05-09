using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Buffers;

namespace Ekom.ActionFilters;
public sealed class CamelCaseJsonAttribute : ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult)
        {
            // Remove the existing JSON formatter
            objectResult.Formatters.RemoveType<NewtonsoftJsonOutputFormatter>();

            // Create a new JsonSerializerSettings object with CamelCasePropertyNamesContractResolver
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };


            // Create the formatter with the new settings
            objectResult.Formatters.Add(new NewtonsoftJsonOutputFormatter(
                               serializerSettings,
                               context.HttpContext.RequestServices.GetRequiredService<ArrayPool<char>>(),
                               context.HttpContext.RequestServices.GetRequiredService<IOptions<MvcOptions>>().Value, null));

        }
    }
}
