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

        /// <summary>
        /// Represents a collection of products to be imported. Each entry in this list corresponds to a product,
        /// as detailed by the <see cref="ImportProduct"/> class.
        /// </summary>
        public List<ImportProduct> Products { get; set; } = new List<ImportProduct>();
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
        public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
        public List<ImportCategory> SubCategories { get; set; } = new List<ImportCategory>();

        private string? _identifier;
        /// <summary>
        /// Optional Identifer value if other then SKU. Represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
        /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
        /// the entity. If <see cref="IdentiferPropertyAlias"/> is not "sku" then the value will be saved to that property
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
        public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
        public List<ImportPrice> Price { get; set; } = new List<ImportPrice>();
        public List<ImportStock> Stock { get; set; } = new List<ImportStock>();
        public bool EnableBackorder { get; set; }
        public decimal? Vat { get; set; }

        /// <summary>
        /// Represents a collection of category identifiers associated with a product. The first identifier
        /// in the list denotes the primary category of the product, while subsequent identifiers represent
        /// additional categories to which the product is linked. This categorization aids in organizing
        /// products within different classifications for easier access and management.
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();

        public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
        public List<ImportVariantGroup> VariantGroups { get; set; } = new List<ImportVariantGroup>();

        private string? _identifier;
        /// <summary>
        /// Optional Identifer value if other then SKU. Represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
        /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
        /// the entity. If <see cref="IdentiferPropertyAlias"/> is not "sku" then the value will be saved to that property
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
        public List<ImportVariant> Variants { get; set; } = new List<ImportVariant>();
    }

    /// <summary>
    /// Defines a specific variant within a variant group, detailing unique attributes like SKU, pricing, and stock levels.
    /// Variants allow customers to select different options for a product based on their preferences or requirements.
    /// </summary>
    public class ImportVariant : ImportBase
    {
        public string? SKU { get; set; }
        public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
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
        public required Dictionary<string, object> Title { get; set; } = new Dictionary<string, object>();

        public List<ImportImage> Images { get; set; } = new List<ImportImage>();

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
        public Dictionary<string,object>? AdditionalProperties { get; set; } = new Dictionary<string, object>();

    }

    /// <summary>
    /// Represents the pricing information of an item for a specific store.
    /// </summary>
    public class ImportPrice
    {
        /// <summary>
        /// Gets or sets the alias of the store where this price is applicable.
        /// </summary>
        /// <value>The store alias as a string.</value>
        public required string StoreAlias { get; set; }

        /// <summary>
        /// Gets or sets the price of the item.
        /// </summary>
        /// <value>The price as a decimal number.</value>
        public required decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the currency code for the price.
        /// </summary>
        /// <value>The currency code as a string.</value>
        public required string Currency { get; set; }
    }


    /// <summary>
    /// Represents the stock level of an item for a specific store.
    /// </summary>
    public class ImportStock
    {
        /// <summary>
        /// Gets or sets the alias of the store where this stock level is applicable.
        /// </summary>
        /// <value>The store alias as a string.</value>
        public required string StoreAlias { get; set; }

        /// <summary>
        /// Gets or sets the stock level of the item.
        /// </summary>
        /// <value>The stock level as an integer.</value>
        public required int Stock { get; set; }
    }


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

}
