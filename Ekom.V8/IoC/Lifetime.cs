namespace EkomV8.IoC
{
    /// <summary>
    /// How the object in a container is managed
    /// </summary>
    public enum Lifetime
    {
        ExternallyOwned,
        Transient,
        Singleton,
        Request
    }
}
