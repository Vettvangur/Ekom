using Ekom.Utilities;
using LinqToDB.Mapping;
using System.Globalization;

namespace Ekom.Models
{
    /// <summary>
    /// SQL Representation of <see cref="OrderInfo"/>
    /// </summary>
    [Table(Name = "EkomOrders")]
    public class OrderData : ICloneable
    {
        internal const int CustomerFieldMaxLength = 200;

        /// <summary>
        /// Primary means of identifying orders
        /// 
        /// Installation and upgrades ensure a unique index on this column.
        /// </summary>
        [Column, NotNull]
        public Guid UniqueId { get; set; }

        /// <summary>
        /// Required by some payment providers and bookkeeping software
        /// </summary>

        [PrimaryKey]
        [Identity, NotNull]
        public int ReferenceId { get; set; }

        /// <summary>
        /// <see cref="Models.OrderInfo"/> Json
        /// </summary>
        [Column(Length = int.MaxValue)]
        public string OrderInfo { get; set; }

        [Column(Length = 100)]
        public string OrderNumber { get; set; }

        /// <summary>
        /// The database representation of the enum.
        /// This is necessary for the creation of the column.
        /// </summary>
        [Column, NotNull]
        public string OrderStatusCol { get; set; }

        /// <summary>Independent, read-only statuses projected from the legacy record.</summary>
        [Column(IsColumn = false)]
        public Orders.OrderStatusSnapshot Status => Orders.OrderStateRules.Read(this);

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string? OrderStateCol { get; set; }

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string? PaymentStateCol { get; set; }

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string? FulfillmentStateCol { get; set; }

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string? OriginalOrderStatusCol { get; set; }

        [Column(Length = int.MaxValue), System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string? FulfilledQuantitiesJson { get; set; }

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public int StatusMappingVersion { get; set; }

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public int StatusRevision { get; set; }

        [Column, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public bool AutoCloseSuppressed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column(IsColumn = false)]
        public OrderStatus OrderStatus
        {
            get { return (OrderStatus)Enum.Parse(typeof(OrderStatus), OrderStatusCol); }
            set { OrderStatusCol = value.ToString(); }
        }

        /// <summary>
        /// 
        /// </summary>
        [Column(IsColumn = false)]
        public string FormattedTotal
        {
            get
            {
                CultureInfo? cultureInfo = CultureHelper.GetCultureInfo(Currency);

                if (cultureInfo != null)
                {
                    string formattedCurrency = string.Format(cultureInfo, "{0:C}", TotalAmount);

                    return formattedCurrency;
                }
                else
                {
                    return String.Format("{0:C}", TotalAmount);
                }

            }
        }

        /// <summary>
        /// 
        /// </summary>
        [Column(Length = CustomerFieldMaxLength)]
        public string CustomerEmail { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column(Length = CustomerFieldMaxLength)]
        public string? CustomerName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column]
        public int CustomerId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column(Length = 200)]
        public string CustomerUsername { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column(Length = 50)]
        public string ShippingCountry { get; set; }

        /// <summary>
        /// 
        /// </summary>
        // [Length(9)] // decimal (9,9) https://stackoverflow.com/questions/19811180/best-data-annotation-for-a-decimal18-2
        [Column]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Contains the culture (e: "is-IS" or "is")
        /// </summary>
        [Column(Length = 5)]
        public string Currency { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column(Length = 50)]
        public string StoreAlias { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Last update date
        /// </summary>
        [Column]
        public DateTime UpdateDate { get; set; } = DateTime.Now;

        /// <summary>
        /// <see cref="DateTime"/> payment was verified.
        /// </summary>
        [Column]
        public DateTime? PaidDate { get; set; }

        /// <summary>
        /// Creates a shallow copy of the current Object.
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();
    }
}
