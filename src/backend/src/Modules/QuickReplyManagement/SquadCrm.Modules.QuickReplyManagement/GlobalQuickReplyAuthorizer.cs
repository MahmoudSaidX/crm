using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace SquadCrm.Modules.QuickReplyManagement;

/// <summary>
/// Answers the one authorization question this module cannot express
/// declaratively on a route: may the caller manage GLOBAL templates?
/// <para>
/// Create carries its scope in the body, and update/activate/deactivate act on
/// a row whose scope is only known after it is loaded, so
/// <c>RequireAuthorization</c> on the endpoint cannot decide it — the check has
/// to happen inside the service, after the scope is known.
/// </para>
/// <para>
/// It is a port rather than a direct <see cref="IAuthorizationService"/>
/// dependency so <c>QuickReplyService</c> stays constructible in the
/// persistence integration suite (which has no HTTP context), exactly as that
/// suite already constructs <c>DepartmentService</c>.
/// </para>
/// </summary>
internal interface IGlobalQuickReplyAuthorizer
{
    Task<bool> CanManageGlobalAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Evaluates the centrally registered <c>permission:quickreplies.manageglobal</c>
/// policy against the current request's principal. The policy itself is owned
/// and registered by RoleManagementModule — referenced here by string only, no
/// project reference, matching the precedent set by every other module.
/// <para>
/// Fails closed: no HTTP context and no principal both mean "not permitted".
/// </para>
/// </summary>
internal sealed class GlobalQuickReplyAuthorizer(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService) : IGlobalQuickReplyAuthorizer
{
    public async Task<bool> CanManageGlobalAsync(CancellationToken cancellationToken)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User is null)
        {
            return false;
        }

        AuthorizationResult result = await authorizationService.AuthorizeAsync(
            httpContext.User, PermissionPolicies.QuickRepliesManageGlobal);
        return result.Succeeded;
    }
}
