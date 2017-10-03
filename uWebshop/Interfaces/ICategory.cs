namespace uWebshop.Interfaces
{
    public interface ICategory : INodeEntitiyWithUrl
    {
        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>
        /// The title.
        /// </value>
        string Title { get; }

    }
}
