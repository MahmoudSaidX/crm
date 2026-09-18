using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace SquadCrm.Modules.QuickReplyManagement.Infrastructure.Authorization;

/// <summary>
/// Answers whether the current caller may use ticket-scoped variables
/// (<c>TicketNumber</c>/<c>CustomerName</c>) when resolving a quick reply
/// (CRM-146). Checks the centrally registered <c>permission:tickets.view</c>
/// policy by string only — no project reference to TicketManagement's
/// presentation layer, same precedent as
/// <see cref="GlobalQuickReplyAuthorizer"/> for
/// <c>permission:quickreplies.manageglobal</c>.
/// <para>
/// A denial here does not fail the whole resolve call: the caller treats it
/// as "these tokens are unresolved" (surfaced to the agent, left literal),
/// not a 403 — resolving is producing draft text, not reading the ticket
/// record itself.
/// </para>
/// </summary>
internal interface ITicketAccessAuthorizer
{
    Task<bool> CanViewTicketsAsync(CancellationToken cancellationToken);
}

internal sealed class TicketAccessAuthorizer(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService) : ITicketAccessAuthorizer
{
    private const string TicketsViewPolicy = "permission:tickets.view";

    public async Task<bool> CanViewTicketsAsync(CancellationToken cancellationToken)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User is null)
        {
            return false;
        }

        AuthorizationResult result = await authorizationService.AuthorizeAsync(httpContext.User, TicketsViewPolicy);
        return result.Succeeded;
    }
}
