namespace Ekom.Models.Import;

/// <summary>
/// Represents an image import entity where the image is identified using a Universal Identifier (UDI) 
/// within the Umbraco CMS. This class is used to reference images that already exist in the media library of Umbraco.
/// </summary>
public class ImportImageFromUdi : IImportImage
{
    public required string ImageUdi { get; set; }
}

/// <summary>
/// Represents an image import entity for importing images from external URLs. This class facilitates the 
/// inclusion of images into Umbraco from remote sources by specifying the URL, associated metadata, and 
/// system identifiers necessary for integration within the CMS.
/// </summary>
public class ImportImageFromExternalUrl : IImportImage
{
    public required string ImageUrl { get; set; }
    public required string FileName { get; set; }
    public required string NodeName { get; set; }
    public DateTime? Date { get; set; }
    public string? Comparer { get; set; }
}

/// <summary>
/// Represents an image import entity where the image data is provided as a raw byte array. This class is designed 
/// to manage direct uploads of image data into Umbraco, storing the raw content along with associated metadata 
/// and identifiers needed for content management and integration.
/// </summary>
public class ImportImageFromBytes : IImportImage
{
    public required byte[] ImageBytes { get; set; }
    public required string FileName { get; set; }
    public required string NodeName { get; set; }
    public DateTime? Date { get; set; }
    public string? Comparer { get; set; }
}

/// <summary>
/// Represents an image import entity where the image data is provided as a raw byte array. This class is designed 
/// to manage direct uploads of image data into Umbraco, storing the raw content along with associated metadata 
/// and identifiers needed for content management and integration.
/// </summary>
public class ImportImageFromBase64 : IImportImage
{
    public required string ImageBase64 { get; set; }
    public required string FileName { get; set; }
    public required string NodeName { get; set; }
    public DateTime? Date { get; set; }
    public string? Comparer { get; set; }
}
