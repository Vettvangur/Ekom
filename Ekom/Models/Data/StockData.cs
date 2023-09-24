using LinqToDB.Mapping;

namespace Ekom.Models
{
    /// <summary>
    /// Current stock of a given item
    /// </summary>
    [Table(Name = "EkomStock")]
    public class StockData
    {
        /// <summary>
        /// With per store stock this id has the following format
        /// {StoreAlias}_{Guid} or:{Guid}
        /// 
        /// In other cases this is simply a Guid
        /// </summary>
        [PrimaryKey, NotNull]
        [Column(Length = 255)]
        public string UniqueId { get; internal set; }

        /// <summary>
        /// Unit count
        /// </summary>
        [Column, NotNull]
        public int Stock { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        [Column, NotNull]
        public DateTime CreateDate { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        [Column, NotNull]
        public DateTime UpdateDate { get; internal set; }
    }
}
