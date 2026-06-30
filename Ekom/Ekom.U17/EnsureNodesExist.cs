using Ekom.Exceptions;
using Ekom.Models;
using Ekom.DataEditors;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;

namespace Ekom.Umb;

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
    private readonly IRuntimeState _runtimeState;
    private const string ContentPickerEditorUiAlias = "Umb.PropertyEditorUi.ContentPicker";

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
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
        IRuntimeState runtimeState)
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
        _runtimeState = runtimeState;
    }

    public void Initialize()
    {
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            // If Installing or Upgrading, we don't want to run this
            return;
        }

        _logger.LogDebug("Ensuring Umbraco nodes exist");

        try
        {
            // Test for existence of Ekom root node
            if (!_contentService.GetRootContent().Any(x => x.ContentType.Alias == "ekom" && !x.Trashed))
            {
                #region Property Editors

                if (!_propertyEditorCollection.TryGet("Ekom.Stock", out IDataEditor? stockEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Stock property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Cache", out IDataEditor? cacheEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Cache property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Coupon", out IDataEditor? couponEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Coupon property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Range", out IDataEditor? rangeEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Range property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Price", out IDataEditor? priceEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Price property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Country", out IDataEditor? countryPicker))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Country property picker, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Zone", out IDataEditor? zonePicker))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Zone property picker, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Currency", out IDataEditor? currencyPicker))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Currency property picker, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Umbraco.MultiNodeTreePicker", out IDataEditor? multiNodeEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.MultiNodeTreePicker property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Property", out IDataEditor? editor))
                {
                    throw new EnsureNodesException(
                        "Unable to find Ekom.Property property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Metafield", out IDataEditor? metafieldPicker))
                {
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.Metafield property picker, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Ekom.Metavalue", out IDataEditor? metavalueEditor))
                {
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.Metavalue property picker, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Umbraco.RadioButtonList", out IDataEditor? radioList))
                {
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.RadioButtonList property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Umbraco.MultipleTextstring", out IDataEditor? multipleTextstringEditor))
                {
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.MultipleTextstring property editor, failed creating Ekom nodes.");
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

                var booleanDt = GetDataType(new Guid("92897bc6-a5f3-4ffe-ae27-f2e7e33dda49"));
                var textstringDt = GetDataType(new Guid("0cc0eba1-9960-42c9-bf9b-60e150b429ae"));
                var numericDt = GetDataType(new Guid("2e6d3631-066e-44b8-aec4-96f09099b2b5"));
                var contentPickerDt = GetDataType(new Guid("fd1e0da5-5606-4862-b679-5d0cf3a52a59"));
                var mediaPickerDt = GetDataType(new Guid("4309a3ea-0d78-4329-a06c-c80b036af19a"));
                var multipleMediaPickerDt = GetDataType(new Guid("1b661f40-2242-4b44-b9cb-3990ee2b13c0"));
                var tagsDt = GetDataType(new Guid("b6b73142-b9c1-4bf8-a16d-e1c23320b549"));
                var rteDt = GetDataType(new Guid("ca90c950-0aff-4e72-b976-a30b1ac57dad"));
                var textareaDt = GetDataType(new Guid("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3"));
                var multipleTextstringDt = EnsureDataTypeExists(new DataType(multipleTextstringEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Repeatable Textstrings",
                });

                var propertyTextDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Textstring",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = textstringDt.Key,
                            name = textstringDt.Name,
                            propertyEditorAlias = textstringDt.EditorAlias,
                        },
                        useLanguages = true,
                        hideLabel = false,
                    }),
                });

                var propertyBoolStoreDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Boolean - Stores",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = booleanDt.Key,
                            name = booleanDt.Name,
                            propertyEditorAlias = booleanDt.EditorAlias,
                        },
                        useLanguages = false,
                        hideLabel = false,
                    }),
                });

                EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Boolean",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = booleanDt.Key,
                            name = booleanDt.Name,
                            propertyEditorAlias = booleanDt.EditorAlias,
                        },
                        useLanguages = true,
                        hideLabel = false,
                    }),
                });

                var propertyNumericDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Numeric - Stores",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = numericDt.Key,
                            name = numericDt.Name,
                            propertyEditorAlias = numericDt.EditorAlias,
                        },
                        useLanguages = false,
                        hideLabel = false,
                    }),
                });

                var propertyRteDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Richtext Editor",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = rteDt.Key,
                            name = rteDt.Name,
                            propertyEditorAlias = rteDt.EditorAlias,
                        },
                        useLanguages = true,
                        hideLabel = false,
                    }),
                });

                var propertyContentPickerDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Content Picker",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = contentPickerDt.Key,
                            name = contentPickerDt.Name,
                            propertyEditorAlias = contentPickerDt.EditorAlias,
                        },
                        useLanguages = true,
                        hideLabel = false,
                    }),
                });

                var propertyTextareaDt = EnsureDataTypeExists(new DataType(editor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Property Editor - Textarea",
                    ConfigurationData = ToConfigurationData(new
                    {
                        dataType = new
                        {
                            guid = textareaDt.Key,
                            name = textareaDt.Name,
                            propertyEditorAlias = textareaDt.EditorAlias,
                        },
                        useLanguages = true,
                        hideLabel = false,
                    }),
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
                    EditorUiAlias = "Ekom.PropertyEditorUi.Coupon",
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
                    EditorUiAlias = "Ekom.PropertyEditorUi.Range",
                });
                ConfigureEditorUi(rangeDt, "Ekom.PropertyEditorUi.Range");
                var metafieldDt = EnsureDataTypeExists(new DataType(metafieldPicker, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Metafield Picker",
                    EditorUiAlias = "Ekom.PropertyEditorUi.Metafield",
                });
                ConfigureEditorUi(metafieldDt, "Ekom.PropertyEditorUi.Metafield");
                var metavalueDt = EnsureDataTypeExists(new DataType(metavalueEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Metavalue Editor",
                    EditorUiAlias = "Ekom.PropertyEditorUi.Metavalue",
                });
                ConfigureEditorUi(metavalueDt, "Ekom.PropertyEditorUi.Metavalue");

                var multinodeCatalogDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Catalog Picker",
                    ConfigurationData = ToConfigurationData(new MultiNodePickerConfiguration
                    {
                        Filter = "ekmProduct, ekmProductVariant, ekmCategory",
                        TreeSource = new MultiNodePickerConfigurationTreeSource()
                        {

                            StartNodeQuery = "$root/ekom/ekmCatalog",
                        }
                    })
                });

                var multinodeProductDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Product Picker",
                    ConfigurationData = ToConfigurationData(new MultiNodePickerConfiguration
                    {
                        Filter = "ekmProduct",
                        TreeSource = new MultiNodePickerConfigurationTreeSource()
                        {
                            StartNodeQuery = "$root/ekom/ekmCatalog"
                        }
                    })
                });

                var multinodeCategoryDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Category Picker",
                    ConfigurationData = ToConfigurationData(new MultiNodePickerConfiguration
                    {
                        Filter = "ekmCategory",
                        TreeSource = new MultiNodePickerConfigurationTreeSource()
                        {
                            StartNodeQuery = "$root/ekom/ekmCatalog"
                        }
                    })
                });


                var discountTypeDt = EnsureDataTypeExists(new DataType(radioList, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Discount Type",
                    EditorUiAlias = "Umb.PropertyEditorUi.RadioButtonList",
                    ConfigurationData = ToConfigurationData(new
                    {
                        items = new[]
                        {
                            "Fixed",
                            "Percentage",
                        },
                    }),
                });
                ConfigureDiscountTypeDataType(discountTypeDt);

                var variantGroupDt = EnsureDataTypeExists(new DataType(multiNodeEditor, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Variant Group Picker",
                    ConfigurationData = ToConfigurationData(new MultiNodePickerConfiguration
                    {
                        Filter = "ekmProductVariantGroup",
                        TreeSource = new MultiNodePickerConfigurationTreeSource()
                        {
                            StartNodeQuery = "$current",
                        },
                        MaxNumber = 1
                    })
                });

                var shippingMethodDt = EnsureDataTypeExists(new DataType(radioList, _configurationEditorJsonSerializer, ekmDtContainer.Id)
                {
                    Name = "Ekom Shipping Method",
                    EditorUiAlias = "Umb.PropertyEditorUi.RadioButtonList",
                    ConfigurationData = ToConfigurationData(new
                    {
                        items = new[]
                        {
                            nameof(ShippingMethods.Pickup),
                            nameof(ShippingMethods.Delivery),
                        },
                    }),
                });
                ConfigureShippingMethodDataType(shippingMethodDt);


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
                                    new PropertyType(_shortStringHelper, propertyRteDt, "description")
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
                                    new PropertyType(_shortStringHelper, propertyBoolStoreDt, "disable")
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
                                    new PropertyType(_shortStringHelper, propertyRteDt, "description")
                                    {
                                        Name = "Description",
                                        SortOrder = 3
                                    },
                                    new PropertyType(_shortStringHelper, priceDt, "price")
                                    {
                                        Name = "Price",
                                        SortOrder = 4
                                    },
                                    new PropertyType(_shortStringHelper, stockDt, "stock")
                                    {
                                        Name = "Stock",
                                        SortOrder = 5
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "enableBackorder")
                                    {
                                        Name = "Enable Backorder",
                                        Description = "If set then the variant can be sold indefinitely",
                                        SortOrder = 6
                                    },
                                    new PropertyType(_shortStringHelper, numericDt, "vat")
                                    {
                                        Name = "VAT",
                                        Description = "%, override store VAT.",
                                        SortOrder = 7
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
                ConfigurePropertyDataType(productVariantCt, "description", propertyRteDt);

                var productVariantGroupCt = EnsureContentTypeExists(
                    new ContentType(_shortStringHelper, catalogContainer.Id)
                    {
                        Name = "Product Variant Group",
                        Alias = "ekmProductVariantGroup",
                        Icon = "icon-folder",
                        AllowedContentTypes = new List<ContentTypeSort>
                        {
                            CreateContentTypeSort(productVariantCt, 1),
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
                            CreateContentTypeSort(productVariantGroupCt, 1),
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
                                    new PropertyType(_shortStringHelper, propertyNumericDt, "ekmStockBuffer")
                                    {
                                        Name = "Stock Buffer",
                                        Description = "Reduces the available stock by this amount",
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
                                    new PropertyType(_shortStringHelper, textstringDt, "searchTags")
                                    {
                                        Name = "Search Tags",
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
                                    new PropertyType(_shortStringHelper, propertyBoolStoreDt, "disable")
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
                    CreateContentTypeSort(productCt, 1)
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
                                    new PropertyType(_shortStringHelper, propertyRteDt, "description")
                                    {
                                        Name = "Description",
                                    },
                                    new PropertyType(_shortStringHelper, multipleMediaPickerDt, "images")
                                    {
                                        Name = "Images",
                                    },
                                    new PropertyType(_shortStringHelper, booleanDt, "ekmVirtualUrl")
                                    {
                                        Name = "Virtual Url",
                                    },
                                    new PropertyType(_shortStringHelper, propertyNumericDt, "ekmStockBuffer")
                                    {
                                        Name = "Stock Buffer",
                                        Description = "Sets the stock buffer for the category, this value will be applied to all products within the category.",
                                    },
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
                                    new PropertyType(_shortStringHelper, propertyBoolStoreDt, "disable")
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
                            CreateContentTypeSort(categoryCt, 1)
                        );

                    _contentTypeService.Save(categoryCt);
                }

                var catalogCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, catalogContainer.Id)
                {
                    Name = "Catalog",
                    Alias = "ekmCatalog",
                    AllowedContentTypes = new List<ContentTypeSort>
                {
                    CreateContentTypeSort(categoryCt, 1),
                },
                    Icon = "icon-books",
                });

                ConfigureCatalogContentPicker(multinodeCatalogDt, catalogCt, "ekmProduct, ekmProductVariant, ekmCategory", originAlias: "Root", queryStepAlias: "NearestDescendantOrSelf");
                ConfigureCatalogContentPicker(multinodeProductDt, catalogCt, "ekmProduct");
                ConfigureCatalogContentPicker(multinodeCategoryDt, catalogCt, "ekmCategory");
                ConfigureCatalogContentPicker(variantGroupDt, catalogCt, "ekmProductVariantGroup", maxNumber: 1);
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
                                        new PropertyType(_shortStringHelper, multinodeCatalogDt, "excludeDiscountItems")
                                        {
                                            Name = "Exclude Discount Items",
                                            SortOrder = 8,
                                            Description = "Exclude items from discount items. For example if you select a category in discount items you can exclude a single product here."
                                        },
                                        new PropertyType(_shortStringHelper, booleanDt, "stackable")
                                        {
                                            Name = "Stackable",
                                            SortOrder = 9,
                                        },
                                        new PropertyType(_shortStringHelper, booleanDt, "globalDiscount")
                                        {
                                            Name = "Global Discount",
                                            SortOrder = 10,
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
                    CreateContentTypeSort(orderDiscountCt, 1),
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
                                        new PropertyType(_shortStringHelper, multinodeCatalogDt, "excludeDiscountItems")
                                        {
                                            Name = "Exclude Discount Items",
                                            SortOrder = 8,
                                            Description = "Exclude items from discount items. For example if you select a category in discount items you can exclude a single product here."
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
                    CreateContentTypeSort(productDiscountCt, 1),
                },
                });

                var discountsCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, discountsContainer.Id)
                {
                    Name = "Discounts",
                    Alias = "ekmDiscounts",
                    Icon = "icon-bills-euro",
                    AllowedContentTypes = new List<ContentTypeSort>
                {
                    CreateContentTypeSort(orderDiscountsCt, 1),
                    CreateContentTypeSort(productDiscountsCt, 2),
                },
                });

                #endregion

                #region Payment Providers

                var paymentProviderCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, ppContainer.Id)
                {
                    Name = "Payment Provider",
                    Alias = "ekmPaymentProvider",
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
                                    Name = "Payment Method",
                                    Description = "By default, the node's name is used to target the payment provider. However, if you want to add the same provider multiple times or use a more descriptive name, you can set it here."
                                },
                                new PropertyType(_shortStringHelper, textstringDt, "configurationKey")
                                {
                                    Name = "Configuration Key",
                                    Description = "You can set the config section key here to be able to have multiple configs for the same provider"
                                },
                                new PropertyType(_shortStringHelper, numericDt, "discount")
                                {
                                    Name = "Discount",
                                },
                                new PropertyType(_shortStringHelper, propertyContentPickerDt, "successUrl")
                                {
                                    Name = "Success Url",
                                    Mandatory = true,
                                },
                                new PropertyType(_shortStringHelper, propertyContentPickerDt, "errorUrl")
                                {
                                    Name = "Error Url",
                                },
                                new PropertyType(_shortStringHelper, propertyContentPickerDt, "cancelUrl")
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
                ConfigurePropertyDataType(paymentProviderCt, "successUrl", propertyContentPickerDt);
                ConfigurePropertyDataType(paymentProviderCt, "errorUrl", propertyContentPickerDt);
                ConfigurePropertyDataType(paymentProviderCt, "cancelUrl", propertyContentPickerDt);

                var paymentProvidersCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, ppContainer.Id)
                {
                    Name = "Payment Providers",
                    Alias = "ekmPaymentProviders",
                    Icon = "icon-bills",
                    AllowedContentTypes = new List<ContentTypeSort>
                {
                    CreateContentTypeSort(paymentProviderCt, 1),
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
                    PropertyGroups = new PropertyGroupCollection(
                        new List<PropertyGroup>
                        {
                        new PropertyGroup(new PropertyTypeCollection(
                            true,
                            new List<PropertyType>
                            {

                                new PropertyType(_shortStringHelper, shippingMethodDt, "shippingMethod")
                                {
                                    Name = "Shipping Method",
                                    Mandatory = true
                                }
                            }))
                        {
                            Alias = "settings",
                            Name = "Settings",
                            Type = PropertyGroupType.Tab
                        },
                        }
                    ),
                });
                ConfigurePropertyDataType(shippingProviderCt, "shippingMethod", shippingMethodDt);

                var shippingProvidersCt = EnsureContentTypeExists(new ContentType(_shortStringHelper, spContainer.Id)
                {
                    Name = "Shipping Providers",
                    Alias = "ekmShippingProviders",
                    Icon = "icon-boat-shipping",
                    AllowedContentTypes = new List<ContentTypeSort>
                    {
                        CreateContentTypeSort(shippingProviderCt, 1),
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
                                new PropertyType(_shortStringHelper, multipleTextstringDt, "cultures")
                                {
                                    Name = "Cultures",
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
                                new PropertyType(_shortStringHelper, propertyTextDt, "urlPrefix")
                                {
                                    Name = "Url Prefix",
                                },
                                new PropertyType(_shortStringHelper, booleanDt, "userBasket")
                                {
                                    Name = "User Basket",
                                    Description = "The store will keep a basket for the user between sessions and store the orderId on the memeber."
                                },
                                new PropertyType(_shortStringHelper, booleanDt, "shareBasketBetweenStores")
                                {
                                    Name = "Share Basket Between Stores",
                                    Description = "This will allow baskets to be shared between stores but be aware that it requires the same currencies to be available cross stores."
                                },
                                new PropertyType(_shortStringHelper, booleanDt, "applyVatOnShipping")
                                {
                                    Name = "Apply Vat On Shipping",
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
                    CreateContentTypeSort(storeCt, 1),
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
                    CreateContentTypeSort(zoneCt, 1),
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
                                new PropertyType(_shortStringHelper, textstringDt, "alias")
                                {
                                    Name = "Alias",
                                    Mandatory = true
                                },
                                new PropertyType(_shortStringHelper, textstringDt, "description")
                                {
                                    Name = "Description",
                                    Mandatory = true
                                },
                                new PropertyType(_shortStringHelper, booleanDt, "enableMultipleChoice")
                                {
                                    Name = "Filterable",
                                    Description = "When checked, the metafield will be visible in the filter."
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
                                new PropertyType(_shortStringHelper, booleanDt, "required")
                                {
                                    Name = "Required"
                                },
                                new PropertyType(_shortStringHelper, booleanDt, "readOnly")
                                {
                                    Name = "Read Only",
                                    Description = "When checked, the field will be read-only. This is good for metafields that data are for example synced to."
                                },
                                new PropertyType(_shortStringHelper, booleanDt, "allConditionsMustMatch")
                                {
                                    Name = "All Conditions Must Match",
                                    Description = "Enable this option to require all selected values within this filter to match. By default, any value can match (OR logic)."
                                }
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
                        CreateContentTypeSort(metafieldCt, 1),
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
                    CreateContentTypeSort(storesCt, 1),
                    CreateContentTypeSort(catalogCt, 2),
                    CreateContentTypeSort(shippingProvidersCt, 3),
                    CreateContentTypeSort(paymentProvidersCt, 4),
                    CreateContentTypeSort(zonesCt, 5),
                    CreateContentTypeSort(discountsCt, 6),
                },
                    Icon = "icon-box color-green",
                });

                #region Content Nodes

                var ekom = EnsureContentExists("Ekom", "ekom");
                var catalog = EnsureContentExists("Catalog", "ekmCatalog", ekom.Id);
                EnsureContentExists("Shipping Providers", "ekmShippingProviders", ekom.Id);
                EnsureContentExists("Payment Providers", "ekmPaymentProviders", ekom.Id);
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

    private IDataType GetDataType(Guid key)
    {
        return _dataTypeService.GetAsync(key).GetAwaiter().GetResult()
            ?? throw new EnsureNodesException($"Unable to find data type {key}, failed creating Ekom nodes.");
    }

    private static ContentTypeSort CreateContentTypeSort(IContentType contentType, int sortOrder)
    {
        return new ContentTypeSort(contentType.Key, sortOrder, contentType.Alias);
    }

    private void ConfigureCatalogContentPicker(
        IDataType dataType,
        IContentType catalogContentType,
        string allowedContentTypes,
        int maxNumber = 0,
        string originAlias = "Current",
        string queryStepAlias = "NearestAncestorOrSelf")
    {
        dataType.EditorUiAlias = ContentPickerEditorUiAlias;
        dataType.ConfigurationData = ToConfigurationData(new MultiNodePickerConfiguration
        {
            Filter = allowedContentTypes,
            MaxNumber = maxNumber,
            TreeSource = new MultiNodePickerConfigurationTreeSource
            {
                ObjectType = "content",
                DynamicRoot = new DynamicRoot
                {
                    OriginAlias = originAlias,
                    QuerySteps = new[]
                    {
                        new QueryStep
                        {
                            Alias = queryStepAlias,
                            AnyOfDocTypeKeys = new[]
                            {
                                catalogContentType.Key,
                            },
                        },
                    },
                },
            },
        });

        _dataTypeService.Save(dataType);
    }

    private void ConfigureDiscountTypeDataType(IDataType dataType)
    {
        dataType.EditorUiAlias = "Umb.PropertyEditorUi.RadioButtonList";
        dataType.ConfigurationData = ToConfigurationData(new
        {
            items = new[]
            {
                "Fixed",
                "Percentage",
            },
        });

        _dataTypeService.Save(dataType);
    }

    private void ConfigureShippingMethodDataType(IDataType dataType)
    {
        dataType.EditorUiAlias = "Umb.PropertyEditorUi.RadioButtonList";
        dataType.ConfigurationData = ToConfigurationData(new
        {
            items = new[]
            {
                nameof(ShippingMethods.Pickup),
                nameof(ShippingMethods.Delivery),
            },
        });

        _dataTypeService.Save(dataType);
    }

    private void ConfigureEditorUi(IDataType dataType, string editorUiAlias)
    {
        dataType.EditorUiAlias = editorUiAlias;
        _dataTypeService.Save(dataType);
    }

    private void ConfigurePropertyDataType(IContentType contentType, string propertyAlias, IDataType dataType)
    {
        var propertyType = contentType.PropertyTypes.FirstOrDefault(property => property.Alias == propertyAlias);

        if (propertyType == null || propertyType.DataTypeId == dataType.Id)
        {
            return;
        }

        propertyType.DataTypeId = dataType.Id;
        _contentTypeService.Save(contentType);
    }

    private static Dictionary<string, object> ToConfigurationData<TConfiguration>(TConfiguration configuration)
    {
        var json = JsonSerializer.Serialize(configuration);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
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
