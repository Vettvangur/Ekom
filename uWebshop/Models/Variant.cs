using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Models
{
    public class Variant
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public decimal OriginalPrice { get; set; }
        public int Stock { get; set; }
        public int ProductId { get; set; }
        public int VariantGroupId { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public Price Price
        {
            get
            {
                return new Price(OriginalPrice);
            }
            set
            { }
        }
    }
}
