using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.BuildingBlocks.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.CustomerManagement.Application.Services;
using SquadCrm.Modules.CustomerManagement.Domain.Entities;
using SquadCrm.Modules.CustomerManagement.Presentation.Responses;

namespace SquadCrm.Modules.CustomerManagement.Presentation.Endpoints;

/// <summary>
/// Attachment sub-resource of a customer:
/// <c>/api/v1/customers/{customerId}/attachments</c>.
/// </summary>
internal static class CustomerAttachmentEndpoints
{
    public static void Map(RouteGroupBuilder customers)
    {
        RouteGroupBuilder attachments = customers.MapGroup("/{customerId:guid}/attachments")
            .WithTags("CustomerAttachments");
        attachments.MapPost("", UploadAttachmentAsync)
            .RequireAuthorization(PermissionPolicies.CustomersManage)
            .DisableAntiforgery();
        attachments.MapGet("", ListAttachmentsAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersView);
        attachments.MapGet("/{attachmentId:guid}", DownloadAttachmentAsync)
            .RequireAuthorization(PermissionPolicies.CustomersView);
        attachments.MapDelete("/{attachmentId:guid}", RemoveAttachmentAsync)
            .RequireAuthorization(PermissionPolicies.CustomersManage);
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid customerId,
        IFormFile file,
        string? description,
        CustomerAttachmentService attachmentService,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A non-empty file is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.attachments.file_required" });
        }

        try
        {
            await using Stream content = file.OpenReadStream();
            FileUpload upload = new(
                content, file.FileName, file.ContentType, file.Length, currentUserAccessor.Handle ?? "unknown");
            CustomerAttachmentResult result = await attachmentService.UploadAsync(
                customerId, upload, description, cancellationToken);
            return result.Failure switch
            {
                CustomerAttachmentFailure.None => Results.Created(
                    $"/api/v1/customers/{customerId}/attachments/{result.Attachment!.Id}",
                    ToAttachmentResponse(result.Attachment)),
                CustomerAttachmentFailure.CustomerNotFound => CustomerEndpoints.NotFoundProblem(),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }
        catch (FileValidationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "customers.attachments.invalid_file" });
        }
    }

    private static async Task<IResult> ListAttachmentsAsync(
        Guid customerId, CustomerAttachmentService attachmentService, CancellationToken cancellationToken)
    {
        List<CustomerAttachment> attachments =
            await attachmentService.ListAsync(customerId, cancellationToken);
        return Results.Ok(attachments.Select(ToAttachmentResponse).ToList());
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid customerId,
        Guid attachmentId,
        CustomerAttachmentService attachmentService,
        CancellationToken cancellationToken)
    {
        CustomerAttachmentContent? attachment =
            await attachmentService.OpenAsync(customerId, attachmentId, cancellationToken);
        return attachment is null
            ? AttachmentNotFoundProblem()
            : Results.Stream(
                attachment.Value.Content,
                attachment.Value.ContentType,
                attachment.Value.OriginalFileName);
    }

    private static async Task<IResult> RemoveAttachmentAsync(
        Guid customerId,
        Guid attachmentId,
        CustomerAttachmentService attachmentService,
        CancellationToken cancellationToken)
    {
        CustomerAttachmentResult result = await attachmentService.RemoveAsync(
            customerId, attachmentId, cancellationToken);
        return result.Failure switch
        {
            CustomerAttachmentFailure.None => Results.NoContent(),
            CustomerAttachmentFailure.AttachmentNotFound => AttachmentNotFoundProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult AttachmentNotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Customer attachment not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.attachments.not_found" });

    private static CustomerAttachmentResponse ToAttachmentResponse(CustomerAttachment attachment) => new(
        attachment.Id,
        attachment.CustomerId,
        attachment.OriginalFileName,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.Description,
        attachment.UploadedBy,
        attachment.UploadedAtUtc);
}
