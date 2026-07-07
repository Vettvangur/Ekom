namespace Ekom.Models.Manager;

public abstract class OrderManagerActionExecutionResult
{
    public string? Message { get; init; }
}

public sealed class OrderManagerActionSuccessResult : OrderManagerActionExecutionResult
{
}

public sealed class OrderManagerActionBadRequestResult : OrderManagerActionExecutionResult
{
}

public sealed class OrderManagerActionFileResult : OrderManagerActionExecutionResult
{
    public required byte[] Content { get; init; }

    public required string ContentType { get; init; }

    public string? FileName { get; init; }
}
