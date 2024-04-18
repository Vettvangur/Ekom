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
        var rootMediaFolder = _mediaService.GetRootMedia().FirstOrDefault(x => x.Key == rootMediaKey);

        ArgumentNullException.ThrowIfNull(rootMediaFolder);

        return rootMediaFolder;
    }

    public List<IMedia> GetUmbracoMediaFiles(IMedia rootMedia)
    {
        var mediaFiles = _mediaService.GetPagedChildren(rootMedia.Id, 0, int.MaxValue, out var _).Where(x => !x.Trashed && x.ContentType.Alias == "Image").ToList();

        return mediaFiles;
    }

    public IMedia ImportImageFromExternalUrl(ImportImageFromExternalUrl externalUrlImage, string comparer)
    {
        return null;
        //var stream = LoadImageToMemoryStreamAsync(externalUrlImage.ImageUrl).Result;
        //return CreateMediaImage(stream, comparer, externalUrlImage.NodeName, externalUrlImage.FileName);
    }

    public IMedia ImportImageFromBytes(ImportImageFromBytes bytesImage, string comparer)
    {
        throw new NotImplementedException();
    }

    public IMedia ImportImageFromBase64(ImportImageFromBase64 base64Image, string comparer)
    {
        throw new NotImplementedException();
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
            memoryStream.Position = 0; // Reset the memory stream position

            return memoryStream;
        }
        catch (Exception ex)
        {
            // Handle exceptions (network issues, bad URL, etc)
            throw new Exception($"Error loading image: {ex.Message} Image: {imageUrl}");
        }
    }

    private IMedia CreateMediaImage(MemoryStream mem, string comparer, string nodeName, string fullFileName, IMedia rootMediaFolder)
    {
        var media = _mediaService.CreateMediaWithIdentity(nodeName, rootMediaFolder.Id, Constants.Conventions.MediaTypes.Image);
        media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, fullFileName, mem);
        media.SetValue("comparer", comparer);
        _mediaService.Save(media);

        return media;
    }
}
