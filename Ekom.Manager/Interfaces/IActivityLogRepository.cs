using Ekom.Helpers;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IActivityLogRepository
    {
        OrderActivityLog CreateActivityLog(Guid key, string log);

    }
}
