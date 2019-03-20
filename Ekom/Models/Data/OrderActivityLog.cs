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
        public Guid UniqueID { get; set; }
        public Guid Key { get; set; }
        public string Log { get; set; }
        public string UserName { get; set; }
        public DateTime Date { get; set; }
        [ResultColumn]
        public string OrderNumber { get; set; }
    }
}
