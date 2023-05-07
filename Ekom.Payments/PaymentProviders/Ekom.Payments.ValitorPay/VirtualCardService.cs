using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vettvangur.ValitorPay;

namespace Ekom.Payments.ValitorPay;

/// <summary>
/// Handles storage and retrieval of virtual cards for repeat payments with merchant <br />
/// Proper usage likely includes a complementary member page for managing multiple cards per member, allowing members to choose which card is their current default. <br />
/// It is also possible this service could reside in the core library if enough payment providers have similar requirements for virtual card handling.
/// </summary>
public class VirtualCardService
{
    readonly ILogger<VirtualCardService> _logger;
    readonly string _connectionString;
    readonly ValitorPayService _valitorPayService;

    ValitorPayDbContext ValitorPayDbContext
        => new ValitorPayDbContext(_connectionString);

    public VirtualCardService(
            ILogger<VirtualCardService> logger,
            ValitorPayService valitorPayService,
            IConfiguration configuration)
    {
        _logger = logger;
        _valitorPayService = valitorPayService;

        var connectionStringName = "umbracoDbDSN";
        _connectionString = configuration.GetConnectionString(connectionStringName);
    }

    public async Task SaveVirtualCardAsync(OrderStatus orderStatus)
    {
        if (orderStatus.Member == null || orderStatus.Member == default)
        {
            _logger.LogWarning("Unable to log virtual card due to missing member information");
            return;
        }

        var paymentSettings = JsonConvert.DeserializeObject<PaymentSettings>(orderStatus.EkomPaymentSettingsData);
        var valitorSettings = JsonConvert.DeserializeObject<ValitorPaySettings>(orderStatus.EkomPaymentProviderData);

        _valitorPayService.ConfigureAgreement(valitorSettings.AgreementNumber, valitorSettings.TerminalId);
        var vcardResp = await _valitorPayService.CreateVirtualCardAsync(new CreateVirtualCardRequest
        {
            SubsequentTransactionType = SubsequentTransactionType.CardholderInitiatedCredentialOnFile,

            CardNumber = paymentSettings.CardNumber,
            ExpirationMonth = paymentSettings.CardExpirationMonth,
            ExpirationYear = paymentSettings.CardExpirationYear,
            Cvc = paymentSettings.CardCVV,
        });

        if (!vcardResp.IsSuccess)
        {
            _logger.LogWarning(
                "Unable to save virtual card for member {Member} due to error from ValitorPay {StatusCode} {ResponseMessage}",
                orderStatus.Member,
                vcardResp.ResponseCode,
                vcardResp.ResponseDescription);

            return;
        }

        using var db = ValitorPayDbContext;

        var firstMemberCard = !db.VirtualCards.Any(x => x.Member == orderStatus.Member.Value);

        var newCard = new VirtualCard
        {
            VirtualCardGuid = vcardResp.VirtualCard,
            Member = orderStatus.Member.Value,
            MaskedCreditCardNumber = MaskCardNumber(paymentSettings.CardNumber),
            CreateDate = DateTime.UtcNow,
            ValidThrough
                = paymentSettings.CardExpirationMonth.ToString("D2")
                + "/"
                + paymentSettings.CardExpirationYear.ToString("D2"),

            Default = firstMemberCard,
        };

        // Return order id
        await db.InsertAsync(newCard).ConfigureAwait(false);
    }

    private string MaskCardNumber(string creditCard)
    {
        var length = creditCard.Length;
        var lastFour = creditCard.Substring(length - 4, 4);
        var stars = new string('*', length - 4);
        return stars + lastFour;
    }

    public async Task SetDefaultCard(Guid member, Guid virtualCard)
    {
        using var db = ValitorPayDbContext;
        await db.VirtualCards
            .Where(x => x.Member == member)
            .Set(x => x.Default, false)
            .UpdateAsync();
        await db.VirtualCards
            .Where(x => x.Member == member && x.VirtualCardGuid == virtualCard)
            .Set(x => x.Default, true)
            .UpdateAsync();
    }

    public async Task<VirtualCard?> GetMemberDefaultCardAsync(Guid member)
    {
        using var db = ValitorPayDbContext;
        return await db.VirtualCards.FirstOrDefaultAsync(x => x.Member == member && x.Default);
    }

    public async Task<VirtualCard?> GetVirtualCardAsync(Guid virtualCard)
    {
        using var db = ValitorPayDbContext;
        return await db.VirtualCards.SingleOrDefaultAsync(x => x.VirtualCardGuid == virtualCard);
    }
}
