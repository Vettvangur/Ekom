using Newtonsoft.Json;

namespace Ekom.Models;

public class StockRequest
{
    [JsonProperty("storeAlias")]
    public string StoreAlias { get; set; }
    [JsonProperty("value")]
    public decimal? Value { get; set; }
}
