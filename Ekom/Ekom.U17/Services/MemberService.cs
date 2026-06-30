using Ekom.Models;
using Ekom.Services;
using Ekom.Umb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;

namespace Ekom.Umb.Services;

internal sealed class MemberService : IMemberService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MemberService> _logger;

    public MemberService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<MemberService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<UmbracoMember> GetCurrentMember()
    {
        var memberManager = _httpContextAccessor.HttpContext?.RequestServices.GetService<IMemberManager>();

        if (memberManager == null)
        {
            return null!;
        }

        var member = await memberManager.GetCurrentMemberAsync();
        var publishedMember = memberManager.AsPublishedMember(member);

        return publishedMember == null ? null! : new Umbraco17Member(publishedMember, member!.UserName!);
    }

    public UmbracoMember GetByUsername(string userName)
    {
        try
        {
            var memberService = _httpContextAccessor.HttpContext?.RequestServices.GetService<Umbraco.Cms.Core.Services.IMemberService>();
            var member = memberService?.GetByUsername(userName);

            return member == null ? null! : new Umbraco17Member(member);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get member by username: {Username}", userName);

            return null!;
        }
    }

    public void Save(Dictionary<string, object> data, UmbracoMember member)
    {
        Save(data, member.UserName);
    }

    public void Save(Dictionary<string, object> data, string? userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return;
        }

        var memberService = _httpContextAccessor.HttpContext?.RequestServices.GetService<Umbraco.Cms.Core.Services.IMemberService>();
        var member = memberService?.GetByUsername(userName);

        if (member == null)
        {
            return;
        }

        foreach (var item in data)
        {
            if (member.HasProperty(item.Key))
            {
                member.SetValue(item.Key, item.Value);
            }
        }

        memberService?.Save(member);
    }
}
