using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ekom.Models.Data;
using System.Web;
using Umbraco.Web;
using Umbraco.Core;

namespace Ekom.Services
{
    class ActivityLog
    {
        readonly ApplicationContext _appCtx;
        readonly UmbracoContext _umbracoCtx;
        public ActivityLog(ApplicationContext appCtx, UmbracoContext umbracoCtx)
        {
            _appCtx = appCtx;
            _umbracoCtx = umbracoCtx;
        }
        public void createActivityLog(OrderActivityLog ActivityLog)
        {
            var user = _umbracoCtx.Security.CurrentUser;
            ActivityLog.UserName = user.Name;
            ActivityLog.CreateDate = DateTime.Now;

            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Insert(ActivityLog);
            }
        }
    }
}
