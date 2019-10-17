using Ekom.Exceptions;
using GMO.Vorto.PropertyEditor;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Vorto.Models;
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
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataTypeService _dataTypeService;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IUmbracoContextFactory _contextFactory;

        public EnsureNodesExist(
            ILogger logger,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            PropertyEditorCollection propertyEditorCollection,
            Configuration configuration,
            IUmbracoContextFactory contextFactory)
        {
            _logger = logger;
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

            // Test for existence of Ekom root node
            if (!_contentService.GetRootContent().Any(x => x.ContentType.Alias == "ekom"))
            {
                #region Property Editors

                if (!_propertyEditorCollection.TryGet("Ekom.Stock", out IDataEditor stockEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Ekom Stock property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Umbraco.MultiNodeTreePicker", out IDataEditor multiNodeEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.MultiNodeTreePicker property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Umbraco.Grid", out IDataEditor gridEditor))
                {
                    // Should never happen
                    throw new EnsureNodesException(
                        "Unable to find Umbraco.Grid property editor, failed creating Ekom nodes.");
                }
                if (!_propertyEditorCollection.TryGet("Our.Umbraco.Vorto", out IDataEditor editor))
                {
                    throw new EnsureNodesException(
                        "Unable to find Our.Umbraco.Vorto property editor, failed creating Ekom nodes. Ensure GMO.Vorto.Web is installed.");
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
                var stockDt = EnsureDataTypeExists(new DataType(stockEditor, ekmDtContainer.Id)
                {
                    Name = "Ekom - Stock",
                });
                var perStoreStockDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                {
                    Name = "Ekom - Stock - Per Store",
                    Configuration = new VortoConfiguration
                    {
                        DataType = new DataTypeInfo
                        {
                            Guid = stockDt.Key,
                            Name = stockDt.Name,
                            PropertyEditorAlias = stockDt.EditorAlias,
                        },
                        MandatoryBehaviour = "primary",
                        LanguageSource = "custom",
                    },
                });
                var multinodeProductsDt = EnsureDataTypeExists(new DataType(multiNodeEditor, ekmDtContainer.Id)
                {
                    Name = "Ekom - Products - Multinode Tree Picker",
                    Configuration = new MultiNodePickerConfiguration
                    {
                    }
                });
                #region Grid Configuration
                var productsGridDt = EnsureDataTypeExists(new DataType(gridEditor, ekmDtContainer.Id)
                {
                    Name = "Ekom - Products Grid Editor",
                    Configuration = new GridConfiguration
                    {
                        Items = new JObject
                        {
                            { "columns", 12 },
                            { "templates", new JArray
                                {
                                   new JObject {
                                       { "name", "Content" },
                                       { "sections", new JArray
                                           {
                                                new JObject
                                                {
                                                    { "grid", 12 },
                                                }
                                           }
                                       }
                                    },
                                }
                            },
                            { "layouts", new JArray
                                {
                                    new JObject
                                    {
                                        { "name", "Expanded" },
                                        { "areas", new JArray
                                            {
                                                new JObject
                                                {
                                                    { "grid", 12 }
                                                }
                                            }
                                        },
                                        { "label", "Expanded" },
                                    },
                                    new JObject
                                    {
                                        { "name", "Container" },
                                        { "areas", new JArray
                                            {
                                                new JObject
                                                {
                                                    { "grid", 12 }
                                                }
                                            }
                                        },
                                        { "label", "Container" },
                                    },
                                    new JObject
                                    {
                                        { "name", "8-Columns" },
                                        { "areas", new JArray
                                            {
                                                new JObject
                                                {
                                                    { "grid", 8 }
                                                }
                                            }
                                        },
                                    },
                                    new JObject
                                    {
                                        { "name", "6+6 Columns" },
                                        { "areas", new JArray
                                            {
                                                new JObject
                                                {
                                                    { "grid", 6 },
                                                },
                                                new JObject
                                                {
                                                    { "grid", 6 },
                                                },
                                            }
                                        },
                                    },
                                }
                            }
                        },
                        Rte = new JObject
                        {
                            { "toolbar", new JArray
                                {
                                    "code", "styleselect", "bold", "italic", "alignleft", "aligncenter", "alignright", "bullist", "numlist", "outdent", "indent", "link", "umbmediapicker", "umbmacro", "umbembeddialog",
                                }
                            },
                            { "stylesheets", new JArray
                                {
                                    "umbraco", "Rte",
                                }
                            },
                            { "dimensions", new JObject
                                {
                                    { "height", 500 }
                                }
                            },
                            { "maxImageSize", 500 },
                        },
                    }
                });
                #endregion
                var perStoreProductGridDt = EnsureDataTypeExists(new DataType(editor, ekmDtContainer.Id)
                {
                    Name = "Ekom - Products Grid Editor - Per Store",
                    Configuration = new VortoConfiguration
                    {
                        DataType = new DataTypeInfo
                        {
                            Guid = productsGridDt.Key,
                            Name = productsGridDt.Name,
                            PropertyEditorAlias = productsGridDt.EditorAlias,
                        },
                        MandatoryBehaviour = "primary",
                        LanguageSource = "custom",
                    },
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
                                        new PropertyType(perStoreTextDt, "description")
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
                                        new PropertyType(perStoreIntDt, "price")
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

                        PropertyGroups = new PropertyGroupCollection(
                            new List<PropertyGroup>
                            {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreIntDt, "startOfRange")
                                        {
                                            Name = "Start of Range",
                                        },
                                        new PropertyType(perStoreIntDt, "endOfRange")
                                        {
                                            Name = "End of Range",
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
                                        new PropertyType(perStoreTextDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                        new PropertyType(textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                        },
                                        new PropertyType(_configuration.PerStoreStock ? perStoreStockDt : stockDt, "stock")
                                        {
                                            Name = "Stock",
                                        },
                                    }))
                                {
                                    Name = "Variant",
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                    }))
                                {
                                    Name = "Details",
                                },
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
                        AllowedContentTypes = new List<ContentTypeSort>
                        {
                            new ContentTypeSort(productVariantCt.Id, 1),
                            new ContentTypeSort(productVariantGroupCt.Id, 2),
                        },
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
                                        new PropertyType(textstringDt, "sku")
                                        {
                                            Name = "SKU",
                                        },
                                        new PropertyType(textstringDt, "barcode")
                                        {
                                            Name = "Barcode",
                                        },
                                        new PropertyType(perStoreTextDt, "underTitle")
                                        {
                                            Name = "Under title",
                                        },
                                        new PropertyType(multipleMediaPickerDt, "images")
                                        {
                                            Name = "Images",
                                        },
                                        new PropertyType(perStoreTextDt, "slug")
                                        {
                                            Name = "Slug",
                                        },
                                        new PropertyType(perStoreTextDt, "price")
                                        {
                                            Name = "Price",
                                        },
                                        new PropertyType(_configuration.PerStoreStock ? perStoreStockDt : stockDt, "stock")
                                        {
                                            Name = "Stock",
                                        },
                                        new PropertyType(textstringDt, "amount")
                                        {
                                            Name = "Amount",
                                        },
                                        new PropertyType(contentPickerDt, "categories")
                                        {
                                            Name = "Product Categories",
                                            Description = "Allows a product to belong to categories other than it's umbraco node parent categories. A single product node can therefore belong to multiple logical category tree hierarchies.",
                                        },
                                        new PropertyType(multinodeProductsDt, "relatedProducts")
                                        {
                                            Name = "Related products",
                                        },
                                    }))
                                {
                                    Name = "Product",
                                },
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(perStoreMediaPickerDt, "coverImage")
                                        {
                                            Name = "Cover Image",
                                        },
                                        new PropertyType(perStoreTextDt, "coverHeader")
                                        {
                                            Name = "Cover Header",
                                        },
                                        new PropertyType(perStoreTextDt, "coverSubtitle")
                                        {
                                            Name = "Cover Subtitle",
                                        },
                                        new PropertyType(perStoreProductGridDt, "content")
                                        {
                                            Name = "Content",
                                        },
                                    }))
                                {
                                    Name = "Cover",
                                },
                            }),
                    }
                );

                var categoryCt = EnsureContentTypeExists(new ContentType(catalogContainer.Id)
                {
                    Name = "Category",
                    Alias = "ekmCategory",
                    AllowedContentTypes = new List<ContentTypeSort> { },
                    Icon = "icon-folder",
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
                                        new PropertyType(perStoreTextDt, "header")
                                        {
                                            Name = "Header",
                                        },
                                        new PropertyType(perStoreTextDt, "Subheader")
                                        {
                                            Name = "subheader",
                                        },
                                        new PropertyType(perStoreTextDt, "description")
                                        {
                                            Name = "Description",
                                        },
                                        new PropertyType(mediaPickerDt, "categoryImage")
                                        {
                                            Name = "Category image",
                                        },
                                        new PropertyType(mediaPickerDt, "menuImage")
                                        {
                                            Name = "Menu image",
                                        },
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
                categoryCt.AllowedContentTypes 
                    = categoryCt.AllowedContentTypes.Append(
                        new ContentTypeSort(categoryCt.Id, 1)
                    );
                categoryCt.AllowedContentTypes 
                    = categoryCt.AllowedContentTypes.Append(
                        new ContentTypeSort(productCt.Id, 2)
                    );
                _contentTypeService.Save(categoryCt);

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

                var couponCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                {
                    Name = "Coupon",
                    Alias = "ekmCoupon",
                    Icon = "icon-coin-euro",
                    PropertyGroups = new PropertyGroupCollection(
                        new List<PropertyGroup>
                            {
                                new PropertyGroup(new PropertyTypeCollection(
                                    true,
                                    new List<PropertyType>
                                    {
                                        new PropertyType(numericDt, "count")
                                        {
                                            Name = "Count",
                                        },
                                    }))
                                {
                                    Name = "Settings",
                                },
                            }
                    ),
                });

                var discountCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                {
                    Name = "Discount",
                    Alias = "ekmDiscount",
                    Icon = "icon-bill-euro",
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
                                        // ToDo: Finish when consensus is reached
                                        new PropertyType(numericDt, "type")
                                        {
                                            Name = "Type",
                                        },
                                        new PropertyType(numericDt, "discount")
                                        {
                                            Name = "Discount",
                                        },
                                    }))
                                {
                                    Name = "Settings",
                                },
                            }
                    ),
                });

                var discountsCt = EnsureContentTypeExists(new ContentType(discountsContainer.Id)
                {
                    Name = "Discounts",
                    Alias = "ekmDiscounts",
                    Icon = "icon-bills-euro",
                    AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(discountCt.Id, 1),
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
                                        Name = "Alias",
                                        Description = "Name of the DLL",
                                    },
                                    new PropertyType(numericDt, "discount")
                                    {
                                        Name = "Discount",
                                    },
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
                                    new PropertyType(textstringDt, "currency")
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

                // Content creation disabled until Umbraco patch
                // https://github.com/umbraco/Umbraco-CMS/issues/5281

                //var ekom = EnsureContentExists("Ekom", "ekom");
                //var catalog = EnsureContentExists("Catalog", "ekmCatalog", ekom.Id);
                //EnsureContentExists("Shipping Providers", "ekmShippingProviders", ekom.Id);
                //EnsureContentExists("Payment Providers", "netPaymentProviders", ekom.Id);
                //EnsureContentExists("Discounts", "ekmDiscounts", ekom.Id);
                //EnsureContentExists("Stores", "ekmStores", ekom.Id);
                //EnsureContentExists("Zones", "ekmZones", ekom.Id);

                //var multiNodeProductsPickerConfiguration = (multinodeProductsDt.Configuration as MultiNodePickerConfiguration);
                //if (multiNodeProductsPickerConfiguration.TreeSource?.StartNodeId == null)
                //{
                //    multiNodeProductsPickerConfiguration.TreeSource = new MultiNodePickerConfigurationTreeSource
                //    {
                //        ObjectType = "content",
                //        StartNodeId = new GuidUdi("document", catalog.Key),
                //    };
                //    _dataTypeService.Save(multinodeProductsDt);
                //}

                #endregion
            }

            _logger.Debug<EnsureNodesExist>("Done");
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
                _logger.Info<EnsureNodesExist>($"Created Data Type {dt.Name}, editor alias {dt.EditorAlias}");
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
                    _logger.Info<EnsureNodesExist>($"Created doc type container {name}");
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
                _logger.Info<EnsureNodesExist>($"Created content type {contentType.Name}, alias {contentType.Alias}");
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
                _logger.Info<EnsureNodesExist>($"Created content {name}, alias {documentTypeAlias}");
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
