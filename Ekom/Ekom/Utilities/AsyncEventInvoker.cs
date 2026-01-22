namespace Ekom.Utilities;

internal static class AsyncEventInvoker
{
    public static async Task InvokeAsync<TArgs>(
        Func<object, TArgs, CancellationToken, Task>? evt,
        object sender,
        TArgs args,
        CancellationToken ct = default)
        where TArgs : EventArgs
    {
        if (evt is null) return;

        foreach (var d in evt.GetInvocationList())
        {
            ct.ThrowIfCancellationRequested();

            var handler = (Func<object, TArgs, CancellationToken, Task>)d;
            await handler(sender, args, ct).ConfigureAwait(false);
        }
    }

    // Optional: bridge older async events without CancellationToken
    public static Task InvokeAsync<TArgs>(
        Func<object, TArgs, Task>? evt,
        object sender,
        TArgs args,
        CancellationToken ct = default)
        where TArgs : EventArgs
        => InvokeAsync(
            evt is null ? null : (s, a, _) => evt(s, a),
            sender,
            args,
            ct
        );
}
