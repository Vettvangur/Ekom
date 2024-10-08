using Ekom.Models.Import;
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
        IShortStringHelper shortStringHelper)
    {
        _mediaService = mediaService;
        _httpClientFactory = httpClientFactory;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
    }

    public IMedia GetRootMedia(Guid rootMediaKey)
    {
        rootMediaFolder = _mediaService.GetRootMedia().FirstOrDefault(x => x.Key == rootMediaKey);

        ArgumentNullException.ThrowIfNull(rootMediaFolder);

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

        var mediaItems = _mediaService.GetPagedChildren(lastMediaFolder.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == MediaTypes.Image || x.ContentType.Alias == MediaTypes.File).ToList();

        mediaCount = mediaItems.Count;
    }

    public List<IMedia> GetRootMediaChildren(IMedia rootMedia)
    {
        var mediaFolders = _mediaService.GetPagedChildren(rootMedia.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == MediaTypes.Folder).ToList();

        return mediaFolders;
    }

    public List<IMedia> GetUmbracoMediaFiles(IMedia rootMedia)
    {
        var mediaFiles = _mediaService.GetPagedDescendants(rootMedia.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == MediaTypes.Image || x.ContentType.Alias == MediaTypes.File).ToList();

        return mediaFiles;
    }

    public IMedia ImportMediaFromExternalUrl(ImportMediaFromExternalUrl image, string comparer, ImportMediaTypes mediaType, string? identifier)
    {
        var stream = LoadMediaToMemoryStreamAsync(image.Url).Result;
        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier);
    }

    public IMedia ImportMediaFromBytes(ImportMediaFromBytes image, string comparer, ImportMediaTypes mediaType, string? identifier)
    {
        var stream = new MemoryStream(image.Bytes);
        stream.Seek(0, SeekOrigin.Begin);

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier);
    }

    public IMedia ImportMediaFromBase64(ImportMediaFromBase64 image, string comparer, ImportMediaTypes mediaType, string? identifier)
    {        
        // Convert Base64 String to byte[]
        byte[] bytes = Convert.FromBase64String(image.Base64);

        // Create a MemoryStream with the bytes
        MemoryStream stream = new MemoryStream(bytes);

        stream.Seek(0, SeekOrigin.Begin);

        return CreateMedia(stream, comparer, image.NodeName, image.FileName, mediaType, image.SortOrder, identifier);
    }

    public IMedia UpdateMediaSortOrder(IMedia media, IImportMedia importMedia)
    {
        return UpdateSortOrderMedia(media, importMedia.SortOrder);
    }

    private async Task<MemoryStream> LoadMediaToMemoryStreamAsync(string url)
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
            // Handle exceptions (network issues, bad URL, etc)
            throw new Exception($"Error loading media to stream: {ex.Message} Url: {url}");
        }
    }

    private IMedia CreateMedia(MemoryStream mem, string comparer, string nodeName, string fullFileName, Ekom.Models.Import.ImportMediaTypes mediaType, int? sortOrder, string? identifier)
    {
        var media = _mediaService.CreateMediaWithIdentity(nodeName, lastMediaFolder.Id, mediaType.ToString());
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
