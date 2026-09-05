using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

/// <summary>Discriminates why a contact mutation did not succeed.</summary>
public enum CustomerContactMutationFailure
{
    None,
    CustomerNotFound,
    ContactNotFound,
    InvalidValue,
    RequiresNewPrimary,
    InvalidNewPrimary,
}

public readonly record struct CustomerContactMutationResult(
    CustomerContact? Contact, CustomerContactMutationFailure Failure)
{
    public static CustomerContactMutationResult Success(CustomerContact contact) => new(contact, CustomerContactMutationFailure.None);
    public static CustomerContactMutationResult Failed(CustomerContactMutationFailure failure) => new(null, failure);
}

internal sealed partial class CustomerContactService(
    CustomerManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    public async Task<CustomerContactMutationResult> AddAsync(
        Guid customerId, AddCustomerContactRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
        {
            return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.CustomerNotFound);
        }

        if (!TryNormalize(request.Type, request.Value, out string normalizedValue))
        {
            return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.InvalidValue);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CustomerContact contact = new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Type = request.Type,
            Value = request.Value.Trim(),
            NormalizedValue = normalizedValue,
            Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
            IsPrimary = request.IsPrimary,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        if (request.IsPrimary)
        {
            await ClearExistingPrimaryAsync(customerId, request.Type, exceptContactId: null, now, cancellationToken);
        }

        dbContext.CustomerContacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(customerId, contact.Id, "contact_added", cancellationToken);
        return CustomerContactMutationResult.Success(contact);
    }

    public async Task<CustomerContactMutationResult> UpdateAsync(
        Guid customerId, Guid contactId, UpdateCustomerContactRequest request, CancellationToken cancellationToken)
    {
        CustomerContact? contact = await dbContext.CustomerContacts.SingleOrDefaultAsync(
            c => c.Id == contactId && c.CustomerId == customerId, cancellationToken);
        if (contact is null)
        {
            return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.ContactNotFound);
        }

        if (!TryNormalize(contact.Type, request.Value, out string normalizedValue))
        {
            return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.InvalidValue);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (request.IsPrimary && !contact.IsPrimary)
        {
            await ClearExistingPrimaryAsync(customerId, contact.Type, exceptContactId: contact.Id, now, cancellationToken);
        }

        contact.Value = request.Value.Trim();
        contact.NormalizedValue = normalizedValue;
        contact.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
        contact.IsPrimary = request.IsPrimary;
        contact.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(customerId, contact.Id, "contact_updated", cancellationToken);
        return CustomerContactMutationResult.Success(contact);
    }

    public async Task<CustomerContactMutationResult> DeactivateAsync(
        Guid customerId, Guid contactId, DeactivateCustomerContactRequest request, CancellationToken cancellationToken)
    {
        CustomerContact? contact = await dbContext.CustomerContacts.SingleOrDefaultAsync(
            c => c.Id == contactId && c.CustomerId == customerId, cancellationToken);
        if (contact is null)
        {
            return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.ContactNotFound);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CustomerContact? newPrimary = null;
        if (contact.IsPrimary)
        {
            List<CustomerContact> remainingActive = await dbContext.CustomerContacts
                .Where(c => c.CustomerId == customerId && c.Type == contact.Type && c.IsActive && c.Id != contact.Id)
                .ToListAsync(cancellationToken);

            if (remainingActive.Count > 0)
            {
                if (request.NewPrimaryContactId is not Guid newPrimaryId)
                {
                    return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.RequiresNewPrimary);
                }

                newPrimary = remainingActive.SingleOrDefault(c => c.Id == newPrimaryId);
                if (newPrimary is null)
                {
                    return CustomerContactMutationResult.Failed(CustomerContactMutationFailure.InvalidNewPrimary);
                }
            }
        }

        contact.IsActive = false;
        contact.IsPrimary = false;
        contact.UpdatedAtUtc = now;

        // Saved separately (before promoting newPrimary) so the old primary
        // row's is_primary flips to false before the new row flips to true —
        // the partial unique index only allows one true+active row per
        // (CustomerId, Type) at a time, and both updates landing in the same
        // batch can be sent to Postgres in either order.
        await dbContext.SaveChangesAsync(cancellationToken);

        if (newPrimary is not null)
        {
            newPrimary.IsPrimary = true;
            newPrimary.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RecordAuditAsync(customerId, contact.Id, "contact_deactivated", cancellationToken);
        return CustomerContactMutationResult.Success(contact);
    }

    public async Task<List<CustomerContact>> ListAsync(Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.CustomerContacts
            .AsNoTracking()
            .Where(contact => contact.CustomerId == customerId)
            .OrderBy(contact => contact.Type)
            .ThenBy(contact => contact.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Clears any existing active primary of <paramref name="type"/> and
    /// saves immediately, in its own statement — the partial unique index
    /// allows only one primary+active row per (CustomerId, Type) at a time,
    /// so this must commit before the caller inserts/promotes the new one in
    /// a later statement, regardless of same-batch statement ordering.
    /// </summary>
    private async Task ClearExistingPrimaryAsync(
        Guid customerId, CustomerContactType type, Guid? exceptContactId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<CustomerContact> existingPrimaries = await dbContext.CustomerContacts
            .Where(c => c.CustomerId == customerId && c.Type == type && c.IsPrimary && c.IsActive && c.Id != exceptContactId)
            .ToListAsync(cancellationToken);
        if (existingPrimaries.Count == 0)
        {
            return;
        }

        foreach (CustomerContact existing in existingPrimaries)
        {
            existing.IsPrimary = false;
            existing.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task RecordAuditAsync(Guid customerId, Guid contactId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown",
                action,
                "CustomerContact",
                contactId.ToString(),
                Metadata: new Dictionary<string, string> { ["customerId"] = customerId.ToString() }),
            cancellationToken);

    private static bool TryNormalize(CustomerContactType type, string value, out string normalizedValue)
    {
        string trimmed = value.Trim();
        if (type == CustomerContactType.Email)
        {
            normalizedValue = trimmed.ToLowerInvariant();
            return EmailPattern().IsMatch(normalizedValue);
        }

        normalizedValue = DigitsOnlyPattern().Replace(trimmed, string.Empty);
        return normalizedValue.Length is >= 7 and <= 15;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex DigitsOnlyPattern();
}
