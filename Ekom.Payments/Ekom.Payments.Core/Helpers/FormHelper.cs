using System.Collections.Generic;
using System.Text;

namespace Ekom.Payments.Helpers;

static class FormHelper
{
    public static string CreateRequest(Dictionary<string, string?> parameters, string url, string method = "POST")
    {
        var html = new StringBuilder($"<form action=\"{url}\" method=\"{method}\" id=\"payform\"> ");

        foreach (var param in parameters)
        {
            html.Append($"<input type=\"hidden\" name=\"{param.Key}\" value=\"{param.Value}\"> ");
        }

        html.Append("<input type=\"submit\" value=\"Submitting\"> ");

        html.Append(@"<noscript> Please click the submit button below.<br/> <button>Submit</button> </noscript>");

        html.Append("</form>");

        html.Append("<script>(function(){ document.getElementById(\"payform\").submit(); }())</script>");

        return html.ToString();
    }
}
