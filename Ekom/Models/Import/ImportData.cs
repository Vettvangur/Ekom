namespace Ekom.Models.Import
{
    /// <summary>
    /// Root container for all import data, representing the complete set of categories to be imported.
    /// </summary>
    public class ImportData
    {
        /// <summary>
        /// A collection of categories to be imported. Each entry in this list represents a category, complete with its hierarchy, products, and associated details, as defined by the <see cref="ImportCategory"/> class. This comprehensive model supports importing a rich, nested structure of categories and products, facilitating complex updates and additions to the e-commerce platform's catalog. The ability to define subcategories and products within each category allows for a deep, tree-structured import, mimicking the natural organization of an e-commerce catalog.
        /// </summary>
        public List<ImportCategory> Categories { get; set; } = new List<ImportCategory>();
    }

    /// <summary>
    /// Represents a category in the import data, including its hierarchy, associated products, and metadata like images and virtual URL flags.
    /// Categories can be nested to represent a category tree structure.
    /// </summary>
    public class ImportCategory : ImportBase
    {
        /// <summary>
        /// Represents a URL-friendly version of the <see cref="Title"/> used for creating more readable and SEO-optimized web addresses. 
        /// If this property is not explicitly set, it will be automatically generated based on the <see cref="Title"/> property, 
        /// ensuring that the resulting slug is both human-readable and suitable for use in URLs. This automatic generation typically 
        /// involves converting the title to lowercase, replacing spaces with hyphens, and removing any special characters that are not 
        /// URL-friendly, thereby facilitating better web standards compliance and user experience.
        /// </summary>
        public Dictionary<string, object>? Slug { get; set; } = new Dictionary<string, object>();
        /// <summary>
        /// Udi format of image. udi://media/xxxxx
        /// </summary>
        public string? Image { get; set; }
        /// <summary>
        /// Determines whether the URL associated with this entity should be excluded from the navigational structure 
        /// of categories/products that descend from it. When set to <c>true</c>, this entity acts as a virtual node, 
        /// meaning its associated URL will not be generated or included in the hierarchical path for any descendant 
        /// categories or products. This is useful for organizing content or products under virtual groupings that do 
        /// not require direct web access via a URL. If this property is left unset or is set to <c>false</c>, the 
        /// entity will be treated as a standard navigational node, with its URL contributing to the path construction 
        /// of descendant entities.
        /// </summary>
        public string? SKU { get; set; }
        public bool? VirtualUrl { get; set; }

        public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
        public List<ImportCategory> SubCategories { get; set; } = new List<ImportCategory>();
        public List<ImportProduct> Products { get; set; } = new List<ImportProduct>();

        private string? _identifier;
        /// <summary>
        /// Optional Identifer value if other then SKU. Represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
        /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
        /// the entity. The usage of this identifier allows for seamless integration and update processes between the external system and our e-commerce platform, 
        /// ensuring that entities are correctly aligned across both systems without ambiguity.
        /// </summary>
        public string? Identifier
        {
            get => _identifier ?? SKU;  // Return _identifier if it's set; otherwise, return SKU.
            set => _identifier = value;
        }
        /// <summary>
        /// Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the 'SKU' property is used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.
        /// </summary>
        public string IdentiferPropertyAlias { get; set; } = "sku";
    }

    /// <summary>
    /// Represents a product with detailed information including pricing, stock levels, backorder capabilities, and categorization for import.
    /// Products are associated with variant groups for more complex product configurations.
    /// </summary>
    public class ImportProduct : ImportBase
    {
        /// <summary>
        /// Represents a URL-friendly version of the <see cref="Title"/> used for creating more readable and SEO-optimized web addresses. 
        /// If this property is not explicitly set, it will be automatically generated based on the <see cref="Title"/> property, 
        /// ensuring that the resulting slug is both human-readable and suitable for use in URLs. This automatic generation typically 
        /// involves converting the title to lowercase, replacing spaces with hyphens, and removing any special characters that are not 
        /// URL-friendly, thereby facilitating better web standards compliance and user experience.
        /// </summary>
        public Dictionary<string, object>? Slug { get; set; } = new Dictionary<string, object>();
        public string? SKU { get; set; }
        public object? Description { get; set; }
        public List<ImportPrice> Price { get; set; } = new List<ImportPrice>();
        public List<ImportStock> Stock { get; set; } = new List<ImportStock>();
        public bool EnableBackorder { get; set; }
        public decimal? Vat { get; set; }
        /// <summary>
        /// List of Udi format of content. udi://document/xxxxx. Defines a collection of category identifiers where the product is additionally listed, beyond its primary category. 
        /// This property enables the product to be associated with multiple categories, facilitating broader visibility across 
        /// the catalog. By specifying additional categories here, the product can be discovered under various contexts or 
        /// classifications, enhancing its reach and relevance to different customer interests or search queries. Each string 
        /// in the list should correspond to a unique identifier of a category within the system. Initializing this property 
        /// with an empty list ensures that the product can be programmatically associated with categories post-creation.
        /// 
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();

        public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
        public List<ImportVariantGroup> VariantGroups { get; set; } = new List<ImportVariantGroup>();

        private string? _identifier;
        /// <summary>
        /// Optional Identifer value if other then SKU. Represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
        /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
        /// the entity. The usage of this identifier allows for seamless integration and update processes between the external system and our e-commerce platform, 
        /// ensuring that entities are correctly aligned across both systems without ambiguity.
        /// </summary>
        public string? Identifier
        {
            get => _identifier ?? SKU;  // Return _identifier if it's set; otherwise, return SKU.
            set => _identifier = value;
        }
        /// <summary>
        /// Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the 'SKU' property is used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.
        /// </summary>
        public string IdentiferPropertyAlias { get; set; } = "sku";
    }

    /// <summary>
    /// Groups variants of a product together, providing a mechanism to associate different versions of a product under a single product listing.
    /// Each variant group can contain multiple variants, differentiated by attributes like size, color, or material.
    /// </summary>
    public class ImportVariantGroup : ImportBase
    {
        /// <summary>
        /// List of Udi format of image. udi://media/xxxxx
        /// </summary>
        public List<string> Images { get; set; } = new List<string>();
        public List<ImportVariant> Variants { get; set; } = new List<ImportVariant>();
    }

    /// <summary>
    /// Defines a specific variant within a variant group, detailing unique attributes like SKU, pricing, and stock levels.
    /// Variants allow customers to select different options for a product based on their preferences or requirements.
    /// </summary>
    public class ImportVariant : ImportBase
    {
        public string? SKU { get; set; }
        /// <summary>
        /// List of Udi format of image. udi://media/xxxxx
        /// </summary>
        public List<string> Images { get; set; } = new List<string>();
        public object? Description { get; set; }
        public List<ImportPrice> Price { get; set; } = new List<ImportPrice>();
        public List<ImportStock> Stock { get; set; } = new List<ImportStock>();
        public bool EnableBackorder { get; set; }

    }

    /// <summary>
    /// Base class for import entities, providing common properties like Title, NodeName, and Comparer for update detection.
    /// The Comparer property is especially critical for identifying when an import entity has changed and requires an update.
    /// </summary>
    public class ImportBase
    {
        public required object Title { get; set; }

        /// <summary>
        /// Set the name of the node in Umbraco
        /// </summary>
        public required string NodeName { get; set; }


        /// <summary>
        /// Represents a value used by the importer to determine whether to update a particular node. It is recommended 
        /// to assign a hash of the object's relevant data to this property. By comparing this value with the existing 
        /// data's hash, the importer can efficiently identify changes and decide if an update is necessary. Storing a 
        /// hash here optimizes the comparison process, especially for complex objects, by simplifying equality checks 
        /// and minimizing the need for deep object comparisons. This approach enhances the overall efficiency and 
        /// reliability of the import operation.
        /// </summary>
        public required string Comparer { get; set; }

        /// <summary>
        /// A collection of key-value pairs representing additional, custom data associated with this node. This flexible structure allows for 
        /// the storage of miscellaneous properties not explicitly defined by the node's class structure. Keys should be unique identifiers for 
        /// each property, with the corresponding values holding the property data. This dictionary is ideal for dynamic data models where the 
        /// set of properties might vary between instances or need to extend beyond the predefined fields of the node. Use this to capture 
        /// additional information required for import operations or to accommodate custom requirements without altering the node's core schema.
        /// </summary>
        public Dictionary<string,object>? AdditionalProperties { get; set; }

    }

    public class ImportPrice
    {
        public required string StoreAlias { get; set; }
        public required decimal Price { get; set; }
        public required string Currency { get; set; }
    }

    public class ImportStock
    {
        public required string StoreAlias { get; set; }
        public required decimal Stock { get; set; }
    }

}
