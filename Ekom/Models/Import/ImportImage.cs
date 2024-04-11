namespace Ekom.Models.Import;

public class ImportImage
{
    /// <summary>
    /// UDI format of the image. Example: udi://media/xxxxx. Use this to reference an existing image in Umbraco.
    /// Note: Either ImageUdi or ImageBytes can be used, but not both. If both are provided, ImageBytes will take precedence and ImageUdi will be ignored.
    /// </summary>
    public string? ImageUdi { get; set; }

    ///// <summary>
    ///// Raw bytes of the image to be imported. This allows for direct image upload, which the service will handle by importing the image into Umbraco.
    ///// Note: Either ImageUdi or ImageBytes can be used, but not both. If both are provided, ImageBytes will take precedence and ImageUdi will be ignored.
    ///// </summary>
    //public byte[]? ImageBytes { get; set; }

    ///// <summary>
    ///// Image Node Name
    ///// </summary>
    //public string NodeName { get; set; }

    //public string Comparer { get; set; }
}
