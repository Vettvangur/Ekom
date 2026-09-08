using Ekom.Models;
using Newtonsoft.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class WarehouseStockEditorValueTests
{
    [Fact]
    public void CamelCaseEditorPayload_PreservesZeroAndUnsetBalances()
    {
        const string json = """
            {
              "sku": "ABC-123",
              "items": [
                {
                  "storeAlias": "default",
                  "warehouseKey": "d8cf6892-f075-430b-b94b-f43f5828bbca",
                  "code": "MAIN",
                  "name": "Main warehouse",
                  "visible": true,
                  "balance": 0
                },
                {
                  "storeAlias": "default",
                  "warehouseKey": "1365c4a6-8dca-4369-969b-2d4dff62a901",
                  "code": "SECONDARY",
                  "name": "Secondary warehouse",
                  "visible": false,
                  "balance": null
                }
              ]
            }
            """;

        WarehouseStockEditorValue? value = JsonConvert.DeserializeObject<WarehouseStockEditorValue>(json);

        Assert.NotNull(value);
        Assert.Equal("ABC-123", value.Sku);
        Assert.Equal(0m, value.Items[0].Balance);
        Assert.Null(value.Items[1].Balance);
    }
}
