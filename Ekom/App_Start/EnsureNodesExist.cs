using Ekom.Exceptions;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Vorto.Models;
using Our.Umbraco.Vorto.PropertyEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.PropertyEditors;

namespace Ekom.App_Start
{
    class EnsureNodesExist : IComponent
    {
        private readonly ILogger _logger;
        private readonly Configuration _configuration;
        private readonly IContentService _contentService;
        private readonly IFileService _fileService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataTypeService _dataTypeService;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IUmbracoContextFactory _contextFactory;

        public EnsureNodesExist(
            ILogger logger,
            IFileService fileService,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            PropertyEditorCollection propertyEditorCollection,
            Configuration configuration,
            IUmbracoContextFactory contextFactory)
        {
            _logger = logger;
            _fileService = fileService;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _propertyEditorCollection = propertyEditorCollection;
            _configuration = configuration;
            _contextFactory = contextFactory;
        }

        public void Initialize()
        {
            _logger.Debug<EnsureNodesExist>("Ensuring Umbraco nodes exist");

            try
            {
                // Test for existence of Ekom root node
                if (!_contentService.GetRootContent().Any(x => x.ContentType.Alias == "ekom" && !x.Trashed))
                {
                    #region Property Editors

                    if (!_propertyEditorCollection.TryGet("Ekom.Stock", out IDataEditor stockEditor))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Stock property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Cache", out IDataEditor cacheEditor))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Cache property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Coupon", out IDataEditor couponEditor))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Coupon property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Range", out IDataEditor rangeEditor))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Range property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Price", out IDataEditor priceEditor))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Price property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Country", out IDataEditor countryPicker))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Country property picker, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Zone", out IDataEditor zonePicker))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Zone property picker, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Currency", out IDataEditor currencyPicker))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Ekom Currency property picker, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Umbraco.MultiNodeTreePicker", out IDataEditor multiNodeEditor))
                    {
                        // Should never happen
                        throw new EnsureNodesException(
                            "Unable to find Umbraco.MultiNodeTreePicker property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Our.Umbraco.Vorto", out IDataEditor editor))
                    {
                        throw new EnsureNodesException(
                            "Unable to find Our.Umbraco.Vorto property editor, failed creating Ekom nodes. Ensure GMO.Vorto.Web is installed.");
                    }
                    if (!_propertyEditorCollection.TryGet("Umbraco.DropDown.Flexible", out IDataEditor dropdownEditor))
                    {
                        throw new EnsureNodesException(
                            "Unable to find Umbraco.DropDown.Flexible property editor, failed creating Ekom nodes.");
                    }

                    #endregion

                    #region Templates

                    var allTemplates = _fileService.GetTemplates();

                    var productTemplate = allTemplates.FirstOrDefault(x => x.Alias.ToLowerInvariant() == "product" || x.Alias.ToLowerInvariant() == "ekmproduct");
                    var categoryTemplate = allTemplates.FirstOrDefault(x => x.Alias.ToLowerInvariant() == "category" || x.Alias.ToLowerInvariant() == "ekmcategory");

                    var productTemplates = new List<ITemplate>();
                    var categoryTemplates = new List<ITemplate>();

                    if (productTemplate != null)
                    {
                        productTemplates.Add(productTemplate);
                    }

                    if (categoryTemplate != null)
                    {
                        categoryTemplates.Add(categoryTemplate);
                    }

                    #endregion

                    #region Data Types

                    var ekmDtContainer = EnsureDataTypeContainerExists();

                    var booleanDt = _dataTypeService.GetDataType(new Guid("92897bc6-a5f3-4ffe-ae27-f2e7e33dda49"));
                    var textstringDt = _dataTypeService.GetDataType(new Guid("0cc0eba1-9960-42c9-bf9b-60e150b429ae"));
                    var numericDt = _dataTypeService.GetDataType(new Guid("2e6d3631-066e-44b8-aec4-96f09099b2b5"));
                    var contentPickerDt = _dataTypeService.GetDataType(new Guid("fd1e0da5-5606-4862-b679-5d0cf3a52a59"));
                    var mediaPickerDt = _dataTypeService.GetDataType(new Guid("135d60e0-64d9-49ed-ab08-893c9ba44ae5"));
                    var multipleMediaPickerDt = _dataTypeService.GetDataType(new Guid("9dbbcbbb-2327-434a-b355-af1b84e5010a"));
                    var tagsDt = _dataTypeService.GetDataType(new Guid("b6b73142-b9c1-4bf8-a16d-e1c23320b549"));
                    var rteDt = _dataTypeService.GetDataType(new Guid("ca90c950-0aff-4e72-b976-a30b1ac57dad"));
                    var textareaDt = _dataTypeService.GetDataType(new Guid("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3"));

                    var perStoreTextDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Textstring - Per Store",
                        Configuration = new VortoConfiguration
                        {
                            DataType = new DataTypeInfo
                            {
                                Guid = textstringDt.Key,
                                Name = textstringDt.Name,
                                PropertyEditorAlias = textstringDt.EditorAlias,
                            },
                            MandatoryBehaviour = "primary",
                            LanguageSource = "custom",
                        },
                    });
                    var perStoreBoolDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Boolean - Per Store",
                        Configuration = new VortoConfiguration
                        {
                            DataType = new DataTypeInfo
                            {
                                Guid = booleanDt.Key,
                                Name = booleanDt.Name,
                                PropertyEditorAlias = booleanDt.EditorAlias,
                            },
                            MandatoryBehaviour = "primary",
                            LanguageSource = "custom",
                        },
                    });
                    var perStoreIntDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Numeric - Per Store",
                        Configuration = new VortoConfiguration
                        {
                            DataType = new DataTypeInfo
                            {
                                Guid = numericDt.Key,
                                Name = numericDt.Name,
                                PropertyEditorAlias = numericDt.EditorAlias,
                            },
                            MandatoryBehaviour = "primary",
                            LanguageSource = "custom",
                        },
                    });
                    var perStoreMediaPickerDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Media Picker - Per Store",
                        Configuration = new VortoConfiguration
                        {
                            DataType = new DataTypeInfo
                            {
                                Guid = mediaPickerDt.Key,
                                Name = mediaPickerDt.Name,
                                PropertyEditorAlias = mediaPickerDt.EditorAlias,
                            },
                            MandatoryBehaviour = "primary",
                            LanguageSource = "custom",
                        },
                    });
                    var perStoreRteDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Richtext Editor - Per Store",
                        Configuration = new VortoConfiguration
                        {
                            DataType = new DataTypeInfo
                            {
                                Guid = rteDt.Key,
                                Name = rteDt.Name,
                                PropertyEditorAlias = rteDt.EditorAlias,
                            },
                            MandatoryBehaviour = "primary",
                            LanguageSource = "custom",
                        },
                    });
                    var perStoreTextareaDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Textarea - Per Store",
                        Configuration = new VortoConfiguration
                        {
                            DataType = new DataTypeInfo
                            {
                                Guid = textareaDt.Key,
                                Name = textareaDt.Name,
                                PropertyEditorAlias = textareaDt.EditorAlias,
                            },
                            MandatoryBehaviour = "primary",
                            LanguageSource = "custom",
                        },
                    });

                    var stockDt = EnsureDataTypeExists(new DataType(stockEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Stock",
                    });
                    var cacheDt = EnsureDataTypeExists(new DataType(cacheEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Cache",
                    });
                    var couponDt = EnsureDataTypeExists(new DataType(couponEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Coupons",
                    });
                    var currencyDt = EnsureDataTypeExists(new DataType(currencyPicker, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Currency",
                    });
                    var zoneDt = EnsureDataTypeExists(new DataType(zonePicker, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Zone",
                    });
                    var countryDt = EnsureDataTypeExists(new DataType(countryPicker, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Country",
                    });
                    var priceDt = EnsureDataTypeExists(new DataType(priceEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Price",
                    });
                    var rangeDt = EnsureDataTypeExists(new DataType(rangeEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Range",
                    });

                    var multinodeCatalogDt = EnsureDataTypeExists(new DataType(multiNodeEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Catalog Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmProduct, ekmProductVariant, ekmCategory",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {
                                StartNodeQuery = "$root/ekom/ekmCatalog"
                            }
                        }
                    });
                    var multinodeProductDt = EnsureDataTypeExists(new DataType(multiNodeEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Product Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmProduct",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {
                                StartNodeQuery = "$root/ekom/ekmCatalog"
                            }
                        }
                    });
                    var multinodeCategoryDt = EnsureDataTypeExists(new DataType(multiNodeEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Category Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmCategory",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {
                                StartNodeQuery = "$root/ekom/ekmCatalog"
                            }
                        }
                    });
                    var discountTypeDt = EnsureDataTypeExists(new DataType(dropdownEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Discount Type",
                        Configuration = new DropDownFlexibleConfiguration
                        {
                            Items = new List<ValueListConfiguration.ValueListItem>
                        {
                            new ValueListConfiguration.ValueListItem
                            {
                                Id = 1,
                                Value = "Fixed",
                            },
                            new ValueListConfiguration.ValueListItem
                            {
                                Id = 2,
                                Value = "Percentage",
                            },
                        }
                        },
                    });
                    var variantGroupDt = EnsureDataTypeExists(new DataType(multiNodeEditor, ekmDtContainer.Id)
                    {
                        Name = "Ekom - Variant Group Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmProductVariantGroup",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {
                                StartNodeQuery = "$current",
                            },
                            MaxNumber = 1
                        }
                    });
                    #endregion

                    var ekmDocTypeContainer = EnsureContainerExists("Ekom");
                    var catalogContainer = EnsureContainerExists("Catalog", 2, ekmDocTypeContainer.Id);
                    var compositionsContainer = EnsureContainerExists("Compositions", 2, ekmDocTypeContainer.Id);
                    var discountsContainer = EnsureContainerExists("Discounts", 2, ekmDocTypeContainer.Id);
                    var ppContainer = EnsureContainerExists("Payment Providers", 2, ekmDocTypeContainer.Id);
                    var spContainer = EnsureContainerExists("Shipping Providers", 2, ekmDocTypeContainer.Id);
                    var storeContainer = EnsureContainerExists("Store", 2, ekmDocTypeContainer.Id);
                    var zoneContainer = EnsureContainerExists("Zones", 2, ekmDocTypeContainer.Id);

                    #region Compositions

                    var baseComposition = EnsureContentTypeExists(
                        new ContentType(compositionsContainer.Id)
                        {
                            Name = "Base Composition",
                            Alias = "ekmBaseComposition",
                            SortOrder = 10,
                            PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreTextDt, "title")
                                        {
                                            Name = "Title",
                                        },
                                        new PropertyType(perStoreRteDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                    }))
                                {
                                    Name = "Settings",
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreBoolDt, "disable")
                                        {
                                            Name = "Disable",
                                        },
                                    }))
                                {
                                    Name = "Stores",
                                },
                                }),
                        }
                    );

                    var providerComposition = EnsureContentTypeExists(
                      new ContentType(compositionsContainer.Id)
                      {
                          Name = "Provider Composition",
                          Alias = "ekmProviderComposition",

                          PropertyGroups = new PropertyGroupCollection(
                              new List<PropertyGroup>
                              {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(contentPickerDt, "zone")
                                        {
                                            Name = "Zone",
                                        },
                                        new PropertyType(priceDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                    }))
                                {
                                    Name = "Settings",
                                },
                              }),
                      }
                    );

                    var rangeComposition = EnsureContentTypeExists(
                        new ContentType(compositionsContainer.Id)
                        {
                            Name = "Range Composition",
                            Alias = "ekmRange",
                            SortOrder = 20,

                            PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(rangeDt, "startOfRange")
                                        {
                                            Name = "Start of Range",
                                            SortOrder = 20
                                        },
                                        new PropertyType(rangeDt, "endOfRange")
                                        {
                                            Name = "End of Range",
                                            SortOrder = 21
                                        },
                                    }))
                                {
                                    Name = "Settings",
                                },
                                }),
                        }
                    );

                    #endregion

                    #region Catalog Document Types

                    var productVariantCt = EnsureContentTypeExists(
                        new ContentType(catalogContainer.Id)
                        {
                            Name = "Product Variant",
                            Alias = "ekmProductVariant",
                            Icon = "icon-layers-alt",
                            PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreTextDt, "title")
                                        {
                                            Name = "Title",
                                        },
                                        new PropertyType(textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                        },
                                        new PropertyType(multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                        new PropertyType(priceDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                        new PropertyType(stockDt, "stock")
                                        {
                                            Name = "Stock",
                                        },
                                        new PropertyType(booleanDt, "stock")
                                        {
                                            Name = "Stock",
                                        },
                                        new PropertyType(booleanDt, "enableBackorder")
                                        {
                                            Name = "Enable Backorder",
                                            Description = "If set then the variant can be sold indefinitely"
                                        },
                                        new PropertyType(numericDt, "vat")
                                        {
                                            Name = "VAT",
                                            Description = "%, override store VAT."
                                        },
                                    }))
                                {
                                    Name = "Variant",
                                }
                                }),
                        }
                    );

                    var productVariantGroupCt = EnsureContentTypeExists(
                        new ContentType(catalogContainer.Id)
                        {
                            Name = "Product Variant Group",
                            Alias = "ekmProductVariantGroup",
                            Icon = "icon-folder",
                            AllowedContentTypes = new List<ContentTypeSort>
                            {
                            new ContentTypeSort(productVariantCt.Id, 1),
                            },
                            PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreTextDt, "title")
                                        {
                                            Name = "Title",
                                        },
                                        new PropertyType(multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                        new PropertyType(textstringDt, "color")
                                        {
                                            Name = "Color",
                                        },
                                    }))
                                {
                                    Name = "Variant Group",
                                },
                                }),
                        }
                    );

                    var productCt = EnsureContentTypeExists(
                        new ContentType(catalogContainer.Id)
                        {
                            Name = "Product",
                            Alias = "ekmProduct",
                            Icon = "icon-loupe",
                            AllowedTemplates = productTemplates,
                            AllowedContentTypes = new List<ContentTypeSort>
                            {
                            new ContentTypeSort(productVariantCt.Id, 1),
                            new ContentTypeSort(productVariantGroupCt.Id, 2),
                            },
                            PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreTextDt, "title")
                                        {
                                            Name = "Title",
                                        },
                                        new PropertyType(perStoreTextDt, "slug")
                                        {
                                            Name = "Slug",
                                        },
                                        new PropertyType(textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                        },
                                        new PropertyType(perStoreTextDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                        new PropertyType(multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                        new PropertyType(priceDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                        new PropertyType(stockDt, "stock")
                                        {
                                            Name = "Stock",
                                        },
                                        new PropertyType(booleanDt, "enableBackorder")
                                        {
                                            Name = "Enable Backorder",
                                            Description = "If set then the product can be sold indefinitely"
                                        },
                                        new PropertyType(numericDt, "vat")
                                        {
                                            Name = "VAT",
                                            Description = "%, override store VAT."
                                        },
                                        new PropertyType(multinodeProductDt, "categories")
                                        {
                                            Name = "Product Categories",
                                            Description = "Allows a product to belong to categories other than it's umbraco node parent categories. A single product node can therefore belong to multiple logical category tree hierarchies.",
                                        },
                                        new PropertyType(multinodeProductDt, "relatedProducts")
                                        {
                                            Name = "Related products",
                                        },
                                        new PropertyType(variantGroupDt, "primaryVariantGroup")
                                        {
                                            Name = "Primary Variant Group",
                                        },
                                    }))
                                {
                                    Name = "Product"
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreBoolDt, "disable")
                                        {
                                            Name = "Disable",
                                        },
                                    }))
                                {
                                    Name = "Stores",
                                },
                                }),
                        }
                    );

                    var categoryCt = EnsureContentTypeExists(new ContentType(catalogContainer.Id)
                    {
                        Name = "Category",
                        Alias = "ekmCategory",
                        AllowedContentTypes = new List<ContentTypeSort> {
                        new ContentTypeSort(productCt.Id, 1)
                    },
                        Icon = "icon-folder",
                        AllowedTemplates = categoryTemplates,
                        PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreTextDt, "title")
                                        {
                                            Name = "Title",
                                        },
                                        new PropertyType(perStoreTextDt, "slug")
                                        {
                                            Name = "Slug",
                                        },
                                        new PropertyType(perStoreTextDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                        new PropertyType(mediaPickerDt, "categoryImage")
                                        {
                                            Name = "Category image",
                                        }
                                    }))
                                    {
                                        Name = "Category",
                                    },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreBoolDt, "disable")
                                        {
                                            Name = "Disable",
                                        },
                                    }))
                                {
                                    Name = "Stores",
                                },
                                }),
                    });

                    if (!categoryCt.AllowedContentTypes.Any(x => x.Alias == categoryCt.Alias))
                    {
                        categoryCt.AllowedContentTypes
                            = categoryCt.AllowedContentTypes.Append(
                                new ContentTypeSort(categoryCt.Id, 1)
                            );

                        _contentTypeService.Save(categoryCt);
                    }

                    var catalogCt = EnsureContentTypeExists(new ContentType(catalogContainer.Id)
                    {
                        Name = "Catalog",
                        Alias = "ekmCatalog",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(categoryCt.Id, 1),
                    },
                        Icon = "icon-books",
                    });
                    #endregion

                    #region Discounts

                    var orderDiscountCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                    {
                        Name = "Order Discount",
                        Alias = "ekmOrderDiscount",
                        Icon = "icon-coin-dollar",
                        ContentTypeComposition = new List<IContentTypeComposition>
                        {
                            baseComposition,
                            rangeComposition,
                        },
                        PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                    {
                                    new PropertyGroup(new PropertyTypeCollection(
                                        true,
                                        new List<PropertyType>
                                        {
                                            new PropertyType(discountTypeDt, "type")
                                            {
                                                Name = "Type",
                                                Mandatory = true,
                                                SortOrder = 5,
                                            },
                                            new PropertyType(rangeDt, "discount")
                                            {
                                                Name = "Discount",
                                                Mandatory = true,
                                                SortOrder = 6,
                                            },
                                            new PropertyType(multinodeCatalogDt, "discountItems")
                                            {
                                                Name = "Discount Items",
                                                SortOrder = 7,
                                                Description = "Controls what items in the order receive the discount. (In contrast to product discount, discount items, where it is used as a constraint)"
                                            },
                                            new PropertyType(booleanDt, "stackable")
                                            {
                                                Name = "Stackable",
                                                SortOrder = 8,
                                            },
                                            new PropertyType(booleanDt, "globalDiscount")
                                            {
                                                Name = "Global Discount",
                                                SortOrder = 9,
                                                Description = "This couponless discount will be automatically applied to orders that match it's constraints"
                                            },
                                        }))
                                    {
                                        Name = "Settings",
                                    },
                                    new PropertyGroup(new PropertyTypeCollection(
                                        true,
                                        new List<PropertyType>
                                        {
                                            new PropertyType(couponDt, "coupons")
                                            {
                                                Name = "Coupons",
                                            },
                                        }))
                                    {
                                        Name = "Coupons",
                                    }
                                    }
                            ),
                    });

                    var orderDiscountsCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                    {
                        Name = "Order Discounts",
                        Alias = "ekmOrderDiscounts",
                        Icon = "icon-bulleted-list",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(orderDiscountCt.Id, 1),
                    },
                    });

                    var productDiscountCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                    {
                        Name = "Product Discount",
                        Alias = "ekmProductDiscount",
                        Icon = "icon-coin-dollar",
                        ContentTypeComposition = new List<IContentTypeComposition>
                        {
                            baseComposition,
                            rangeComposition,
                        },
                        PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                    {
                                    new PropertyGroup(new PropertyTypeCollection(
                                        true,
                                        new List<PropertyType>
                                        {
                                            new PropertyType(discountTypeDt, "type")
                                            {
                                                Name = "Type",
                                                Mandatory = true,
                                                SortOrder = 5,
                                            },
                                            new PropertyType(rangeDt, "discount")
                                            {
                                                Name = "Discount",
                                                Mandatory = true,
                                                SortOrder = 6,
                                            },
                                            new PropertyType(multinodeCatalogDt, "discountItems")
                                            {
                                                Name = "Discount Items",
                                                SortOrder = 7,
                                                Description = "Discount is automatically applied to selected items if the other constraints are valid.",
                                            },
                                        }))
                                    {
                                        Name = "Settings",
                                    }
                                    }
                            ),
                    });

                    var productDiscountsCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                    {
                        Name = "Product Discounts",
                        Alias = "ekmProductDiscounts",
                        Icon = "icon-bulleted-list",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(productDiscountCt.Id, 1),
                    },
                    });

                    var discountsCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                    {
                        Name = "Discounts",
                        Alias = "ekmDiscounts",
                        Icon = "icon-bills-euro",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(orderDiscountsCt.Id, 1),
                        new ContentTypeSort(productDiscountsCt.Id, 2),
                    },
                    });

                    #endregion

                    #region Payment Providers

                    var netPaymentProviderCt = EnsureContentTypeExists(new ContentType(ppContainer.Id)
                    {
                        Name = "NetPayment Provider",
                        Alias = "netPaymentProvider",
                        Icon = "icon-bill",
                        ContentTypeComposition = new List<IContentTypeComposition>
                    {
                        baseComposition,
                    },
                        PropertyGroups = new PropertyGroupCollection(
                            new List<PropertyGroup>
                            {
                            new PropertyGroup(new PropertyTypeCollection(
                                true,
                                new List<PropertyType>
                                {
                                    new PropertyType(textstringDt, "basePaymentProvider")
                                    {
                                        Name = "Base Payment Provider DLL",
                                        Description = "Allows payment provider overloading. " +
                                            "F.x. Borgun ISK and Borgun USD nodes with different names and different xml configurations targetting the same base payment provider."
                                    },
                                    //new PropertyType(numericDt, "discount")
                                    //{
                                    //    Name = "Discount",
                                    //},
                                    new PropertyType(perStoreTextDt, "successUrl")
                                    {
                                        Name = "Success Url",
                                        Mandatory = true,
                                    },
                                    new PropertyType(perStoreTextDt, "errorUrl")
                                    {
                                        Name = "Error Url",
                                    },
                                    new PropertyType(perStoreTextDt, "cancelUrl")
                                    {
                                        Name = "Cancel Url",
                                    },
                                    new PropertyType(mediaPickerDt, "logo")
                                    {
                                        Name = "Logo",
                                    },
                                    new PropertyType(textstringDt, "currency")
                                    {
                                        Name = "Currency"
                                    },
                                    new PropertyType(booleanDt, "offlinePayment")
                                    {
                                        Name = "Offline Payment"
                                    },
                                }))
                            {
                                Name = "Settings",
                            },
                            }
                        ),
                    });

                    var netPaymentProvidersCt = EnsureContentTypeExists(new ContentType(ppContainer.Id)
                    {
                        Name = "NetPayment Providers",
                        Alias = "netPaymentProviders",
                        Icon = "icon-bills",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(netPaymentProviderCt.Id, 1),
                    },
                    });

                    #endregion

                    #region Shipping Providers

                    var shippingProviderCt = EnsureContentTypeExists(new ContentType(spContainer.Id)
                    {
                        Name = "Shipping Provider",
                        Alias = "ekmShippingProvider",
                        Icon = "icon-truck",
                        ContentTypeComposition = new List<IContentTypeComposition>
                    {
                        baseComposition,
                        providerComposition,
                        rangeComposition,
                    },
                    });

                    var shippingProvidersCt = EnsureContentTypeExists(new ContentType(spContainer.Id)
                    {
                        Name = "Shipping Providers",
                        Alias = "ekmShippingProviders",
                        Icon = "icon-boat-shipping",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(shippingProviderCt.Id, 1),
                    },
                    });

                    #endregion

                    #region Stores

                    var storeCt = EnsureContentTypeExists(new ContentType(storeContainer.Id)
                    {
                        Name = "Store",
                        Alias = "ekmStore",
                        Icon = "icon-store",
                        PropertyGroups = new PropertyGroupCollection(
                            new List<PropertyGroup>
                            {
                            new PropertyGroup(new PropertyTypeCollection(
                                true,
                                new List<PropertyType>
                                {
                                    new PropertyType(contentPickerDt, "storeRootNode")
                                    {
                                        Name = "Store Root Node",
                                        Mandatory = true,
                                    },
                                    new PropertyType(numericDt, "vat")
                                    {
                                        Name = "Vat",
                                        Mandatory = true,
                                    },
                                    new PropertyType(textstringDt, "culture")
                                    {
                                        Name = "Culture",
                                        Mandatory = true,
                                    },
                                    new PropertyType(currencyDt, "currency")
                                    {
                                        Name = "Currency",
                                    },
                                    new PropertyType(booleanDt, "vatIncludedInPrice")
                                    {
                                        Name = "Vat included in price",
                                    },
                                    new PropertyType(textstringDt, "orderNumberTemplate")
                                    {
                                        Name = "Order Number Template",
                                        Description ="Define how the ordernumber will be created. You can use #orderId# #orderIdPadded#, #storeAlias#, #day#, #month# and #year#, plus any characters you need"
                                    },
                                    new PropertyType(textstringDt, "orderNumberPrefix")
                                    {
                                        Name = "Order Number Prefix",
                                    },
                                })
                            )
                            {
                                Name = "Store"
                            }
                            }
                        ),
                    });

                    var storesCt = EnsureContentTypeExists(new ContentType(storeContainer.Id)
                    {
                        Name = "Stores",
                        Alias = "ekmStores",
                        Icon = "icon-store",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(storeCt.Id, 1),
                    },
                    });

                    #endregion

                    #region Zones

                    var zoneCt = EnsureContentTypeExists(new ContentType(zoneContainer.Id)
                    {
                        Name = "Zone",
                        Alias = "ekmZone",
                        Icon = "icon-globe",
                        PropertyGroups = new PropertyGroupCollection(
                            new List<PropertyGroup>
                            {
                            new PropertyGroup(new PropertyTypeCollection(
                                true,
                                new List<PropertyType>
                                {
                                    new PropertyType(tagsDt, "zoneSelector")
                                    {
                                        Name = "Zone Selector",
                                    },
                                })
                            ){
                                Name = "Zones",
                            }
                            }
                        )
                    });

                    var zonesCt = EnsureContentTypeExists(new ContentType(zoneContainer.Id)
                    {
                        Name = "Zones",
                        Alias = "ekmZones",
                        Icon = "icon-globe-alt",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(zoneCt.Id, 1),
                    },
                    });

                    #endregion

                    var ekmCt = EnsureContentTypeExists(new ContentType(ekmDocTypeContainer.Id)
                    {
                        Name = "Ekom",
                        Alias = "ekom",
                        AllowedAsRoot = true,
                        PropertyGroups = new PropertyGroupCollection(
                                new List<PropertyGroup>
                                {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(cacheDt, "Cache")
                                        {
                                            Name = "Populate Cache",
                                        }
                                    }))
                                {
                                    Name = "Ekom",
                                }
                                }),
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(storesCt.Id, 1),
                        new ContentTypeSort(catalogCt.Id, 2),
                        new ContentTypeSort(shippingProvidersCt.Id, 3),
                        new ContentTypeSort(netPaymentProvidersCt.Id, 4),
                        new ContentTypeSort(zonesCt.Id, 5),
                        new ContentTypeSort(discountsCt.Id, 6),
                    },
                        Icon = "icon-box color-green",
                    });

                    #region Content Nodes

                    var ekom = EnsureContentExists("Ekom", "ekom");
                    var catalog = EnsureContentExists("Catalog", "ekmCatalog", ekom.Id);
                    EnsureContentExists("Shipping Providers", "ekmShippingProviders", ekom.Id);
                    EnsureContentExists("Payment Providers", "netPaymentProviders", ekom.Id);
                    var discounts = EnsureContentExists("Discounts", "ekmDiscounts", ekom.Id);
                    EnsureContentExists("Product Discounts", "ekmProductDiscounts", discounts.Id);
                    EnsureContentExists("Order Discounts", "ekmOrderDiscounts", discounts.Id);
                    EnsureContentExists("Stores", "ekmStores", ekom.Id);
                    EnsureContentExists("Zones", "ekmZones", ekom.Id);

                    #endregion
                }

                _logger.Debug<EnsureNodesExist>("Done");
            }
