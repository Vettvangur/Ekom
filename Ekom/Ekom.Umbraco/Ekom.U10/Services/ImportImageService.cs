using Ekom.Models.Import;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using MediaTypes = Umbraco.Cms.Core.Constants.Conventions.MediaTypes;

namespace Ekom.Umb.Services;
public class ImportMediaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaService _mediaService;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly MediaFileManager _mediaFileManager;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly ILogger<ImportMediaService> _logger;
    private readonly IScopeProvider _scopeProvider;

    private int mediaCount = 0;
    private int mediaFolderPageSize = 400;
    private IMedia? lastMediaFolder;
    private IMedia? rootMediaFolder;

    public ImportMediaService(
        IMediaService mediaService,
        IHttpClientFactory httpClientFactory,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        ILogger<ImportMediaService> logger,
        IScopeProvider scopeProvider)
    {
        _mediaService = mediaService;
        _httpClientFactory = httpClientFactory;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _logger = logger;
        _scopeProvider = scopeProvider;
    }

    public IMedia GetRootMedia(Guid rootMediaKey)
    {
        var media = _mediaService.GetById(rootMediaKey);

        if (media == null)
        {
            throw new ArgumentException($"No media exists with key '{rootMediaKey}'.", nameof(rootMediaKey));
        }

        if (media.Trashed)
        {
            throw new InvalidOperationException($"Media '{rootMediaKey}' is in the recycle bin.");
        }

        if (media.ContentType.Alias != MediaTypes.Folder)
        {
            throw new InvalidOperationException($"Media '{rootMediaKey}' is not a media folder.");
        }

        rootMediaFolder = media;

        GetRootMediaLastChildrenFolder(rootMediaFolder);

        return rootMediaFolder;
    }


    private void GetRootMediaLastChildrenFolder(IMedia rootMedia)
    { 
       
        var mediaFolders = GetRootMediaChildren(rootMedia);

        lastMediaFolder = mediaFolders.LastOrDefault();

        if (lastMediaFolder == null)
        {
            lastMediaFolder = CreateMediaFolder("1");
        }

        var mediaItems = _mediaService.GetPagedChildren(lastMediaFolder.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && (x.ContentType.Alias == MediaTypes.Image || x.ContentType.Alias == MediaTypes.File)).ToList();

        mediaCount = mediaItems.Count;
    }

    public List<IMedia> GetRootMediaChildren(IMedia rootMedia)
    {
        var mediaFolders = _mediaService.GetPagedChildren(rootMedia.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == MediaTypes.Folder).ToList();

        return mediaFolders;
    }

    public List<IMedia> GetUmbracoMediaFiles(IMedia rootMedia)
    {
        if (rootMedia == null)
            return new List<IMedia>();

        const int pageSize = 1000;
        var results = new List<IMedia>();
        long total;
        var pageIndex = 0;

        do
        {
            var page = _mediaService.GetPagedDescendants(
                rootMedia.Id,
                pageIndex,
                pageSize,
                out total).ToList();

            results.AddRange(page.Where(x =>
                !x.Trashed &&
                (x.ContentType.Alias == Constants.Conventions.MediaTypes.Image ||
                 x.ContentType.Alias == Constants.Conventions.MediaTypes.File)));

            pageIndex++;
        }
        while (pageIndex * pageSize < total);

        return results;
    }

    public List<IMedia> GetUmbracoMediaFiles(
        IMedia rootMedia,
        IReadOnlyCollection<string> identifiers,
        IReadOnlyCollection<string> comparers,
        IReadOnlyCollection<Guid> keys)
    {
        if (identifiers.Count == 0 && comparers.Count == 0 && keys.Count == 0)
        {
            return new List<IMedia>();
        }

        var conditions = new List<string>();
        var parameters = new List<object> { $"%,{rootMedia.Id},%", MediaTypes.Image, MediaTypes.File };

        if (keys.Count > 0)
        {
            conditions.Add($"n.uniqueId IN (@{parameters.Count})");
            parameters.Add(keys);
        }

        if (identifiers.Count > 0)
        {
            conditions.Add($"(pt.alias = 'ekmIdentifier' AND pd.varcharValue IN (@{parameters.Count}))");
            parameters.Add(identifiers);
        }

        if (comparers.Count > 0)
        {
            conditions.Add($"(pt.alias = 'comparer' AND pd.varcharValue IN (@{parameters.Count}))");
            parameters.Add(comparers);
        }

        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var mediaIds = scope.Database.Fetch<int>(
            $@"SELECT DISTINCT n.id
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
INNER JOIN umbracoContentVersion cv ON cv.nodeId = n.id AND cv.[current] = 1
LEFT JOIN umbracoPropertyData pd ON pd.versionId = cv.id
LEFT JOIN cmsPropertyType pt ON pt.id = pd.propertyTypeId
WHERE n.trashed = 0
  AND n.path LIKE @0
  AND ct.alias IN (@1, @2)
  AND ({string.Join(" OR ", conditions)})
ORDER BY n.id",
            parameters.ToArray());

        var matchingMedia = _mediaService.GetPagedDescendants(
                rootMedia.Id,
                0,
                int.MaxValue,
                out _,
                new Query<IMedia>(_scopeProvider.SqlContext).Where(media => mediaIds.Contains(media.Id)))
            .Where(media => !media.Trashed
                && (media.ContentType.Alias == MediaTypes.Image || media.ContentType.Alias == MediaTypes.File))
            .ToDictionary(media => media.Id);

        return mediaIds
            .Where(matchingMedia.ContainsKey)
            .Select(mediaId => matchingMedia[mediaId])
            .ToList();
    }

    public IMedia? ImportMediaFromExternalUrl(ImportMediaFromExternalUrl image, string comparer, ImportMediaTypes mediaType, string? identifier, int? syncUser = -1)
    {
        var stream = LoadMediaToMemoryStreamAsync(image.Url).Result;

        if (stream == null)
        {
            return null;
        }

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier, syncUser);
    }

    public IMedia ImportMediaFromBytes(ImportMediaFromBytes image, string comparer, ImportMediaTypes mediaType, string? identifier, int? syncUser = -1)
    {
        var stream = new MemoryStream(image.Bytes);
        stream.Seek(0, SeekOrigin.Begin);

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier, syncUser);
    }

    public IMedia ImportMediaFromBase64(ImportMediaFromBase64 image, string comparer, ImportMediaTypes mediaType, string? identifier, int? syncUser = -1)
    {        
        // Convert Base64 String to byte[]
        byte[] bytes = Convert.FromBase64String(image.Base64);

        // Create a MemoryStream with the bytes
        var stream = new MemoryStream(bytes);

        stream.Seek(0, SeekOrigin.Begin);

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier, syncUser);
    }

    public IMedia UpdateMediaSortOrder(IMedia media, IImportMedia importMedia)
    {
        return UpdateSortOrderMedia(media, importMedia.SortOrder);
    }

    private async Task<MemoryStream?> LoadMediaToMemoryStreamAsync(string url)
    {
        try
        {
            // Create a new HttpClient using the factory
            var httpClient = _httpClientFactory.CreateClient();

            // Send a GET request to the image URL
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode(); // Throws if not successful

            // Read the response content as byte array
            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            // Load the byte array into a memory stream
            var memoryStream = new MemoryStream(imageBytes);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading media to stream: {ex.Message} Url: {url}");
            return null;
        }
    }

    private IMedia CreateMedia(MemoryStream mem, string comparer, string nodeName, string fullFileName, Ekom.Models.Import.ImportMediaTypes mediaType, int? sortOrder, string? identifier, int? syncUser)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeName);
        ArgumentException.ThrowIfNullOrEmpty(fullFileName);

        var media = _mediaService.CreateMedia(nodeName, lastMediaFolder.Id, mediaType.ToString(), userId: syncUser.HasValue ? syncUser.Value : -1);
        media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, fullFileName, mem);
        media.SetValue("comparer", comparer);

        if (media.HasProperty("ekmSortOrder") && sortOrder.HasValue)
        {
            media.SetValue("ekmSortOrder", sortOrder.Value);
        }

        if (media.HasProperty("ekmIdentifier") && !string.IsNullOrEmpty(identifier))
        {
            media.SetValue("ekmIdentifier", identifier);
        }

        _mediaService.Save(media);

        mediaCount++;

        if (mediaCount >= mediaFolderPageSize)
        {
            var currentNodeName = lastMediaFolder.Name;

            int.TryParse(currentNodeName, out int newName);

            newName++;

            lastMediaFolder = CreateMediaFolder(newName.ToString());

            mediaCount = 0;
        }

        return media;
    }

    private IMedia UpdateSortOrderMedia(IMedia media, int? sortOrder)
    {

        if (media.HasProperty("ekmSortOrder") && sortOrder.HasValue && media.GetValue<int>("ekmSortOrder") != sortOrder.Value)
        {
            media.SetValue("ekmSortOrder", sortOrder.Value);
            _mediaService.Save(media);
        }

        return media;
    }

    private IMedia CreateMediaFolder(string nodeName)
    {
        var media = _mediaService.CreateMediaWithIdentity(nodeName, rootMediaFolder.Id, Constants.Conventions.MediaTypes.Folder);
        _mediaService.Save(media);

        return media;
    }
}
