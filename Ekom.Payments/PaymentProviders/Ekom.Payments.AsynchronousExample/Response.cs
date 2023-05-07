using System;
using System.ComponentModel.DataAnnotations;

namespace Ekom.Payments.AsynchronousExample;

/// <summary>
/// Response data from Borgun Server
/// </summary>
public class Response
{
    private string _referenceNumber;
    /// <summary>
    /// Tilvísunarnúmer söluaðila. 
    /// </summary>
    public string ReferenceNumber
    {
        get
        {
            return _referenceNumber;
        }
        set
        {
            _referenceNumber = System.Net.WebUtility.HtmlEncode(value);
        }
    }

    /// <summary>
    /// MD5 hash sem er búið til með að taka MD5 af strengnum VerificationCode + ReferenceNumber á sama hátt og gert er í kafla 4.2.1.1. <para></para>
    /// Nauðsynlegt er að DigitalSignatureResponse sé reiknað út á þeirri síðu sem Greiðslusíðan kallar á og það gildi borið saman við gildið sem Greiðslusíðan sendir <para></para>
    /// til að tryggja að ekki sé verið að búa til tengla og reyna að líkja eftir sölum án þess að greiðsla eigi sér stað. <para></para>
    /// </summary>
    [RegularExpression("[0-9a-zA-Z]+")]
    public string DigitalSignatureResponse { get; set; }

    /// <summary>
    /// Kortategund.
    /// </summary>
    [RegularExpression("[0-9a-zA-Z -_:ÓÞ.,+]+")]
    public string CardType { get; set; }

    /// <summary>
    /// Fyrir PaymentSuccesfulURL þá er þetta síðustu 4 stafir kortnúmers með * táknum fyrir framan. <para></para>
    /// Fyrir PaymentSuccesfulServerSideURL er þetta fyrstu 6 og síðustu 4 stafir kortnúmers með * táknum á milli. <para></para>
    /// Tómt í báðum tilfellum ef sala fór í gegnum Veskið. <para></para>
    /// </summary>
    [RegularExpression("[0-9-* ]+")]
    public string CardNumberMasked { get; set; }

    /// <summary>
    /// Heimildarnúmer. Tómt ef sala fór í gegnum Veskið.
    /// </summary>
    [RegularExpression("[0-9a-zA-Z -_.,]+")]
    public string AuthorizationNumber { get; set; }

    /// <summary>
    /// Færslunúmer. 
    /// </summary>
    public long TransactionNumber { get; set; }

    /// <summary>
    /// GUID sem Greiðslusíðan býr til og er einkvæmt fyrir sölu. 
    /// </summary>
    public Guid SaleID { get; set; }

    /// <summary>
    /// Samningsnúmer sem sala fór á. 
    /// </summary>
    public long ContractNumber { get; set; }

    /// <summary>
    /// Tegund samnings sem sala fór á. ORUGGS fyrir venjulegan greiðslusíðusamning. VESKID fyrir Veskissamning. 
    /// </summary>
    [RegularExpression("[0-9a-zA-Z ]+")]
    public string ContractType { get; set; }

    /// <summary>
    /// Dagsetning sölu (á forminu dd.MM.yyyy). 
    /// </summary>
    [RegularExpression("[0-9a-zA-Z .]+")]
    public string Date { get; set; }
}
