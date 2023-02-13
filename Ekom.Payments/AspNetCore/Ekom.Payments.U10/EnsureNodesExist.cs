using System;
using System.Collections.Generic;
using System.Linq;
using Ekom.Payments.Exceptions;

#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace Ekom.Payments.App_Start;
#pragma warning restore CA1707 // Identifiers should not contain underscores

//class EnsureNodesExist : IComponent
//{
//    const string _indexName = "ExternalIndex";
//    public const string paymentProviderAlias = "netPaymentProvider";

//    private readonly ILogger _logger;
//    private readonly Settings _settings;
//    private readonly IContentService _contentService;
//    private readonly IContentTypeService _contentTypeService;
//    private readonly IDataTypeService _dataTypeService;
//    private readonly PropertyEditorCollection _propertyEditorCollection;
//    private readonly IExamineManager _examineMgr;
//    private readonly IUmbracoContextFactory _contextFactory;

//    public EnsureNodesExist(
//        ILogger logger,
//        IContentService contentService,
//        IContentTypeService contentTypeService,
//        IDataTypeService dataTypeService,
//        PropertyEditorCollection propertyEditorCollection,
//        IExamineManager examineMgr,
//        IUmbracoContextFactory contextFactory,
//        Settings settings)
//    {
//        _logger = logger;
//        _contentService = contentService;
//        _contentTypeService = contentTypeService;
//        _dataTypeService = dataTypeService;
//        _propertyEditorCollection = propertyEditorCollection;
//        _examineMgr = examineMgr;
//        _contextFactory = contextFactory;
//        _settings = settings;
//    }

//    public void Initialize()
//    {
//        _logger.Debug<EnsureNodesExist>("Ensuring Umbraco nodes exist");

//        try
//        {
//            if (IsEkomProject())
//            {
//                _logger.Debug<EnsureNodesExist>("Is Ekom project, will use Ekom nodes");
//                return;
//            }

//            if (_examineMgr.TryGetIndex(_indexName, out IIndex index))
//            {
//                var searcher = index.GetSearcher();
//                var results = searcher.CreateQuery("content")
//                    .NodeTypeAlias(_settings.PPDocumentTypeAlias)
//                    .Execute()
//                    ;

//                // Assume ready if we find a netPaymentProviders content node
//                if (results.Any())
//                {
//                    return;
//                }
//            }
//            else
//            {
//                _logger.Error<EnsureNodesExist>($"Unable to find Examine index: {_indexName}, could not begin NetPayment nodes creation");
//                // If we can't find the externalSearcher, assume user has other fixing to do before worrying about NetPayment
//                return;
//            }

//            if (!_propertyEditorCollection.TryGet("Our.Umbraco.Vorto", out IDataEditor vortoEditor))
//            {
//                throw new EnsureNodesException(
//                    "Unable to find Our.Umbraco.Vorto property editor, failed creating NetPayment nodes. Ensure GMO.Vorto.Web is installed.");
//            }

//            #region Data Types

//            var netPaymentDtContainer = EnsureDataTypeContainerExists();

//            var textstringDt = _dataTypeService.GetDataType(new Guid("0cc0eba1-9960-42c9-bf9b-60e150b429ae"));
//            var textareaDt = _dataTypeService.GetDataType(new Guid("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3"));

//            var perStoreTextDt = EnsureDataTypeExists(new DataType(vortoEditor, netPaymentDtContainer.Id)
//            {
//                Name = "NetPayment - Textstring - Vorto",
//                Configuration = new VortoConfiguration
//                {
//                    DataType = new DataTypeInfo
//                    {
//                        Guid = textstringDt.Key,
//                        Name = textstringDt.Name,
//                        PropertyEditorAlias = textstringDt.EditorAlias,
//                    },
//                    MandatoryBehaviour = "primary",
//                },
//            });
//            var perStoreTextareaDt = EnsureDataTypeExists(new DataType(vortoEditor, netPaymentDtContainer.Id)
//            {
//                Name = "NetPayment - Textarea - Vorto",
//                Configuration = new VortoConfiguration
//                {
//                    DataType = new DataTypeInfo
//                    {
//                        Guid = textareaDt.Key,
//                        Name = textareaDt.Name,
//                        PropertyEditorAlias = textareaDt.EditorAlias,
//                    },
//                    MandatoryBehaviour = "primary",
//                },
//            });

//            #endregion

//            #region Document Types

//            var netPaymentDocTypeContainer = EnsureContainerExists("NetPayment");

//            var paymentProviderComposition = EnsureContentTypeExists(
//                new ContentType(netPaymentDocTypeContainer.Id)
//                {
//                    Name = "Payment Provider",
//                    Alias = paymentProviderAlias,

