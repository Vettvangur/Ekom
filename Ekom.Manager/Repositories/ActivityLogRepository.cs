using System;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Umbraco.Web;
using Umbraco.Core;

namespace Ekom.Repository
{
    class ActivityLogRepository
    {
        readonly ApplicationContext _appCtx;
        readonly UmbracoContext _umbracoCtx;
        public ActivityLogRepository(ApplicationContext appCtx, UmbracoContext umbracoCtx)
        {
            _appCtx = appCtx;
            _umbracoCtx = umbracoCtx;
        }
        public void CreateActivityLog(Guid key, string log)
        {
            var user = _umbracoCtx.Security.CurrentUser;

            var activityLog = new OrderActivityLog
            {
                CreateDate = DateTime.Now,
                Log = log,
                Key = key,
                UniqueId = Guid.NewGuid(),
                UpdateDate = DateTime.Now,
                UserName = user.Name,
            };

            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Insert(activityLog);
            }
        }
    }
}
