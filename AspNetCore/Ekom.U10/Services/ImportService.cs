using Ekom.Models.Import;
using Ekom.Services;
using Ekom.Utilities;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.Umb.Services
{
    public class ImportService : IImportService
    {
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IScopeProvider _scopeProvider;

        private IContentType? productContentType;
        private IContentType? categoryContentType;
        private IContentType? catalogContentType;

        public ImportService(
            IUmbracoContextFactory umbracoContextFactory,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IScopeProvider scopeProvider)
        {
            _umbracoContextFactory = umbracoContextFactory;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _scopeProvider = scopeProvider;
        }

        public void FullSync(ImportData data)
        {
            categoryContentType = _contentTypeService.Get("ekmCategory");
            productContentType = _contentTypeService.Get("ekmProduct");
            catalogContentType = _contentTypeService.Get("ekmCatalog");

            ArgumentNullException.ThrowIfNull(categoryContentType);
            ArgumentNullException.ThrowIfNull(productContentType);
            ArgumentNullException.ThrowIfNull(catalogContentType);

            IContent? umbracoRootContent = null;

            if (data.ParentKey.HasValue)
            {
                umbracoRootContent = _contentService.GetById(data.ParentKey.Value);
            } else
            {
                umbracoRootContent = _contentService
                    .GetPagedOfType(catalogContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
                    .Where(x => !x.Trashed)).FirstOrDefault();
            }

            ArgumentNullException.ThrowIfNull(umbracoRootContent);

            using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
            {
                IterateCategoryTree(data.Categories, umbracoRootContent, data.IdentiferPropertyAlias, data.SyncUser);
            }
        }

        private void IterateCategoryTree(List<ImportCategory>? importCategories, IContent parentContent, string identiferPropertyAlias, int syncUser)
        {
            if (importCategories == null || importCategories.Count == 0)
            {
                return;
            }

            var existingUmbracoCategories = _contentService
                .GetPagedChildren(parentContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
                .Where(x => !x.Trashed)).Where(x => x.ContentType.Alias == "ekmCategory")
                .ToList();

            foreach (var importCategory in importCategories)
            {
                var categoryContent = existingUmbracoCategories.FirstOrDefault(x => x.GetValue<string>(identiferPropertyAlias) == importCategory.Identifier);

                var save = false;
                var create = false;
                if (categoryContent == null)
                {
                    // Category does not exist in Umbraco, so create it.
                    categoryContent = new Content(importCategory.NodeName, parentContent.Id, categoryContentType);
                    save = true;
                    create = true;
                }
                else
                {
                    existingUmbracoCategories.Remove(categoryContent);

                    // Category exists, check for changes...

                    if (categoryContent.GetValue<string>("comparer") != importCategory.Comparer)
                    {
                        save = true;
                    }
                }

                if (save)
                {
                    categoryContent.SetProperty("title", importCategory.Title);

                    if (importCategory.Slug != null && importCategory.Slug.Any()) {
                        categoryContent.SetSlug(importCategory.Slug);
                    }

                    if (!string.IsNullOrEmpty(importCategory.SKU))
                    {
                        categoryContent.SetValue("sku", importCategory.SKU);
                    }

                    categoryContent.SetValue("comparer", importCategory.Comparer);

                    categoryContent.Name = importCategory.NodeName;

                    using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
                    {
                        if (categoryContent.Published || create)
                        {
                            _contentService.SaveAndPublish(categoryContent, userId: syncUser);
                        }
                        else
                        {
                            _contentService.Save(categoryContent, userId: syncUser);
                        }
                    }
                }

                IterateCategoryTree(importCategory.SubCategories, categoryContent, identiferPropertyAlias, syncUser);
            }

            foreach (var categoryToDelete in existingUmbracoCategories)
            {
                _contentService.Delete(categoryToDelete);
            }

        }

        public void CategorySync(ImportCategory categoryData)
        {
            throw new NotImplementedException();
        }

        public void ProductSync(ImportProduct productData)
        {
            throw new NotImplementedException();
        }
    }
}
