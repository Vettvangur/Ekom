using Ekom.Models.Import;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
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

    private readonly int _mediaFolderPageSize = 400;
    private int _mediaCount;
    private IMedia? _lastMediaFolder;
    private IMedia? _rootMediaFolder;

    public ImportMediaService(
        IMediaService mediaService,
        IHttpClientFactory httpClientFactory,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        ILogger<ImportMediaService> logger)
    {
        _mediaService = mediaService;
        _httpClientFactory = httpClientFactory;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _logger = logger;
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

        _rootMediaFolder = media;

        GetRootMediaLastChildrenFolder(_rootMediaFolder);

        return _rootMediaFolder;
    }

    public List<IMedia> GetRootMediaChildren(IMedia rootMedia)
    {
        return _mediaService.GetPagedChildren(rootMedia.Id, 0, int.MaxValue, out _)
            .Where(x => !x.Trashed && x.ContentType.Alias == MediaTypes.Folder)
            .ToList();
    }

    public List<IMedia> GetUmbracoMediaFiles(IMedia rootMedia)
    {
        const int pageSize = 1000;
        var results = new List<IMedia>();
        var pageIndex = 0;

        do
        {
            var page = _mediaService.GetPagedDescendants(rootMedia.Id, pageIndex, pageSize, out var total).ToList();

            results.AddRange(page.Where(x =>
                !x.Trashed
                && (x.ContentType.Alias == Constants.Conventions.MediaTypes.Image
                    || x.ContentType.Alias == Constants.Conventions.MediaTypes.File)));

            pageIndex++;

            if (pageIndex * pageSize >= total)
            {
                break;
            }
        }
        while (true);

        return results;
    }

    public IMedia? ImportMediaFromExternalUrl(
        ImportMediaFromExternalUrl image,
        string comparer,
        ImportMediaTypes mediaType,
        string? identifier,
        int? syncUser = -1)
    {
        var stream = LoadMediaToMemoryStreamAsync(image.Url).GetAwaiter().GetResult();

        return stream == null
            ? null
            : CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier, syncUser);
    }

    public IMedia ImportMediaFromBytes(
        ImportMediaFromBytes image,
        string comparer,
        ImportMediaTypes mediaType,
        string? identifier,
        int? syncUser = -1)
    {
        var stream = new MemoryStream(image.Bytes);
        stream.Seek(0, SeekOrigin.Begin);

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier, syncUser);
    }

    public IMedia ImportMediaFromBase64(
        ImportMediaFromBase64 image,
        string comparer,
        ImportMediaTypes mediaType,
        string? identifier,
        int? syncUser = -1)
    {
        var bytes = Convert.FromBase64String(image.Base64);
        var stream = new MemoryStream(bytes);
        stream.Seek(0, SeekOrigin.Begin);

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier, syncUser);
    }

    public IMedia UpdateMediaSortOrder(IMedia media, IImportMedia importMedia)
    {
        return UpdateSortOrderMedia(media, importMedia.SortOrder);
    }

    private void GetRootMediaLastChildrenFolder(IMedia rootMedia)
    {
        var mediaFolders = GetRootMediaChildren(rootMedia);
        _lastMediaFolder = mediaFolders.LastOrDefault() ?? CreateMediaFolder("1");

        var mediaItems = _mediaService.GetPagedChildren(_lastMediaFolder.Id, 0, int.MaxValue, out _)
            .Where(x => !x.Trashed && (x.ContentType.Alias == MediaTypes.Image || x.ContentType.Alias == MediaTypes.File))
            .ToList();

        _mediaCount = mediaItems.Count;
    }

    private async Task<MemoryStream?> LoadMediaToMemoryStreamAsync(string url)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var memoryStream = new MemoryStream(imageBytes);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error loading media to stream. Url: {Url}", url);
            return null;
        }
    }

    private IMedia CreateMedia(
        MemoryStream stream,
        string comparer,
        string nodeName,
        string fullFileName,
        ImportMediaTypes mediaType,
        int? sortOrder,
        string? identifier,
        int? syncUser)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeName);
        ArgumentException.ThrowIfNullOrEmpty(fullFileName);
        ArgumentNullException.ThrowIfNull(_lastMediaFolder);

        var media = _mediaService.CreateMedia(nodeName, _lastMediaFolder.Id, mediaType.ToString(), userId: syncUser ?? -1);
        media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, fullFileName, stream);
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
        _mediaCount++;

        if (_mediaCount < _mediaFolderPageSize)
        {
            return media;
        }

        var currentNodeName = _lastMediaFolder.Name;
        int.TryParse(currentNodeName, out var newName);
        newName++;

        _lastMediaFolder = CreateMediaFolder(newName.ToString());
        _mediaCount = 0;

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
        ArgumentNullException.ThrowIfNull(_rootMediaFolder);

        var media = _mediaService.CreateMediaWithIdentity(nodeName, _rootMediaFolder.Id, Constants.Conventions.MediaTypes.Folder);
        _mediaService.Save(media);

        return media;
    }
}