//                    PropertyGroups = new PropertyGroupCollection(
//                        new List<PropertyGroup>
//                        {
//                            new PropertyGroup(new PropertyTypeCollection(
//                                true,
//                                new List<PropertyType>
//                                {
//                                    new PropertyType(perStoreTextDt, "title")
//                                    {
//                                        Name = "Title",
//                                        Mandatory = true,
//                                    },
//                                    new PropertyType(perStoreTextDt, "successUrl")
//                                    {
//                                        Name = "Success Url",
//                                        Mandatory = true,
//                                        Description = "Relative url, f.x. /success",
//                                    },
//                                    new PropertyType(perStoreTextDt, "cancelUrl")
//                                    {
//                                        Name = "Cancel Url",
//                                        Description = "Required for Borgun",
//                                    },
//                                    new PropertyType(perStoreTextDt, "errorUrl")
//                                    {
//                                        Name = "Error Url",
//                                        Mandatory = true,
//                                    },
//                                    new PropertyType(perStoreTextareaDt, "paymentInfo")
//                                    {
//                                        Name = "Payment Info",
//                                    },
//                                    new PropertyType(textstringDt, "basePaymentProvider")
//                                    {
//                                        Name = "Base Payment Provider",
//                                        Description = "Allows payment provider overloading. " +
//                                            "F.x. Borgun ISK and Borgun USD nodes with different names and different xml configurations targetting the same base payment provider."
//                                    },
//                                }))
//                            {
//                                Name = "Settings",
//                            },
//                            new PropertyGroup(new PropertyTypeCollection(
//                                true,
//                                new List<PropertyType>
//                                {
//                                    new PropertyType(textstringDt, "imageUrl")
//                                    {
//                                        Name = "Image Url",
//                                    },
//                                }))
//                            {
//                                Name = "Image",
//                            },
//                        }),
//                }
//            );

//            var netPaymentProvidersCt = EnsureContentTypeExists(new ContentType(netPaymentDocTypeContainer.Id)
//            {
//                Name = "Payment Providers",
//                Alias = _settings.PPDocumentTypeAlias,
//                Icon = "icon-bills",
//                AllowedAsRoot = true,
//            });

//            #endregion

//            EnsureContentExists("Greiðslugáttir", netPaymentProvidersCt.Alias);
//        }
//#pragma warning disable CA1031 // Should not kill startup
//        catch (Exception ex)
//#pragma warning restore CA1031 // Do not catch general exception types
//        {
//            _logger.Error<EnsureNodesExist>(ex);
//        }

//        _logger.Debug<EnsureNodesExist>("Done");
//    }

//    private IContent EnsureContentExists(string _name, string documentTypeAlias, int parentId = -1)
//    {
//        return null;
//        // ToDo: check for existence if we ever end up creating more content nodes

//        //var content = _contentService.Create(name, parentId, documentTypeAlias);

//        //PublishResult res;
//        //using (_contextFactory.EnsureUmbracoContext())
//        //{
//        //    res = _contentService.SaveAndPublish(content);
//        //}

//        //if (res.Success)
//        //{
//        //    _logger.Info<EnsureNodesExist>(
//        //        "Created content {Name}, alias {DocumentTypeAlias}",
//        //        name,
//        //        documentTypeAlias);

//        //    return content;
//        //}
//        //else
//        //{
//        //    throw new EnsureNodesException($"Unable to SaveAndPublish {name} content with doc type {documentTypeAlias} and parent {parentId}");
//        //}
//    }

//    private EntityContainer EnsureDataTypeContainerExists()
//    {
//        var container = _dataTypeService.GetContainers("NetPayment", 1).FirstOrDefault();
//        if (container == null)
//        {
//            var createContainerAttempt = _dataTypeService.CreateContainer(-1, "NetPayment");
//            if (createContainerAttempt.Success)
//            {
//                _logger.Info<EnsureNodesExist>("Created DataType container");
//                container = createContainerAttempt.Result.Entity;
//            }
//            else
//            {
//                throw new EnsureNodesException("Unable to create container, failed creating NetPayment Data Types", createContainerAttempt.Exception);
//            }
//        }

//        return container;
//    }

//    private IDataType EnsureDataTypeExists(DataType dt)
//    {
//        var textDt = _dataTypeService.GetDataType(dt.Name);

//        if (textDt == null)
//        {
//            textDt = dt;
//            _dataTypeService.Save(textDt);
//            _logger.Info<EnsureNodesExist>(
//                "Created Data Type {Name}, editor alias {EditorAlias}",
//                dt.Name,
//                dt.EditorAlias
//            );
//        }

//        return textDt;
//    }

//    private EntityContainer EnsureContainerExists(string name, int level = 1, int parentId = -1)
//    {
//        var container = _contentTypeService.GetContainers(name, level).FirstOrDefault(x => x.ParentId == parentId);
//        if (container == null)
//        {
//            var createContainerAttempt = _contentTypeService.CreateContainer(parentId, name);
//            if (createContainerAttempt.Success)
//            {
//                container = createContainerAttempt.Result.Entity;
//                _logger.Info<EnsureNodesExist>("Created doc type container {Name}", name);
//            }
//            else
//            {
//                throw new EnsureNodesException("Unable to create container, failed creating NetPayment nodes", createContainerAttempt.Exception);
//            }
//        }

//        return container;
//    }

//    private IContentType EnsureContentTypeExists(ContentType contentType)
//    {
//        var netPaymentContentType = _contentTypeService.Get(contentType.Alias);

//        if (netPaymentContentType == null)
//        {
//            netPaymentContentType = contentType;
//            _contentTypeService.Save(netPaymentContentType);
//            _logger.Info<EnsureNodesExist>(
//                "Created content type {Name}, alias {Alias}",
//                contentType.Name,
//                contentType.Alias);
//        }

//        return netPaymentContentType;
//    }

//    private bool IsEkomProject()
//    {
//        return AppDomain.CurrentDomain.GetAssemblies()
//            .Any(x => x.GetName().Name == "Ekom");
//    }

//    public void Terminate() { }
//}
