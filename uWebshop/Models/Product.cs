using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using uWebshop.Cache;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Path { get; set; }
        public decimal OriginalPrice { get; set; }
        public int Stock { get; set; }
        public List<Category> Categories { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public int Level { get; set; }
        public string Url {
            get {

                var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

                var defaulUrl = Urls.FirstOrDefault();
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                if (findUrlByPrefix != null) {
                    return findUrlByPrefix;
                }

                return defaulUrl;
            }
        }
        public IEnumerable<String> Urls { get; set; }
        public Price Price
        {
            get
            {
                return new Price(OriginalPrice);
            }
        }
        public IEnumerable<VariantGroup> VariantGroups {
            get
            {
                return VariantGroupCache.Instance._cache.Where(x => x.Value.ProductId == Id && x.Value.Store.Alias == Store.Alias).Select(x => x.Value);
            }
        }
        public IEnumerable<Variant> AllVariants {
            get
            {
                return VariantCache.Instance._cache.Where(x => x.Value.ProductId == Id && x.Value.Store.Alias == Store.Alias).Select(x => x.Value);
            }
        }

        public Product(): base() { }
        public Product(SearchResult item, Store store)
        {
            try
            {
                var pathField = item.Fields["path"];

                var examineItemsFromPath = ExamineService.GetAllCatalogItemsFromPath(pathField);

                if (!CatalogService.IsItemDisabled(examineItemsFromPath, store))
                {
                    int categoryId = Convert.ToInt32(item.Fields["parentID"]);

                    var categoryField = item.Fields.Any(x => x.Key == "categories") ? 
                                        item.Fields["categories"] : "";

                    var categories = new List<Category>();

                    var primaryCategory = CategoryCache.Instance._cache.FirstOrDefault
                        (x => x.Value.Id == categoryId && 
                              x.Value.Store.Alias == store.Alias).Value;

                    if (primaryCategory != null)
                    {
                        categories.Add(primaryCategory);
                    }

                    if (!string.IsNullOrEmpty(categoryField))
                    {
                        var categoryIds = categoryField.Split(',');

                        foreach (var catId in categoryIds)
                        {
                            var intCatId = Convert.ToInt32(catId);

                            var categoryItem = CategoryCache.Instance._cache.FirstOrDefault
                                (x => x.Value.Id == intCatId && 
                                      x.Value.Store.Alias == store.Alias).Value;

                            if (categoryItem != null && !categories.Contains(categoryItem))
                            {
                                categories.Add(categoryItem);
                            }
                        }
                    }

                    var priceField = ExamineService.GetProperty(item, "price", store.Alias);

                    decimal originalPrice = 0;
                    decimal.TryParse(priceField, out originalPrice);

                    Id            = item.Id;
                    Path          = pathField;
                    OriginalPrice = originalPrice;
                    Categories    = categories;
                    Store         = store;

                    Title         = ExamineService.GetProperty(item, "title", store.Alias);
                    Slug          = ExamineService.GetProperty(item, "slug", store.Alias);

                    SortOrder     = Convert.ToInt32(item.Fields["sortOrder"]);
                    Level         = Convert.ToInt32(item.Fields["level"]);

                    Urls          = UrlService.BuildProductUrls(Slug, Categories, store);

                    if (Urls.Any() && !string.IsNullOrEmpty(Title))
                    {
                        return;
                    }
                    else
                    {
                        throw new Exception("No url's or no title present");
                    }
                }
                else
                {
                    throw new Exception("Error product disabled. Node id: " + item.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error creating product item from Examine. Node id: " + item.Id, ex);
                throw;
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
