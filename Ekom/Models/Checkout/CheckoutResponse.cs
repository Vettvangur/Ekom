namespace Ekom.Models
{
    /// <summary>
    /// 230 Html response body initiated redirect. document.write(response.text) f.x.
    /// 530 Stock error
    /// </summary>
    public class CheckoutResponse
    {
        public object ResponseBody { get; set; }

        public int HttpStatusCode { get; set; }
        public string ReturnUrl { get; set; } = "";
    }
}
