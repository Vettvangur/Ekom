using Ekom.Exceptions;
using Ekom.Umb.DataEditors;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;

namespace Ekom.App_Start
{
    class EnsureNodesExist : IComponent
    {
        private readonly ILogger<EnsureNodesExist> _logger;
        private readonly Configuration _configuration;
        private readonly IContentService _contentService;
        private readonly IFileService _fileService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataTypeService _dataTypeService;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IUmbracoContextFactory _contextFactory;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

        public EnsureNodesExist(
            ILogger<EnsureNodesExist> logger,
            IFileService fileService,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            PropertyEditorCollection propertyEditorCollection,
            Configuration configuration,
            IUmbracoContextFactory contextFactory,
            IShortStringHelper shortStringHelper,
            IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        {
            _logger = logger;
            _fileService = fileService;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _propertyEditorCollection = propertyEditorCollection;
            _configuration = configuration;
            _contextFactory = contextFactory;
            _shortStringHelper = shortStringHelper;
            _configurationEditorJsonSerializer = configurationEditorJsonSerializer;
        }

        public void Initialize()
        {
            _logger.LogDebug("Ensuring Umbraco nodes exist");

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
                    if (!_propertyEditorCollection.TryGet("Ekom.Property", out IDataEditor editor))
                    {
                        throw new EnsureNodesException(
                            "Unable to find Ekom.Property property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Umbraco.DropDown.Flexible", out IDataEditor dropdownEditor))
                    {
                        throw new EnsureNodesException(
                            "Unable to find Umbraco.DropDown.Flexible property editor, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Metafield", out IDataEditor metafieldPicker))
                    {
                        throw new EnsureNodesException(
                            "Unable to find Umbraco.Metafield property picker, failed creating Ekom nodes.");
                    }
                    if (!_propertyEditorCollection.TryGet("Ekom.Metavalue", out IDataEditor metavalueEditor))
                    {
                        throw new EnsureNodesException(
                            "Unable to find Umbraco.Metavalue property picker, failed creating Ekom nodes.");
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
                    var mediaPickerDt = _dataTypeService.GetDataType(new Guid("4309a3ea-0d78-4329-a06c-c80b036af19a"));
                    var multipleMediaPickerDt = _dataTypeService.GetDataType(new Guid("1b661f40-2242-4b44-b9cb-3990ee2b13c0"));
                    var tagsDt = _dataTypeService.GetDataType(new Guid("b6b73142-b9c1-4bf8-a16d-e1c23320b549"));
                    var rteDt = _dataTypeService.GetDataType(new Guid("ca90c950-0aff-4e72-b976-a30b1ac57dad"));
                    var textareaDt = _dataTypeService.GetDataType(new Guid("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3"));

                    var propertyTextDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Property Editor - Textstring",
                        Configuration = new EkomPropertyEditorConfiguration
                        {
                            DataType = new
                            {
                                guid = textstringDt.Key,
                                name = textstringDt.Name,
                                propertyEditorAlias = textstringDt.EditorAlias,
                            },
                            useLanguages = true,
                            HideLabel = false
                        },
                    });

                    var propertyBoolDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Property Editor - Boolean",
                        Configuration = new EkomPropertyEditorConfiguration
                        {
                            DataType = new
                            {
                                guid = booleanDt.Key,
                                name = booleanDt.Name,
                                propertyEditorAlias = booleanDt.EditorAlias,
                            },
                            useLanguages = true,
                            HideLabel = false
                        },
                    });

