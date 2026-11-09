using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace Ekom.Controllers;

[Route("ekom/manager/customers")]
[CamelCaseJson]
[UmbracoUserAuthorize]
public sealed class EkomCustomersController(CustomerRepository repository, IManagerAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string store, DateTime start, DateTime end, string orderStatus = "CompletedOrders", string query = "", string segment = "all", string sort = "newest", int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!access.CanAccessStore(store)) return Forbid();
        if (!ValidRange(start, end) || page < 1 || page > 1000000 || pageSize < 1 || pageSize > 100 || !ValidSegment(segment)) return BadRequest("Invalid customer filters.");
        return Ok(await repository.SearchAsync(store, start, end, orderStatus, query ?? "", segment, sort, page, pageSize, ct).ConfigureAwait(false));
    }

    [HttpGet("detail")]
    public async Task<IActionResult> Detail(string store, string identity, int page = 1, bool other = false, CancellationToken ct = default)
    {
        if (!access.CanAccessStore(store)) return Forbid();
        if (string.IsNullOrWhiteSpace(identity) || identity.Length > 200 || page < 1 || page > 1000000) return BadRequest("Invalid customer identity or page.");
        var detail = await repository.DetailAsync(store, identity, page, other, ct).ConfigureAwait(false);
        return detail == null ? NotFound() : Ok(detail);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(string store, DateTime start, DateTime end, string orderStatus = "CompletedOrders", string query = "", string segment = "all", string sort = "newest", CancellationToken ct = default)
    {
        if (!access.CanAccessStore(store)) return Forbid();
        if (!ValidRange(start, end) || !ValidSegment(segment)) return BadRequest("Invalid customer filters.");
        var csv = new StringBuilder("Name,Email,Username,First order,Last order,Orders,Days since last order,Average reorder days,Currency,Lifetime spend,Average order\r\n");
        int page = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var result = await repository.SearchAsync(store, start, end, orderStatus, query ?? "", segment, sort, page, 100, ct).ConfigureAwait(false);
            foreach (var customer in result.Customers)
            foreach (var spend in customer.Spend)
            {
                csv.AppendLine(string.Join(",", new[] { customer.Name, customer.Email, customer.Username, customer.FirstOrder.ToString("O"), customer.LastOrder.ToString("O"), customer.Orders.ToString(CultureInfo.InvariantCulture), customer.DaysSinceLastOrder.ToString(CultureInfo.InvariantCulture), customer.AverageReorderDays?.ToString("0.##", CultureInfo.InvariantCulture), spend.Currency, spend.Total.ToString(CultureInfo.InvariantCulture), spend.AverageOrder.ToString(CultureInfo.InvariantCulture) }.Select(Csv)));
            }
            if (page++ >= result.TotalPages) break;
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "customers.csv");
    }

    private static bool ValidRange(DateTime start, DateTime end) => start != default && end >= start && end.Date < DateTime.MaxValue.Date;
    private static bool ValidSegment(string segment) => segment is "all" or "new" or "returning";
    private static string Csv(string? value)
    {
        value ??= "";
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
