using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

/// <summary>Discriminates why a note mutation did not succeed.</summary>
public enum CustomerNoteMutationFailure
{
    None,
    CustomerNotFound,
}

public readonly record struct CustomerNoteMutationResult(
    CustomerNote? Note, CustomerNoteMutationFailure Failure)
{
    public static CustomerNoteMutationResult Success(CustomerNote note) => new(note, CustomerNoteMutationFailure.None);
    public static CustomerNoteMutationResult Failed(CustomerNoteMutationFailure failure) => new(null, failure);
}

internal sealed class CustomerNoteService(
    CustomerManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    public async Task<CustomerNoteMutationResult> AddAsync(
        Guid customerId, AddCustomerNoteRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
        {
            return CustomerNoteMutationResult.Failed(CustomerNoteMutationFailure.CustomerNotFound);
        }

        CustomerNote note = new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Body = request.Body.Trim(),
            AuthorUserId = Guid.Parse(currentUserAccessor.Handle!),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        dbContext.CustomerNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(customerId, note.Id, cancellationToken);
        return CustomerNoteMutationResult.Success(note);
    }

    public async Task<List<CustomerNote>> ListAsync(Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.CustomerNotes
            .AsNoTracking()
            .Where(note => note.CustomerId == customerId)
            .OrderBy(note => note.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    private Task RecordAuditAsync(Guid customerId, Guid noteId, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown",
                "note_added",
                "CustomerNote",
                noteId.ToString(),
                Metadata: new Dictionary<string, string> { ["customerId"] = customerId.ToString() }),
            cancellationToken);
}
