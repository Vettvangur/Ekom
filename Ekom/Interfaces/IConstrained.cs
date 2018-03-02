namespace Ekom.Interfaces
{
    /// <summary>
    /// Represents an object that has constraints available
    /// </summary>
    public interface IConstrained
    {
        /// <summary>
        /// Start + end price ranges and applicable zones
        /// </summary>
        IConstraints Constraints { get; }
    }
}
