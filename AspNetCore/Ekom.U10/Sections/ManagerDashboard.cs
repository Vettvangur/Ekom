using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Dashboards;

namespace Ekom.Umb.Sections
{
    public class ManagerDashboard : IDashboard
    {
        public string[] Sections => new[] { "ekommanager" };

        public IAccessRule[] AccessRules
        {
            get
            {
                var rules = new IAccessRule[]
                {
                    new AccessRule {Type = AccessRuleType.Grant, Value = Constants.Security.EditorGroupAlias},
                    new AccessRule {Type = AccessRuleType.Grant, Value = Constants.Security.AdminGroupAlias}
                };
                return rules;
            }
        }

        public string Alias => "ekommanager";

        public string View => "/app_plugins/ekommanager/views/ekmManager.html";
    }
}
