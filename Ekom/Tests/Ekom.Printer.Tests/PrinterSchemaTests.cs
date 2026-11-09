using Ekom.Printer.Storage;
using LinqToDB.DataProvider.SqlServer;
using Xunit;

namespace Ekom.Printer.Tests;

public sealed class PrinterSchemaTests
{
    [Theory]
    [InlineData(typeof(PrintJob), "CompletedUtc,SnapshotJson,Html,ErrorCode")]
    [InlineData(typeof(PrinterDelivery), "ReportedUtc,Submitted,SpoolJobId")]
    [InlineData(typeof(PrinterStation), "")]
    [InlineData(typeof(CapturedStatus), "ErrorCode")]
    [InlineData(typeof(PrinterManagerAction), "NewJobId")]
    [InlineData(typeof(PrinterSchemaVersion), "")]
    public void SqlServerSchemaOnlyAllowsNullForOptionalColumns(Type entityType, string optionalColumns)
    {
        var provider = SqlServerTools.GetDataProvider(SqlServerVersion.v2012, SqlServerProvider.MicrosoftDataSqlClient);
        var descriptor = provider.MappingSchema.GetEntityDescriptor(entityType);
        var optional = optionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(descriptor.Columns);
        foreach (var column in descriptor.Columns)
        {
            Assert.Equal(optional.Contains(column.ColumnName), column.CanBeNull);
            if (column.IsPrimaryKey) Assert.False(column.CanBeNull);
        }
    }
}
