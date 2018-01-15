using Ekom.Models.Behaviors;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Represents an object that has constraints available
    /// </summary>
    public interface IConstrained
    {

        Constraints Constraints { get; }
    }
}
