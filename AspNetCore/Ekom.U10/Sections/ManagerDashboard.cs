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
                var accessRulesAliases = Configuration.Instance.SectionAccessRules;


                var rules = new IAccessRule[]
                {
                    new AccessRule {Type = AccessRuleType.Grant, Value = Constants.Security.AdminGroupAlias}
                };

                foreach (var accessRule in accessRulesAliases)
                {
                    rules.Append(new AccessRule
                        { Type = AccessRuleType.Grant, Value = accessRule });
                }


                return rules;
            }
        }

        public string Alias => "ekommanager";

        public string View => "/app_plugins/ekom/manager/views/ekmManager.html";
    }
}
