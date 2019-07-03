namespace EkomV8.IoC
{
    /// <summary>
    /// Abstract class used to instantiate an object
    /// </summary>
    public abstract class Resolver
    {
        public abstract TService Resolve<TService>();
    }
}
