using System.ComponentModel.DataAnnotations;

namespace Ekom.Payments;

/// <summary>
/// Customer Information.
/// Supply remote payment provider with customer information using <see cref="PaymentSettings"/>
/// </summary>
public class CustomerInfo
{
    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    /// <value>
    /// The address.
    /// </value>
    [Required]
    [RegularExpression(".{2,}")]
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    /// <value>
    /// The city.
    /// </value>
    [Required]
    [RegularExpression(".{2,}")]
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    /// <value>
    /// The email.
    /// </value>
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>
    /// The name.
    /// </value>
    [Required]
    [RegularExpression(".{2,}")]
    public string Name { get; set; }

    /// <summary>
    /// Icelandic social security number.
    /// </summary>
    /// <value>
    /// The national registry identifier.
    /// </value>
    [Required]
    [RegularExpression("[0-9]{6}(-)?[0-9]{4}")]
    public string NationalRegistryId { get; set; }

    /// <summary>
    /// Gets or sets the phone number.
    /// </summary>
    /// <value>
    /// The phone number.
    /// </value>
    [Required]
    [Phone]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    /// <value>
    /// The postal code.
    /// </value>
    [Required]
    [RegularExpression("[0-9]{3,}")]
    public string PostalCode { get; set; }
}
