using NPoco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Ekom.Manager.Models
{
    [TableName("NetPayments")]
    [PrimaryKey("Id", AutoIncrement = false)]
    public class PaymentData
    {
        /// <summary>
        /// SQL entry Id
        /// </summary>
        [PrimaryKeyColumn(AutoIncrement = false)]
        [NullSetting(NullSetting = NullSettings.NotNull)]
        public Guid Id { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [NullSetting(NullSetting = NullSettings.Null)]
        [Length(30)]
        public string PaymentDate { get; set; }

        /// <summary>
        /// From remote payment provider
        /// </summary>
        [Length(30)]
        public string AuthorizationNumber { get; set; }

        /// <summary>
        /// Masked credit card number
        /// </summary>
        [Length(16)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CardNumber { get; set; }

        /// <summary>
        /// Mastercard/Visa/etc...
        /// </summary>
        [NullSetting(NullSetting = NullSettings.Null)]
        [Length(30)]
        public string CardType { get; set; }

        /// <summary>
        /// Total
        /// </summary>
        [Length(20)]
        public string Amount { get; set; }

        /// <summary>
        /// SQL entry creation date
        /// </summary>
        public DateTime Date { get; set; }
    }
}
