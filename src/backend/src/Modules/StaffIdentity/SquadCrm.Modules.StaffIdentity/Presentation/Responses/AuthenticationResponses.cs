namespace SquadCrm.Modules.StaffIdentity.Presentation.Responses;

public sealed record AccessCredentialResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record CurrentStaffResponse(Guid StaffUserId);
