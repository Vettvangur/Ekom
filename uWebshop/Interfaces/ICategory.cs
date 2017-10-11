namespace uWebshop.Interfaces
{
    public interface ICategory : INodeEntityWithUrl
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
