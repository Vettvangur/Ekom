using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ekom.Models.Data;

namespace Ekom.Manager.Models
{
    public class ActivityLogData
    {

        public ActivityLogData(IEnumerable<OrderActivityLog> orderActivityLogs)
        {
            OrderActivityLogs = orderActivityLogs;
        }

        public IEnumerable<OrderActivityLog> OrderActivityLogs { get; set; }
    }
}
