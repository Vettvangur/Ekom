using LinqToDB.Mapping;

namespace Ekom.Models
{
    /// <summary>
    /// Unfinished
    /// </summary>
    [Table(Name = "EkomCustomerData")]
    public class CustomerData
    {
        [PrimaryKey]
        public string UniqueId { get; internal set; }

    }
}
