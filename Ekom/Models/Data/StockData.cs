using LinqToDB.Mapping;
using System;
using System.ComponentModel.DataAnnotations;

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
        /// {Two letter country code}_{Guid} or:
        /// IS_d6da9d30-9246-4856-b66b-5411086b84d9
        /// In other cases this is simply a Guid
        /// </summary>
        [PrimaryKey, NotNull]
        [Column(Length = 39)]
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
