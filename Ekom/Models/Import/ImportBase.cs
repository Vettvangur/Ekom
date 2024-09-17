namespace Ekom.Models.Import;

/// <summary>
/// Base class for import entities, providing common properties like Title, NodeName, and Comparer for update detection.
/// The Comparer property is especially critical for identifying when an import entity has changed and requires an update.
/// </summary>
public class ImportBase
{
    public required Dictionary<string, object> Title { get; set; } = new Dictionary<string, object>();

    public List<IImportMedia> Images { get; set; } = new List<IImportMedia>();

    /// <summary>
    /// Set the name of the node in Umbraco
    /// </summary>
    public required string NodeName { get; set; }

    /// <summary>
    /// Represents a unique identifier for comparing the current state of an object against its previous state to determine the necessity of an update.
    /// This property serves as a key factor in the import process, enabling efficient change detection by comparing the current object state 
    /// with a stored reference state. When not manually assigned, this identifier is automatically generated by computing a SHA hash 
    /// of the object's serialized representation. This automated generation ensures that any modification to the object's relevant properties 
    /// results in a different identifier, facilitating accurate and efficient change tracking. 
    /// </summary>
    public string? Comparer { get; set; }

    /// <summary>
    /// A collection of key-value pairs representing additional, custom data associated with this node. This flexible structure allows for 
    /// the storage of miscellaneous properties not explicitly defined by the node's class structure. Keys should be unique identifiers for 
    /// each property, with the corresponding values holding the property data. This dictionary is ideal for dynamic data models where the 
    /// set of properties might vary between instances or need to extend beyond the predefined fields of the node. Use this to capture 
    /// additional information required for import operations or to accommodate custom requirements without altering the node's core schema.
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// A collection of key-value pairs intended for temporary use during events. These properties are used to store event-specific data that is not 
    /// automatically saved to the underlying Umbraco node. This allows for the capture and utilization of transient data that may be critical for event 
    /// processing or handling but does not require long-term persistence within the node's core structure. Use this dictionary to manage data 
    /// dynamically during runtime without impacting the node’s permanent data storage.
    /// </summary>
    public Dictionary<string, object>? EventProperties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Identifer value represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
    /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
    /// the entity. "ekmIdentifier" is the alias of the property
    /// </summary>
    public required string Identifier { get; set; }
    /// <summary>
    /// The default behavior is to save/publish newsly created and modified data.
    /// You can change it so data will be unpublished or only saved
    /// </summary>
    public ImportSaveEntEnum SaveEvent { get; set; } = ImportSaveEntEnum.SavePublish;
}
