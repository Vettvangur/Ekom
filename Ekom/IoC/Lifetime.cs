namespace Ekom.IoC
{
    /// <summary>
    /// how the object in a container is managed
    /// </summary>
    public enum Lifetime
    {
        ExternallyOwned,
        Transient,
        Singleton,
        Request
    }
}
