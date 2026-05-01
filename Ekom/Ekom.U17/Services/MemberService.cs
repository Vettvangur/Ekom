using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.Logging;

namespace Ekom.Umb.Services;

internal sealed class MemberService : IMemberService
{
    private readonly ILogger<MemberService> _logger;

    public MemberService(ILogger<MemberService> logger)
    {
        _logger = logger;
    }

    public Task<UmbracoMember> GetCurrentMember()
    {
        _logger.LogDebug("Umbraco 17 member lookup is not ported yet.");

        return Task.FromResult<UmbracoMember>(null!);
    }

    public UmbracoMember GetByUsername(string t)
    {
        _logger.LogDebug("Umbraco 17 member lookup by username is not ported yet. Username: {Username}", t);

        return null!;
    }

    public void Save(Dictionary<string, object> t, UmbracoMember member)
    {
        _logger.LogDebug("Umbraco 17 member save is not ported yet.");
    }

    public void Save(Dictionary<string, object> t, string? userSsn)
    {
        _logger.LogDebug("Umbraco 17 member save by username is not ported yet. Username: {Username}", userSsn);
    }
}
