namespace Ekom.Services;

/// <summary>Shared by adapter initialization and the reservation worker.</summary>
internal sealed class StockReservationReadiness
{
    private int _schemaReady;
    private int _initialized;
    public bool IsReady => Volatile.Read(ref _schemaReady) == 1 && Volatile.Read(ref _initialized) == 1;
    public void SchemaReady() => Volatile.Write(ref _schemaReady, 1);
    public void Initialized() => Volatile.Write(ref _initialized, 1);
    public void Initializing() => Volatile.Write(ref _initialized, 0);
}
