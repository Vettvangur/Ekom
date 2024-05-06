namespace Ekom.Models;

public class HeadlessConfig
{
    public List<RevalidateApi> ReValidateApis { get; set; } = new List<RevalidateApi>();
}
public class RevalidateApi
{
    public string Store { get; set; }
    public string Url { get; set; }
    public string Secret { get; set; }
}
