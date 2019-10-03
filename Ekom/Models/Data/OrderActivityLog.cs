using NPoco;
using System;

namespace Ekom.Models.Data
{
    /// <summary>
    /// Activity log of order
    /// </summary>
    [TableName("EkomOrdersActivityLog")]
    [PrimaryKey("UniqueId", AutoIncrement = false)]
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
