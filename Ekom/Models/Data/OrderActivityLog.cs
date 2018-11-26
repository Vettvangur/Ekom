using System;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Ekom.Models.Data
{
    /// <summary>
    /// Activity log of order
    /// </summary>
    [TableName("EkomOrdersActivityLog")]
    [PrimaryKey("UniqueId", autoIncrement = false)]
    public class OrderActivityLog
    {
        /// <summary>
        /// </summary>
        [Index(IndexTypes.Clustered)]
        public Guid UniqueId { get; set; }

        /// <summary>
        /// Order ID
        /// </summary>
        public Guid OrderId { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public string Log { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public string UserName { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public int UserId { get; internal set; }

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