                    var propertyRteDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Property Editor - Richtext Editor",
                        Configuration = new EkomPropertyEditorConfiguration
                        {
                            DataType = new
                            {
                                guid = rteDt.Key,
                                name = rteDt.Name,
                                propertyEditorAlias = rteDt.EditorAlias,
                            },
                            useLanguages = true,
                            HideLabel = false
                        },
                    });

                    var propertyTextareaDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Property Editor - Textarea",
                        Configuration = new EkomPropertyEditorConfiguration
                        {
                            DataType = new
                            {
                                guid = textareaDt.Key,
                                name = textareaDt.Name,
                                propertyEditorAlias = textareaDt.EditorAlias,
                            },
                            useLanguages = true,
                            HideLabel = false
                        },
                    });

                    var stockDt = EnsureDataTypeExists(new DataType(stockEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Stock Editor",
                    });
                    var cacheDt = EnsureDataTypeExists(new DataType(cacheEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Cache Editor",
                    });
                    var couponDt = EnsureDataTypeExists(new DataType(couponEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Coupons Editor",
                    });
                    var currencyDt = EnsureDataTypeExists(new DataType(currencyPicker, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Currency Picker",
                    });
                    var zoneDt = EnsureDataTypeExists(new DataType(zonePicker, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Zone Picker",
                    });
                    var countryDt = EnsureDataTypeExists(new DataType(countryPicker, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Country Picker",
                    });
                    var priceDt = EnsureDataTypeExists(new DataType(priceEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Price Editor",
                    });
                    var rangeDt = EnsureDataTypeExists(new DataType(rangeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Range Editor",
                    });
                    var metafieldDt = EnsureDataTypeExists(new DataType(metafieldPicker, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Metafield Picker",
                    });
                    var metavalueDt = EnsureDataTypeExists(new DataType(metavalueEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Metavalue Editor",
                    });

                    var multinodeCatalogDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Catalog Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmProduct, ekmProductVariant, ekmCategory",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {

                                StartNodeQuery = "$root/ekom/ekmCatalog",
                            }
                        }
                    });

                    var multinodeProductDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Product Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmProduct",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {
                                StartNodeQuery = "$root/ekom/ekmCatalog"
                            }
                        }
                    });

                    var multinodeCategoryDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Category Picker",
                        Configuration = new MultiNodePickerConfiguration
                        {
                            Filter = "ekmCategory",
                            TreeSource = new MultiNodePickerConfigurationTreeSource()
                            {
                                StartNodeQuery = "$root/ekom/ekmCatalog"
                            }
                        }
                    });

                    var discountTypeDt = EnsureDataTypeExists(new DataType(dropdownEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Discount Type",
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

                    var variantGroupDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                    {
                        Name = "Ekom Variant Group Picker",
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
                    var metafieldContainer = EnsureContainerExists("Metafields", 2, ekmDocTypeContainer.Id);

                    #region Compositions

                    var baseComposition = EnsureContentTypeExists(
                        new ContentType(_shortStringHelper, compositionsContainer.Id)
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
                                        new PropertyType(_shortStringHelper, propertyTextDt, "title")
                                        {
                                            Name = "Title",
                                        },
                                        new PropertyType(_shortStringHelper, propertyTextareaDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                    }))
                                {
                                    Alias = "settings",
                                    Name = "Settings",
                                    Type = PropertyGroupType.Tab
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(_shortStringHelper, propertyBoolDt, "disable")
                                        {
                                            Name = "Disable",
                                        },
                                    }))
                                {
                                    Alias = "stores",
                                    Name = "Stores",
                                    Type = PropertyGroupType.Tab
                                },
                                }),
                        }
                    );

                    var providerComposition = EnsureContentTypeExists(
                      new ContentType(_shortStringHelper, compositionsContainer.Id)
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
                                        new PropertyType(_shortStringHelper, contentPickerDt, "zone")
                                        {
                                            Name = "Zone",
                                        },
                                        new PropertyType(_shortStringHelper, priceDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                    }))
                                {
                                    Alias = "settings",
                                    Name = "Settings",
                                    Type = PropertyGroupType.Tab
                                },
                            }),
                      }
                    );

                    var rangeComposition = EnsureContentTypeExists(
                        new ContentType(_shortStringHelper, compositionsContainer.Id)
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
                                        new PropertyType(_shortStringHelper, rangeDt, "startOfRange")
                                        {
                                            Name = "Start of Range",
                                            SortOrder = 20
                                        },
                                        new PropertyType(_shortStringHelper, rangeDt, "endOfRange")
                                        {
                                            Name = "End of Range",
                                            SortOrder = 21
                                        },
                                    }))
                                {
                                    Alias = "settings",
                                    Name = "Settings",
                                    Type = PropertyGroupType.Tab
                                },
                            }),
                        }
                    );

                    #endregion

                    #region Catalog Document Types

                    var productVariantCt = EnsureContentTypeExists(
                        new ContentType(_shortStringHelper, catalogContainer.Id)
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
                                        new PropertyType(_shortStringHelper, propertyTextDt, "title")
                                        {
                                            Name = "Title",
                                            SortOrder = 0
                                        },
                                        new PropertyType(_shortStringHelper, textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                            SortOrder = 1
                                        },
                                        new PropertyType(_shortStringHelper, multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                            SortOrder = 2
                                        },
                                        new PropertyType(_shortStringHelper, priceDt, "price")
                                        {
                                            Name = "Price",
                                            SortOrder = 3
                                        },
                                        new PropertyType(_shortStringHelper, stockDt, "stock")
                                        {
                                            Name = "Stock",
                                            SortOrder = 4
                                        },
                                        new PropertyType(_shortStringHelper, booleanDt, "enableBackorder")
                                        {
                                            Name = "Enable Backorder",
                                            Description = "If set then the variant can be sold indefinitely",
                                            SortOrder = 5
                                        },
                                        new PropertyType(_shortStringHelper, numericDt, "vat")
                                        {
                                            Name = "VAT",
                                            Description = "%, override store VAT.",
                                            SortOrder = 6
                                        },
                                    }))
                                {
                                    Alias = "variant",
                                    Name = "Variant",
                                    Type = PropertyGroupType.Tab
                                }
                            }),
                        }
                    );

                    var productVariantGroupCt = EnsureContentTypeExists(
                        new ContentType(_shortStringHelper, catalogContainer.Id)
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
                                        new PropertyType(_shortStringHelper, propertyTextDt, "title")
                                        {
                                            Name = "Title"
                                        },
                                        new PropertyType(_shortStringHelper, multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                        new PropertyType(_shortStringHelper, textstringDt, "color")
                                        {
                                            Name = "Color",
                                        },
                                    }))
                                {
                                     Alias = "variantGroup",
                                    Name = "Variant Group",
                                },
                            }),
                        }
                    );

                    var productCt = EnsureContentTypeExists(
                        new ContentType(_shortStringHelper, catalogContainer.Id)
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
                                        new PropertyType(_shortStringHelper, propertyTextDt, "title")
                                        {
                                            Name = "Title",
                                            Mandatory  = true
                                        },
                                        new PropertyType(_shortStringHelper, propertyTextDt, "slug")
                                        {
                                            Name = "Slug",
                                            Mandatory  = true
                                        },
                                        new PropertyType(_shortStringHelper, booleanDt, "updateSlug")
                                        {
                                            Name = "Update Slug",
                                        },
                                        new PropertyType(_shortStringHelper, textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                        },
                                        new PropertyType(_shortStringHelper, propertyTextareaDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                        new PropertyType(_shortStringHelper, multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                        new PropertyType(_shortStringHelper, priceDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                        new PropertyType(_shortStringHelper, stockDt, "stock")
                                        {
                                            Name = "Stock",
                                        },
                                        new PropertyType(_shortStringHelper, booleanDt, "enableBackorder")
                                        {
                                            Name = "Enable Backorder",
                                            Description = "If set then the product can be sold indefinitely"
                                        },
                                        new PropertyType(_shortStringHelper, numericDt, "vat")
                                        {
                                            Name = "VAT",
                                            Description = "%, override store VAT."
                                        },
                                        new PropertyType(_shortStringHelper, multinodeCategoryDt, "categories")
                                        {
                                            Name = "Product Categories",
                                            Description = "Allows a product to belong to categories other than it's umbraco node parent categories. A single product node can therefore belong to multiple logical category tree hierarchies.",
                                        },
                                        new PropertyType(_shortStringHelper, multinodeProductDt, "relatedProducts")
                                        {
                                            Name = "Related Products",
                                        },
                                        new PropertyType(_shortStringHelper, variantGroupDt, "primaryVariantGroup")
                                        {
                                            Name = "Primary Variant Group",
                                        },
                                    }))
                                {
                                    Alias = "product",
                                    Name = "Product",
                                    Type = PropertyGroupType.Tab
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(_shortStringHelper, propertyBoolDt, "disable")
                                        {
                                            Name = "Disable",
                                        },
                                    }))
                                {
                                    Alias = "stores",
                                    Name = "Stores",
                                    Type = PropertyGroupType.Tab
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(_shortStringHelper, metafieldDt, "disable")
                                        {
                                            Name = "Metafields",
                                            Alias = "metafields"
                                        },
                                    }))
                                {
                                    Name = "Metafields",
                                    Alias = "metafields",
                                    Type = PropertyGroupType.Tab
                                }
                            })
                        }
                    );

                    var categoryCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, catalogContainer.Id)
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
                                        new PropertyType(_shortStringHelper, propertyTextDt, "title")
                                        {
                                            Name = "Title",
                                            Mandatory = true
                                        },
                                        new PropertyType(_shortStringHelper, propertyTextDt, "slug")
                                        {
                                            Name = "Slug",
                                            Mandatory = true
                                        },
                                        new PropertyType(_shortStringHelper, booleanDt, "updateSlug")
                                        {
                                            Name = "Update Slug",
                                        },
                                        new PropertyType(_shortStringHelper, textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                        },
                                        new PropertyType(_shortStringHelper, propertyTextareaDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                        new PropertyType(_shortStringHelper, mediaPickerDt, "categoryImage")
                                        {
                                            Name = "Category Image",
                                        }
                                    }))
                                    {
                                        Alias = "category",
                                        Name = "Category",
                                        Type = PropertyGroupType.Tab
                                    },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(_shortStringHelper, propertyBoolDt, "disable")
                                        {
                                            Name = "Disable",
                                        },
                                    }))
                                {
                                     Alias = "stores",
                                    Name = "Stores",
                                    Type = PropertyGroupType.Tab
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

                    var catalogCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, catalogContainer.Id)
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

                    var orderDiscountCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, discountsContainer.Id)
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
                                            new PropertyType(_shortStringHelper, discountTypeDt, "type")
                                            {
                                                Name = "Type",
                                                Mandatory = true,
                                                SortOrder = 5,
                                            },
                                            new PropertyType(_shortStringHelper, rangeDt, "discount")
                                            {
                                                Name = "Discount",
                                                Mandatory = true,
                                                SortOrder = 6,
                                            },
                                            new PropertyType(_shortStringHelper, multinodeCatalogDt, "discountItems")
                                            {
                                                Name = "Discount Items",
                                                SortOrder = 7,
                                                Description = "Controls what items in the order receive the discount. (In contrast to product discount, discount items, where it is used as a constraint)"
                                            },
                                            new PropertyType(_shortStringHelper, booleanDt, "stackable")
                                            {
                                                Name = "Stackable",
                                                SortOrder = 8,
                                            },
                                            new PropertyType(_shortStringHelper, booleanDt, "globalDiscount")
                                            {
                                                Name = "Global Discount",
                                                SortOrder = 9,
                                                Description = "This couponless discount will be automatically applied to orders that match it's constraints"
                                            },
                                        }))
                                    {
                                        Alias = "settings",
                                        Name = "Settings",
                                        Type = PropertyGroupType.Tab
                                    },
                                    new PropertyGroup(new PropertyTypeCollection(
                                        true,
                                        new List<PropertyType>
                                        {
                                            new PropertyType(_shortStringHelper, couponDt, "coupons")
                                            {
                                                Name = "Coupons",
                                            },
                                        }))
                                    {
                                        Alias = "coupons",
                                        Name = "Coupons",
                                        Type = PropertyGroupType.Tab
                                    }
                                }
                            ),
                    });

                    var orderDiscountsCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, discountsContainer.Id)
                    {
                        Name = "Order Discounts",
                        Alias = "ekmOrderDiscounts",
                        Icon = "icon-bulleted-list",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(orderDiscountCt.Id, 1),
                    },
                    });

                    var productDiscountCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, discountsContainer.Id)
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
                                            new PropertyType(_shortStringHelper, discountTypeDt, "type")
                                            {
                                                Name = "Type",
                                                Mandatory = true,
                                                SortOrder = 5,
                                            },
                                            new PropertyType(_shortStringHelper, rangeDt, "discount")
                                            {
                                                Name = "Discount",
                                                Mandatory = true,
                                                SortOrder = 6,
                                            },
                                            new PropertyType(_shortStringHelper, multinodeCatalogDt, "discountItems")
                                            {
                                                Name = "Discount Items",
                                                SortOrder = 7,
                                                Description = "Discount is automatically applied to selected items if the other constraints are valid.",
                                            },
                                        }))
                                    {
                                        Alias = "settings",
                                        Name = "Settings",
                                        Type = PropertyGroupType.Tab
                                    }
                                }
                            ),
                    });

                    var productDiscountsCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, discountsContainer.Id)
                    {
                        Name = "Product Discounts",
                        Alias = "ekmProductDiscounts",
                        Icon = "icon-bulleted-list",
                        AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(productDiscountCt.Id, 1),
                    },
                    });

                    var discountsCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, discountsContainer.Id)
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

                    var netPaymentProviderCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, ppContainer.Id)
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
                                    new PropertyType(_shortStringHelper, textstringDt, "basePaymentProvider")
                                    {
                                        Name = "Base Payment Provider DLL",
                                        Description = "Allows payment provider overloading. " +
                                            "F.x. Borgun ISK and Borgun USD nodes with different names and different xml configurations targetting the same base payment provider."
                                    },
                                    new PropertyType(_shortStringHelper, numericDt, "discount")
                                    {
                                        Name = "Discount",
                                    },
                                    new PropertyType(_shortStringHelper, propertyTextDt, "successUrl")
                                    {
                                        Name = "Success Url",
                                        Mandatory = true,
                                    },
                                    new PropertyType(_shortStringHelper, propertyTextDt, "errorUrl")
                                    {
                                        Name = "Error Url",
                                    },
                                    new PropertyType(_shortStringHelper, propertyTextDt, "cancelUrl")
                                    {
                                        Name = "Cancel Url",
                                    },
                                    new PropertyType(_shortStringHelper, mediaPickerDt, "logo")
                                    {
                                        Name = "Logo",
                                    },
                                    new PropertyType(_shortStringHelper, textstringDt, "currency")
                                    {
                                        Name = "Currency"
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "offlinePayment")
                                    {
                                        Name = "Offline Payment"
                                    },
                                }))
                            {
                                Alias = "settings",
                                Name = "Settings",
                                Type = PropertyGroupType.Tab
                            },
                            }
                        ),
                    });

                    var netPaymentProvidersCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, ppContainer.Id)
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

                    var shippingProviderCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, spContainer.Id)
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

                    var shippingProvidersCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, spContainer.Id)
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

                    var storeCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, storeContainer.Id)
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
                                    new PropertyType(_shortStringHelper, contentPickerDt, "storeRootNode")
                                    {
                                        Name = "Store Root Node",
                                        Mandatory = true,
                                    },
                                    new PropertyType(_shortStringHelper, numericDt, "vat")
                                    {
                                        Name = "Vat",
                                        Mandatory = true,
                                    },
                                    new PropertyType(_shortStringHelper, textstringDt, "culture")
                                    {
                                        Name = "Culture",
                                        Mandatory = true,
                                    },
                                    new PropertyType(_shortStringHelper, currencyDt, "currency")
                                    {
                                        Name = "Currency",
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "vatIncludedInPrice")
                                    {
                                        Name = "Vat included in price",
                                    },
                                    new PropertyType(_shortStringHelper, textstringDt, "orderNumberTemplate")
                                    {
                                        Name = "Order Number Template",
                                        Description ="Define how the ordernumber will be created. You can use #orderId# #orderIdPadded#, #storeAlias#, #day#, #month# and #year#, plus any characters you need"
                                    },
                                    new PropertyType(_shortStringHelper, textstringDt, "orderNumberPrefix")
                                    {
                                        Name = "Order Number Prefix",
                                    },
                                })
                            )
                            {
                                Alias = "store",
                                Name = "Store",
                                Type = PropertyGroupType.Tab
                            }
                            }
                        ),
                    });

                    var storesCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, storeContainer.Id)
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

                    var zoneCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, zoneContainer.Id)
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
                                    new PropertyType(_shortStringHelper, tagsDt, "zoneSelector")
                                    {
                                        Name = "Zone Selector",
                                    },
                                })
                            ){
                                Alias = "zones",
                                Name = "Zones",
                                Type = PropertyGroupType.Tab
                            }
                            }
                        )
                    });

                    var zonesCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, zoneContainer.Id)
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

                    #region Metafields

                    var metafieldCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, metafieldContainer.Id)
                    {
                        Name = "Metafield",
                        Alias = "ekmMetafield",
                        Icon = "icon-ordered-list",
                        PropertyGroups = new PropertyGroupCollection(
                            new List<PropertyGroup>
                            {
                            new PropertyGroup(new PropertyTypeCollection(
                                true,
                                new List<PropertyType>
                                {
                                    new PropertyType(_shortStringHelper, propertyTextDt, "title")
                                    {
                                        Name = "Title",
                                        Mandatory = true
                                    },
                                    new PropertyType(_shortStringHelper, textstringDt, "description")
                                    {
                                        Name = "Description",
                                        Mandatory = true
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "enableMultipleChoice")
                                    {
                                        Name = "Filterable"
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "filterable")
                                    {
                                        Name = "Enable Multiple Choice",
                                        Description  = "When checked, the dropdown will be a select multiple / combo box style dropdown."
                                    },
                                    new PropertyType(_shortStringHelper, metavalueDt, "values")
                                    {
                                        Name = "Values",
                                        Description  = "If no values are set then an textstring input is used."
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "enableMultipleChoice")
                                    {
                                        Name = "Required"
                                    },
                                })
                            ){
                                Alias = "metafield",
                                Name = "Metafield",
                                Type = PropertyGroupType.Tab
                            }
                            }
                        )
                    });

                    var metafieldsCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, metafieldContainer.Id)
                    {
                        Name = "Metafields",
                        Alias = "ekmMetafields",
                        Icon = "icon-ordered-list",
                        AllowedContentTypes = new List<ContentTypeSort>
                        {
                            new ContentTypeSort(metafieldCt.Id, 1),
                        },
                    });

                    #endregion

                    var ekmCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, ekmDocTypeContainer.Id)
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
                                        new PropertyType(_shortStringHelper, cacheDt, "Cache")
                                        {
                                            Name = "Populate Cache",
                                        }
                                    }))
                                {
                                    Alias = "ekom",
                                    Name = "Ekom",
                                    Type= PropertyGroupType.Tab
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
                    EnsureContentExists("Metafields", "ekmMetafields", ekom.Id);

                    #endregion
                }

                _logger.LogDebug("Done");
            }
#pragma warning disable CA1031 // Should not kill startup
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex, "Failed to Initialize EnsureNodesExist");
            }
        }

        private EntityContainer EnsureDataTypeContainerExists()
        {
            var ekmContainer = _dataTypeService.GetContainers("Ekom", 1).FirstOrDefault();
            if (ekmContainer == null)
            {
                var createContainerAttempt = _dataTypeService.CreateContainer(-1, Guid.NewGuid(), "Ekom");
                if (createContainerAttempt.Success)
                {
                    ekmContainer = createContainerAttempt.Result.Entity;
                    _logger.LogInformation("Created Ekom DataType container");
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
                _logger.LogInformation(
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
                var createContainerAttempt = _contentTypeService.CreateContainer(parentId, Guid.NewGuid(), name);
                if (createContainerAttempt.Success)
                {
                    ekmContainer = createContainerAttempt.Result.Entity;
                    _logger.LogInformation("Created doc type container {Name}", name);
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
                _logger.LogInformation(
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
                _logger.LogInformation(
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
