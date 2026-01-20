//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Umbraco.Cms.Core.Events;
//using Umbraco.Cms.Core.Services;
//using Umbraco.Cms.Core;
//using Umbraco.Cms.Core.Trees;
//using Umbraco.Cms.Web.BackOffice.Trees;
//using Umbraco.Cms.Web.Common.Attributes;

//namespace Ekom.Umb.Sections
//{
//    [Tree("ekommanager", "ekommanager", IsSingleNodeTree = true, TreeTitle = "Ekom Manager", TreeGroup = "ekomGroup", SortOrder = 5)]
//    [PluginController("ekommanager")]
//    public class ManagerTreeController : TreeController
//    {
//        private readonly IMenuItemCollectionFactory _menuItemCollectionFactory;
        
//        public ManagerTreeController(ILocalizedTextService localizedTextService,
//            UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
//            IMenuItemCollectionFactory menuItemCollectionFactory,
//            IEventAggregator eventAggregator)
//            : base(localizedTextService, umbracoApiControllerTypeCollection, eventAggregator)
//        {
//            _menuItemCollectionFactory = menuItemCollectionFactory ?? throw new ArgumentNullException(nameof(menuItemCollectionFactory));
//        }
        
//        protected override ActionResult<TreeNodeCollection> GetTreeNodes(string id, FormCollection queryStrings)
//        {
//            return new TreeNodeCollection();
//        }
//        protected override ActionResult<MenuItemCollection> GetMenuForNode(string id, FormCollection queryStrings)
//        {
//            var menu = _menuItemCollectionFactory.Create();
            
//            return menu;
//        }
//    }
//}