#pragma warning disable CA1031 // Should not kill startup
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.Error<EnsureNodesExist>(ex, "Failed to Initialize EnsureNodesExist");
            }
        }

        private EntityContainer EnsureDataTypeContainerExists()
        {
            var ekmContainer = _dataTypeService.GetContainers("Ekom", 1).FirstOrDefault();
            if (ekmContainer == null)
            {
                var createContainerAttempt = _dataTypeService.CreateContainer(-1, "Ekom");
                if (createContainerAttempt.Success)
                {
                    ekmContainer = createContainerAttempt.Result.Entity;
                    _logger.Info<EnsureNodesExist>("Created Ekom DataType container");
                }
                else
                {
                    throw new EnsureNodesException("Unable to create container, failed creating Ekom Data Types", createContainerAttempt.Exception);
                }
            }

            return ekmContainer;
        }

        private IDataType EnsureDataTypeExists(DataType dt)
        {
            var textDt = _dataTypeService.GetDataType(dt.Name);

            if (textDt == null)
            {
                textDt = dt;
                _dataTypeService.Save(textDt);
                _logger.Info<EnsureNodesExist>(
                    "Created Data Type {Name}, editor alias {EditorAlias}",
                    dt.Name,
                    dt.EditorAlias
                );
            }

            return textDt;
        }

        private EntityContainer EnsureContainerExists(string name, int level = 1, int parentId = -1)
        {
            var ekmContainer = _contentTypeService.GetContainers(name, level).FirstOrDefault(x => x.ParentId == parentId);
            if (ekmContainer == null)
            {
                var createContainerAttempt = _contentTypeService.CreateContainer(parentId, name);
                if (createContainerAttempt.Success)
                {
                    ekmContainer = createContainerAttempt.Result.Entity;
                    _logger.Info<EnsureNodesExist>("Created doc type container {Name}", name);
                }
                else
                {
                    throw new EnsureNodesException("Unable to create container, failed creating Ekom nodes", createContainerAttempt.Exception);
                }
            }

            return ekmContainer;
        }

        private IContentType EnsureContentTypeExists(ContentType contentType)
        {
            var ekmContentType = _contentTypeService.Get(contentType.Alias);

            if (ekmContentType == null)
            {
                ekmContentType = contentType;
                _contentTypeService.Save(ekmContentType);
                _logger.Info<EnsureNodesExist>(
                    "Created content type {Name}, alias {Alias}",
                    contentType.Name,
                    contentType.Alias);
            }

            return ekmContentType;
        }

        private IContent EnsureContentExists(string name, string documentTypeAlias, int parentId = -1)
        {
            // ToDo: check for existence if we ever end up creating more content nodes

            var content = _contentService.Create(name, parentId, documentTypeAlias);

            OperationResult res;
            using (_contextFactory.EnsureUmbracoContext())
            {
                res = _contentService.Save(content);
            }

            if (res.Success)
            {
                _logger.Info<EnsureNodesExist>(
                    "Created content {Name}, alias {DocumentTypeAlias}",
                    name,
                    documentTypeAlias);

                return content;
            }
            else
            {
                throw new EnsureNodesException($"Unable to SaveAndPublish {name} content with doc type {documentTypeAlias} and parent {parentId}");
            }
        }

        public void Terminate()
        {
        }
    }
}
