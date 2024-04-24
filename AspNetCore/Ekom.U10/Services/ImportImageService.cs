using Ekom.Models.Import;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;
public class ImportImageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaService _mediaService;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly MediaFileManager _mediaFileManager;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IShortStringHelper _shortStringHelper;

    private int mediaCount = 0;
    private IMedia? lastMediaFolder;
    private IMedia? rootMediaFolder;

    public ImportImageService (
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

        var mediaItems = _mediaService.GetPagedChildren(lastMediaFolder.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == Constants.Conventions.MediaTypes.Image).ToList();

        mediaCount = mediaItems.Count;
    }

    public List<IMedia> GetRootMediaChildren(IMedia rootMedia)
    {
        var mediaFolders = _mediaService.GetPagedChildren(rootMedia.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == Constants.Conventions.MediaTypes.Folder).ToList();

        return mediaFolders;
    }

    public List<IMedia> GetUmbracoMediaFiles(IMedia rootMedia)
    {
        var mediaFiles = _mediaService.GetPagedDescendants(rootMedia.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == Constants.Conventions.MediaTypes.Image).ToList();

        return mediaFiles;
    }

    public IMedia ImportImageFromExternalUrl(ImportImageFromExternalUrl image, string comparer)
    {
        var stream = LoadImageToMemoryStreamAsync(image.ImageUrl).Result;
        return CreateMediaImage(stream, comparer, image.NodeName, image.FileName);
    }

    public IMedia ImportImageFromBytes(ImportImageFromBytes image, string comparer)
    {
        var stream = new MemoryStream(image.ImageBytes);
        stream.Seek(0, SeekOrigin.Begin);

        return CreateMediaImage(stream, comparer, image.NodeName, image.FileName);
    }

    public IMedia ImportImageFromBase64(ImportImageFromBase64 image, string comparer)
    {        
        // Convert Base64 String to byte[]
        byte[] bytes = Convert.FromBase64String(image.ImageBase64);

        // Create a MemoryStream with the bytes
        MemoryStream stream = new MemoryStream(bytes);

        stream.Seek(0, SeekOrigin.Begin);

        return CreateMediaImage(stream, comparer, image.NodeName, image.FileName);
    }

    private async Task<MemoryStream> LoadImageToMemoryStreamAsync(string imageUrl)
    {
        try
        {
            // Create a new HttpClient using the factory
            var httpClient = _httpClientFactory.CreateClient();

            // Send a GET request to the image URL
            var response = await httpClient.GetAsync(imageUrl);
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
            throw new Exception($"Error loading image: {ex.Message} Image: {imageUrl}");
        }
    }

    private IMedia CreateMediaImage(MemoryStream mem, string comparer, string nodeName, string fullFileName)
    {
        var media = _mediaService.CreateMediaWithIdentity(nodeName, lastMediaFolder.Id, Constants.Conventions.MediaTypes.Image);
        media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, fullFileName, mem);
        media.SetValue("comparer", comparer);
        _mediaService.Save(media);

        mediaCount++;

        if (mediaCount == 150)
        {
            var currentNodeName = lastMediaFolder.Name;

            int.TryParse(currentNodeName, out int newName);

            newName++;

            lastMediaFolder = CreateMediaFolder(newName.ToString());
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
