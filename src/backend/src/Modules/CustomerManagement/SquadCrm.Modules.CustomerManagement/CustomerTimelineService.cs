using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

/// <summary>Discriminates why a timeline request did not succeed.</summary>
public enum CustomerTimelineFailure
{
    None,
    CustomerNotFound,
}

public readonly record struct CustomerTimelineResult(
    List<CustomerTimelineEntryResponse>? Entries, CustomerTimelineFailure Failure)
{
    public static CustomerTimelineResult Success(List<CustomerTimelineEntryResponse> entries) =>
        new(entries, CustomerTimelineFailure.None);

    public static CustomerTimelineResult Failed(CustomerTimelineFailure failure) => new(null, failure);
}

/// <summary>
/// Composes a chronological interaction timeline for a customer by querying
/// existing <c>CustomerManagement</c> tables directly (CRM-129). This stays
/// within the module's own schema, so it does not cross a module boundary.
/// An event-sourced read model (domain events + outbox + projection) is
/// deferred until a second module actually needs to publish into the
/// timeline — see the CRM-129 Squad Kit intake for the full rationale.
/// </summary>
internal sealed class CustomerTimelineService(CustomerManagementDbContext dbContext)
{
    public async Task<CustomerTimelineResult> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        Customer? customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return CustomerTimelineResult.Failed(CustomerTimelineFailure.CustomerNotFound);
        }

        List<CustomerTimelineEntryResponse> entries = [];

        entries.Add(new CustomerTimelineEntryResponse(
            "CustomerCreated",
            customer.CreatedAtUtc,
            null,
            "Customer",
            customer.Id,
            "Customer profile created",
            CustomerTimelineVisibility.Customer));
        if (customer.UpdatedAtUtc != customer.CreatedAtUtc)
        {
            entries.Add(new CustomerTimelineEntryResponse(
                "CustomerUpdated",
                customer.UpdatedAtUtc,
                null,
                "Customer",
                customer.Id,
                "Customer profile updated",
                CustomerTimelineVisibility.Customer));
        }

        List<CustomerContact> contacts = await dbContext.CustomerContacts
            .AsNoTracking()
            .Where(contact => contact.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        entries.AddRange(contacts.Select(contact => new CustomerTimelineEntryResponse(
            "ContactAdded",
            contact.CreatedAtUtc,
            null,
            "CustomerContact",
            contact.Id,
            $"{contact.Type} contact added{(contact.Label is null ? "" : $" ({contact.Label})")}",
            CustomerTimelineVisibility.Customer)));

        List<CustomerNote> notes = await dbContext.CustomerNotes
            .AsNoTracking()
            .Where(note => note.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        entries.AddRange(notes.Select(note => new CustomerTimelineEntryResponse(
            "NoteAdded",
            note.CreatedAtUtc,
            note.AuthorUserId.ToString(),
            "CustomerNote",
            note.Id,
            Summarize(note.Body),
            CustomerTimelineVisibility.Internal)));

        List<CustomerAttachment> attachments = await dbContext.CustomerAttachments
            .AsNoTracking()
            .Where(attachment => attachment.CustomerId == customerId && attachment.RemovedAtUtc == null)
            .ToListAsync(cancellationToken);
        entries.AddRange(attachments.Select(attachment => new CustomerTimelineEntryResponse(
            "AttachmentAdded",
            attachment.UploadedAtUtc,
            attachment.UploadedBy,
            "CustomerAttachment",
            attachment.Id,
            attachment.OriginalFileName,
            CustomerTimelineVisibility.Customer)));

        return CustomerTimelineResult.Success([.. entries.OrderBy(entry => entry.OccurredAtUtc)]);
    }

    private static string Summarize(string body) =>
        body.Length <= 120 ? body : string.Concat(body.AsSpan(0, 117), "...");
}
