namespace Ekom.Utilities;

using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

public class PaginationHelper
{
    public int CurrentPage { get; }
    public int TotalPages { get; }

    public PaginationHelper(int currentPage, int totalPages)
    {
        this.CurrentPage = currentPage;
        this.TotalPages = totalPages;
    }

    public List<string> PageRange()
    {
        const int rangeSize = 5;
        var ret = new List<string>();
        int start;

        if (CurrentPage <= rangeSize / 2)
        {
            start = 1;
        }
        else if (CurrentPage + rangeSize / 2 >= TotalPages)
        {
            start = System.Math.Max(TotalPages - rangeSize + 1, 1);
        }
        else
        {
            start = CurrentPage - rangeSize / 2;
        }

        for (int i = 0; i < rangeSize; i++)
        {
            int pageNumber = start + i;
            if (pageNumber <= TotalPages)
            {
                ret.Add(pageNumber.ToString());
            }
        }

        if (int.Parse(ret[ret.Count - 1]) < TotalPages)
        {
            ret.Add("...");
        }

        if (int.Parse(ret[0]) > 1)
        {
            ret.Insert(0, "...");
        }

        return ret;
    }
    
    public static int GetCurrentPage(HttpContext ctx) {
       
        return ctx.Request.Query.ContainsKey("p") && int.TryParse(ctx.Request.Query["p"], out int parsedPage) ? parsedPage : 1;
        
    }
}
