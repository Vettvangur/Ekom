using System;

namespace Ekom.Models
{
    public class OrderLineSettings
    {
        public Guid Link { get; set; }
        public bool CountToTotal { get; set; } = true;
    }
}
