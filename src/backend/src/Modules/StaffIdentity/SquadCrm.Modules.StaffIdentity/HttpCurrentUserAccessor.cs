using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SquadCrm.BuildingBlocks.Security;

namespace SquadCrm.Modules.StaffIdentity;

internal sealed class HttpCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? Handle => IsAuthenticated
        ? Principal?.FindFirst("sub")?.Value
        : null;
}
