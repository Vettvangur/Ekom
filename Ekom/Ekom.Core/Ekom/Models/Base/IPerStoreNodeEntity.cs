namespace Ekom.Models
{
    /// <summary>
    /// Base Per Store Umbraco node entity
    /// </summary>
    public interface IPerStoreNodeEntity : INodeEntity
    {
    /// <summary>
    /// Ekom Store
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [System.Xml.Serialization.XmlIgnore]
    IStore Store { get; }
    }
}
