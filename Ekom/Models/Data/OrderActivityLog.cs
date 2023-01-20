using LinqToDB.Mapping;
using System;

namespace Ekom.Models
{
    /// <summary>
    /// Activity log of order
    /// </summary>
    [Table(Name = "EkomOrdersActivityLog")]
    public class OrderActivityLog
    {
        [PrimaryKey, NotNull]
        public Guid UniqueID { get; set; }
        [Column, NotNull]
        public Guid Key { get; set; }
        [Column, NotNull]
        public string Log { get; set; }
        [Column, NotNull]
        public string UserName { get; set; }
        [Column, NotNull]
        public DateTime Date { get; set; }
        public string OrderNumber { get; set; }
    }
}
