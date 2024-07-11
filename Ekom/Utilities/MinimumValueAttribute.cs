using System.ComponentModel.DataAnnotations;

namespace Ekom.Utilities;

public class MinimumValueAttribute : ValidationAttribute
{
    private readonly int _minValue;

    public MinimumValueAttribute(int minValue)
    {
        _minValue = minValue;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is int intValue)
        {
            if (intValue < _minValue)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must be greater than or equal to {_minValue}.");
            }
        }
        return ValidationResult.Success;
    }
}
