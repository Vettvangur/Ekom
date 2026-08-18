using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Models.Indexing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaContentRecordSizeInspectorTests
{
    [Fact]
    public void Measures_Serialized_Utf8_Record_Size()
    {
        var record = CreateRecord();
        record.Data["body"] = "Halló 🌋";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var result = AlgoliaContentRecordSizeInspector.Inspect(record);

        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(record, options).Length, result.SizeBytes);
    }

    [Fact]
    public void Reports_Largest_Fields_Without_Field_Values()
    {
        var record = CreateRecord();
        var body = new string('x', 1_000);
        record.Data["body"] = body;
        record.Data["summary"] = "Short";

        var result = AlgoliaContentRecordSizeInspector.Inspect(record);

        var largestField = Assert.Single(result.LargestFields, field => field.Name == "body");
        Assert.True(largestField.SizeBytes > 1_000);
        Assert.DoesNotContain(result.LargestFields, field => field.Name == body);
    }

    [Theory]
    [InlineData("{\"message\":\"Record is too big\",\"objectID\":\"record-key\",\"status\":400}", true, "record-key")]
    [InlineData("{\"message\":\"Record at the position 7 objectID=record-key is too big size=102824/100000 bytes.\",\"status\":400}", true, "record-key")]
    [InlineData("{\"message\":\"Record is too big\",\"status\":400}", false, null)]
    [InlineData("not-json", false, null)]
    public void Extracts_ObjectId_From_Algolia_Error(string message, bool expectedResult, string? expectedObjectId)
    {
        var result = AlgoliaContentRecordSizeInspector.TryGetObjectId(message, out var objectId);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedObjectId, objectId);
    }

    [Fact]
    public void Oversized_Record_Options_Default_To_Fail_At_Algolia_Limit()
    {
        var options = new AlgoliaContentIndexingOptions();

        Assert.Equal(AlgoliaOversizedRecordBehavior.Fail, options.OversizedRecords.Behavior);
        Assert.Equal(100_000, options.OversizedRecords.MaxSizeBytes);
    }

    private static AlgoliaContentRecord CreateRecord()
        => new()
        {
            ObjectID = "record-key",
            NodeId = 42,
            ContentTypeAlias = "article",
            Url = "/articles/example/",
            Name = "Example article",
            UpdateDate = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc),
            CreateDate = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc)
        };
}
