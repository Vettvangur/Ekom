using NPoco;
using System;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Ekom.Models.Data
{
    /// <summary>
    /// Current stock of a given item
    /// </summary>
    [TableName("EkomStock")]
    [PrimaryKey("UniqueId", AutoIncrement = false)]
    public class StockData
    {
        /// <summary>
        /// With per store stock this id has the following format
        /// {Two letter country code}_{Guid} or:
        /// IS_d6da9d30-9246-4856-b66b-5411086b84d9
        /// In other cases this is simply a Guid
        /// </summary>
        [Length(39)]
        [PrimaryKeyColumn(AutoIncrement = false)]
        public string UniqueId { get; internal set; }

        /// <summary>
        /// Unit count
        /// </summary>
        public int Stock { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime CreateDate { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime UpdateDate { get; internal set; }
    }
}
