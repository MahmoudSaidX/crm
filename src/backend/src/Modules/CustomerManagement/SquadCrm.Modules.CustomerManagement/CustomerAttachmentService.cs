using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

/// <summary>Discriminates why an attachment operation did not succeed.</summary>
public enum CustomerAttachmentFailure
{
    None,
    CustomerNotFound,
    AttachmentNotFound,
}

public readonly record struct CustomerAttachmentResult(
    CustomerAttachment? Attachment, CustomerAttachmentFailure Failure)
{
    public static CustomerAttachmentResult Success(CustomerAttachment attachment) =>
        new(attachment, CustomerAttachmentFailure.None);

    public static CustomerAttachmentResult Failed(CustomerAttachmentFailure failure) => new(null, failure);
}

public readonly record struct CustomerAttachmentContent(
    Stream Content, string ContentType, string OriginalFileName);

internal sealed class CustomerAttachmentService(
    CustomerManagementDbContext dbContext,
    IFileStorage fileStorage,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    public async Task<CustomerAttachmentResult> UploadAsync(
        Guid customerId, FileUpload upload, string? description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);

        if (!await dbContext.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
        {
            return CustomerAttachmentResult.Failed(CustomerAttachmentFailure.CustomerNotFound);
        }

        // Size/type rules are enforced inside the storage abstraction's
        // configured validator; a rejection surfaces as FileValidationException.
        FileReference stored = await fileStorage.UploadAsync(upload, cancellationToken);

        CustomerAttachment attachment = new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            StorageKey = stored.StorageKey,
            OriginalFileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UploadedBy = stored.CreatedBy,
            UploadedAtUtc = stored.CreatedAtUtc,
        };

        dbContext.CustomerAttachments.Add(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("attachment_added", customerId, attachment.Id, cancellationToken);
        return CustomerAttachmentResult.Success(attachment);
    }

    public async Task<List<CustomerAttachment>> ListAsync(Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.CustomerAttachments
            .AsNoTracking()
            .Where(attachment => attachment.CustomerId == customerId && attachment.RemovedAtUtc == null)
            .OrderBy(attachment => attachment.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<CustomerAttachmentContent?> OpenAsync(
        Guid customerId, Guid attachmentId, CancellationToken cancellationToken)
    {
        CustomerAttachment? attachment = await FindActiveAsync(customerId, attachmentId, tracked: false, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        Stream content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        await RecordAuditAsync("attachment_downloaded", customerId, attachment.Id, cancellationToken);
        return new CustomerAttachmentContent(content, attachment.ContentType, attachment.OriginalFileName);
    }

    public async Task<CustomerAttachmentResult> RemoveAsync(
        Guid customerId, Guid attachmentId, CancellationToken cancellationToken)
    {
        CustomerAttachment? attachment = await FindActiveAsync(customerId, attachmentId, tracked: true, cancellationToken);
        if (attachment is null)
        {
            return CustomerAttachmentResult.Failed(CustomerAttachmentFailure.AttachmentNotFound);
        }

        // Logical removal only: audit/historical requirements may prevent
        // physical deletion, so the metadata row and stored bytes are kept.
        attachment.RemovedAtUtc = DateTimeOffset.UtcNow;
        attachment.RemovedBy = currentUserAccessor.Handle ?? "unknown";
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("attachment_removed", customerId, attachment.Id, cancellationToken);
        return CustomerAttachmentResult.Success(attachment);
    }

    private Task<CustomerAttachment?> FindActiveAsync(
        Guid customerId, Guid attachmentId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<CustomerAttachment> query = tracked
            ? dbContext.CustomerAttachments
            : dbContext.CustomerAttachments.AsNoTracking();

        return query.SingleOrDefaultAsync(
            attachment => attachment.Id == attachmentId
                && attachment.CustomerId == customerId
                && attachment.RemovedAtUtc == null,
            cancellationToken);
    }

    private Task RecordAuditAsync(
        string action, Guid customerId, Guid attachmentId, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown",
                action,
                "CustomerAttachment",
                attachmentId.ToString(),
                Metadata: new Dictionary<string, string> { ["customerId"] = customerId.ToString() }),
            cancellationToken);
}
