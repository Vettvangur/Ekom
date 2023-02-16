//namespace Ekom.Payments
//{
//    abstract class EnsurePaymentProviderNodesExist : IComponent
//    {
//        // We don't publish node, use InternalIndex if using Examine
//        //const string _indexName = "InternalIndex";

//        private readonly ILogger _logger;
//        private readonly PaymentsConfiguration _settings;
//        private readonly IContentService _contentService;
//        private readonly IContentTypeService _contentTypeService;
//        private readonly IExamineManager _examineMgr;
//        private readonly IUmbracoContextFactory _contextFactory;
//        private readonly IScopeProvider _scopeProvider;

//        private readonly string _nodeName;
//        private readonly string _aliasName;

//        /// <summary>
//        /// Override to customise payment providers
//        /// </summary>
//        protected virtual PropertyGroupCollection PropertyGroupCollection => new PropertyGroupCollection(
//            new List<PropertyGroup>
//            {
//            });

//        protected EnsurePaymentProviderNodesExist(
//            ILogger logger,
//            PaymentsConfiguration settings,
//            IContentService contentService,
//            IContentTypeService contentTypeService,
//            IExamineManager examineMgr,
//            IUmbracoContextFactory contextFactory,
//            IScopeProvider scopeProvider,
//            string nodeName,
//            string aliasNameSuffix
//        )
//        {
//            _logger = logger;
//            _settings = settings;
//            _contentService = contentService;
//            _contentTypeService = contentTypeService;
//            _examineMgr = examineMgr;
//            _contextFactory = contextFactory;
//            _scopeProvider = scopeProvider;

//            _nodeName = nodeName;
//            // Add prefix to alias
//            _aliasName = PaymentsConfiguration.ProviderDocTypeAliasPrefix + aliasNameSuffix;
//        }

//        public void Initialize()
//        {
//            _logger.Debug<EnsurePaymentProviderNodesExist>("Ensuring {Name} node exists", _nodeName);

//            try
//            {
//                var noTrashedQuery = new Query<IContent>(_scopeProvider.SqlContext)
//                    .Where(x => !x.Trashed);

//                var ppDocType = _contentTypeService.Get(_aliasName);
//                if (ppDocType != null)
//                {

//                    var results = _contentService.GetPagedOfType(
//                        ppDocType.Id,
//                        0,
//                        10,
//                        out _,
//                        noTrashedQuery);

//                    // Assume ready if we find matching content node
//                    if (results.Any())
//                    {
//                        return;
//                    }
//                }

//                #region Document Types

//                var container = _contentTypeService.GetContainers("NetPayment", 1).FirstOrDefault()
//                    // Ekom
//                    ?? _contentTypeService.GetContainers("Payment Providers", 2).FirstOrDefault();

//                if (container == null)
//                {
//                    _logger.Error<EnsurePaymentProviderNodesExist>("Unable to create {Name} payment provider node, container type missing", _nodeName);
//                    return;
//                }

//                var ppContentType = _contentTypeService.Get(EkomPayments.App_Start.EnsureNodesExist.paymentProviderAlias);
//                if (ppContentType == null)
//                {
//                    _logger.Error<EnsurePaymentProviderNodesExist>("Unable to create {Name} payment provider node, composition missing", _nodeName);
//                    return;
//                }

//                var paymentProvider = EnsureContentTypeExists(
//                    new ContentType(container.Id)
//                    {
//                        Name = _nodeName,
//                        Alias = _aliasName,
//                        Icon = "icon-bill-euro",
//                        ContentTypeComposition = new List<IContentTypeComposition>
//                        {
//                            ppContentType,
//                        },
//                        PropertyGroups = PropertyGroupCollection,
//                    }
//                );

//                var netPaymentContentType = _contentTypeService.Get(_settings.ContainerDocumentTypeAlias);
//                if (!netPaymentContentType.AllowedContentTypes.Any(x => x.Id.Value == paymentProvider.Id))
//                {
//                    var allowedContentTypes = new List<ContentTypeSort>
//                    {
//                        new ContentTypeSort(paymentProvider.Id, 1),
//                    };
//                    allowedContentTypes.AddRange(netPaymentContentType.AllowedContentTypes);
//                    netPaymentContentType.AllowedContentTypes = allowedContentTypes;
//                    _contentTypeService.Save(netPaymentContentType);
//                }

//                #endregion

//                var netPaymentNode
//                    = _contentService.GetPagedOfType(netPaymentContentType.Id, 0, 1, out _, noTrashedQuery)
//                    .FirstOrDefault();

//                if (netPaymentNode == null)
//                {
//                    _logger.Error<EnsurePaymentProviderNodesExist>(
//                        "Unable to create {Name} payment provider node, container node missing", 
//                        _nodeName);
//                    return;
//                }

//                EnsureContentExists(_nodeName, paymentProvider.Alias, netPaymentNode.Id);
//            }
//#pragma warning disable CA1031 // Should not kill startup
//            catch (Exception ex)
//#pragma warning restore CA1031 // Do not catch general exception types
//            {
//                _logger.Error<EnsurePaymentProviderNodesExist>(ex);
//            }

//            _logger.Debug<EnsurePaymentProviderNodesExist>("Done");
//        }

//        private IContent EnsureContentExists(string name, string documentTypeAlias, int parentId = -1)
//        {
//            // ToDo: check for existence if we ever end up creating more content nodes

//            var content = _contentService.Create(name, parentId, documentTypeAlias);

//            OperationResult res;
//            using (_contextFactory.EnsureUmbracoContext())
//            {
//                res = _contentService.Save(content);
//            }

//            if (res.Success)
//            {
//                _logger.Info<EnsurePaymentProviderNodesExist>(
//                    "Created content {Name}, alias {DocumentTypeAlias}",
//                    name,
//                    documentTypeAlias);

//                return content;
//            }
//            else
//            {
//                throw new EnsureNodesException($"Unable to Save {name} content with doc type {documentTypeAlias} and parent {parentId}");
//            }
//        }

//        private IContentType EnsureContentTypeExists(ContentType contentType)
//        {
//            var netPaymentContentType = _contentTypeService.Get(contentType.Alias);

//            if (netPaymentContentType == null)
//            {
//                netPaymentContentType = contentType;
//                _contentTypeService.Save(netPaymentContentType);
//                _logger.Info<EnsurePaymentProviderNodesExist>(
//                    "Created content type {Name}, alias {Alias}",
//                    contentType.Name,
//                    contentType.Alias);
//            }

//            return netPaymentContentType;
//        }

//        public void Terminate() { }
//    }
//}
