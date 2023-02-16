namespace Ekom.Payments.Valitor;

/// <summary>
/// Custom LoanType specification for Valitor, allows Valitor PP to approximate how Borgun specifies their loans.
/// We map from these values to the correct Valitor parameters inside Payment
/// </summary>
public enum LoanType
{
    /// <summary>
    /// </summary>
    Disabled,

    /// <summary>
    /// </summary>
    IsLoan,

    /// <summary>
    /// </summary>
    IsInterestFreeLoan,
}
