using Ekom.Utilities;

namespace Ekom.Models;

public class Image
{
    public Image(UmbracoContent node, string storeAlias)
    {
        Id = node.Id;
        Key = node.Key;
        Url = node.Url;
        Name = node.Name;
        Description = node.Properties.HasPropertyValue("description", storeAlias) ? node.GetValue("description", storeAlias) : "";
    }

    public Image()
    {

    }

    public int Id { get; set; }
    public Guid Key { get; set; }

    public string Url { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }
}
