namespace Ekom.Models;

public sealed class OrderActivityLogEntry
{
    public string Message { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public OrderActivityLogType LogType { get; set; }
}
