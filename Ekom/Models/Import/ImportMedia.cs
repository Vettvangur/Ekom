namespace Ekom.Models.Import;

/// <summary>
/// Represents an media import entity where the media is identified using a Universal Identifier (UDI) 
/// within the Umbraco CMS. This class is used to reference medias that already exist in the media library of Umbraco.
/// </summary>
public class ImportMediaFromUdi : IImportMedia
{
    public required string Udi { get; set; }
}

/// <summary>
/// Represents an media import entity for importing media from external URLs. This class facilitates the 
/// inclusion of media into Umbraco from remote sources by specifying the URL, associated metadata, and 
/// system identifiers necessary for integration within the CMS.
/// </summary>
public class ImportMediaFromExternalUrl : IImportMedia
{
    public required string Url { get; set; }
    public required string FileName { get; set; }
    public required string NodeName { get; set; }
    public DateTime? Date { get; set; }
    public string? Comparer { get; set; }
}

/// <summary>
/// Represents an media import entity where the media data is provided as a raw byte array. This class is designed 
/// to manage direct uploads of media data into Umbraco, storing the raw content along with associated metadata 
/// and identifiers needed for content management and integration.
/// </summary>
public class ImportMediaFromBytes : IImportMedia
{
    public required byte[] Bytes { get; set; }
    public required string FileName { get; set; }
    public required string NodeName { get; set; }
    public DateTime? Date { get; set; }
    public string? Comparer { get; set; }
}

/// <summary>
/// Represents an media import entity where the media data is provided as a raw byte array. This class is designed 
/// to manage direct uploads of media data into Umbraco, storing the raw content along with associated metadata 
/// and identifiers needed for content management and integration.
/// </summary>
public class ImportMediaFromBase64 : IImportMedia
{
    public required string Base64 { get; set; }
    public required string FileName { get; set; }
    public required string NodeName { get; set; }
    public DateTime? Date { get; set; }
    public string? Comparer { get; set; }
}
