using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

public sealed class CustomerManagementModule : IModule
{
    // The "customers.manage" policy is centrally owned and registered by
    // RoleManagementModule (the permission catalog's single home), following
    // the same precedent as AuditModule's AuditViewPolicy: referenced here by
    // string only, no project reference to RoleManagement.
    public string Name => "CustomerManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CustomerManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    CustomerManagementSchema.MigrationsHistoryTable,
                    CustomerManagementSchema.Name)));
        services.AddScoped<CustomerService>();
        services.AddScoped<CustomerContactService>();
        services.AddScoped<CustomerNoteService>();
        services.AddScoped<CustomerAttachmentService>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // IDepartmentActiveLookup/IBranchActiveLookup are already registered
        // by DepartmentManagementModule/BranchManagementModule; DI resolves
        // those same registrations. No duplicate registration and no project
        // reference to those modules' main projects is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder customers = endpoints.MapGroup("/api/v1/customers").WithTags("Customers");

        customers.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateCustomerRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        customers.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.CustomersView);
        customers.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.CustomersView);
        customers.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateCustomerRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);

        RouteGroupBuilder contacts = customers.MapGroup("/{customerId:guid}/contacts").WithTags("CustomerContacts");
        contacts.MapPost("", AddContactAsync).ValidatesDataAnnotations<AddCustomerContactRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        contacts.MapGet("", ListContactsAsync).RequireAuthorization(PermissionPolicies.CustomersView);
        contacts.MapPut("/{contactId:guid}", UpdateContactAsync).ValidatesDataAnnotations<UpdateCustomerContactRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        contacts.MapPost("/{contactId:guid}/deactivate", DeactivateContactAsync)
            .RequireAuthorization(PermissionPolicies.CustomersManage);

        RouteGroupBuilder notes = customers.MapGroup("/{customerId:guid}/notes").WithTags("CustomerNotes");
        notes.MapPost("", AddNoteAsync).ValidatesDataAnnotations<AddCustomerNoteRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        notes.MapGet("", ListNotesAsync).RequireAuthorization(PermissionPolicies.CustomersView);

        RouteGroupBuilder attachments = customers.MapGroup("/{customerId:guid}/attachments")
            .WithTags("CustomerAttachments");
        attachments.MapPost("", UploadAttachmentAsync)
            .RequireAuthorization(PermissionPolicies.CustomersManage)
            .DisableAntiforgery();
        attachments.MapGet("", ListAttachmentsAsync).RequireAuthorization(PermissionPolicies.CustomersView);
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
                CustomerAttachmentFailure.CustomerNotFound => NotFoundProblem(),
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
        List<Persistence.CustomerAttachment> attachments =
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

    private static CustomerAttachmentResponse ToAttachmentResponse(Persistence.CustomerAttachment attachment) => new(
        attachment.Id,
        attachment.CustomerId,
        attachment.OriginalFileName,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.Description,
        attachment.UploadedBy,
        attachment.UploadedAtUtc);

    private static async Task<IResult> AddNoteAsync(
        Guid customerId,
        AddCustomerNoteRequest request,
        CustomerNoteService noteService,
        CancellationToken cancellationToken)
    {
        CustomerNoteMutationResult result = await noteService.AddAsync(customerId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerNoteMutationFailure.None => Results.Created(
                $"/api/v1/customers/{customerId}/notes/{result.Note!.Id}", ToNoteResponse(result.Note)),
            CustomerNoteMutationFailure.CustomerNotFound => NotFoundProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListNotesAsync(
        Guid customerId, CustomerNoteService noteService, CancellationToken cancellationToken)
    {
        List<Persistence.CustomerNote> notes = await noteService.ListAsync(customerId, cancellationToken);
        return Results.Ok(notes.Select(ToNoteResponse).ToList());
    }

    private static CustomerNoteResponse ToNoteResponse(Persistence.CustomerNote note) => new(
        note.Id,
        note.CustomerId,
        note.Body,
        note.AuthorUserId,
        note.CreatedAtUtc);

    private static async Task<IResult> AddContactAsync(
        Guid customerId,
        AddCustomerContactRequest request,
        CustomerContactService contactService,
        CancellationToken cancellationToken)
    {
        CustomerContactMutationResult result = await contactService.AddAsync(customerId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerContactMutationFailure.None => Results.Created(
                $"/api/v1/customers/{customerId}/contacts/{result.Contact!.Id}", ToContactResponse(result.Contact)),
            CustomerContactMutationFailure.CustomerNotFound => NotFoundProblem(),
            CustomerContactMutationFailure.InvalidValue => InvalidContactValueProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListContactsAsync(
        Guid customerId, CustomerContactService contactService, CancellationToken cancellationToken)
    {
        List<Persistence.CustomerContact> contacts = await contactService.ListAsync(customerId, cancellationToken);
        return Results.Ok(contacts.Select(ToContactResponse).ToList());
    }

    private static async Task<IResult> UpdateContactAsync(
        Guid customerId,
        Guid contactId,
        UpdateCustomerContactRequest request,
        CustomerContactService contactService,
        CancellationToken cancellationToken)
    {
        CustomerContactMutationResult result = await contactService.UpdateAsync(customerId, contactId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerContactMutationFailure.None => Results.Ok(ToContactResponse(result.Contact!)),
            CustomerContactMutationFailure.ContactNotFound => NotFoundProblem(),
            CustomerContactMutationFailure.InvalidValue => InvalidContactValueProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> DeactivateContactAsync(
        Guid customerId,
        Guid contactId,
        DeactivateCustomerContactRequest request,
        CustomerContactService contactService,
        CancellationToken cancellationToken)
    {
        CustomerContactMutationResult result = await contactService.DeactivateAsync(customerId, contactId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerContactMutationFailure.None => Results.Ok(ToContactResponse(result.Contact!)),
            CustomerContactMutationFailure.ContactNotFound => NotFoundProblem(),
            CustomerContactMutationFailure.RequiresNewPrimary => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Another active contact of this type must be designated as primary first.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.contacts.requires_new_primary" }),
            CustomerContactMutationFailure.InvalidNewPrimary => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected replacement primary contact is not valid.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.contacts.invalid_new_primary" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult InvalidContactValueProblem() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "The contact value is not valid for the selected type.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.contacts.invalid_value" });

    private static CustomerContactResponse ToContactResponse(Persistence.CustomerContact contact) => new(
        contact.Id,
        contact.CustomerId,
        contact.Type,
        contact.Value,
        contact.Label,
        contact.IsPrimary,
        contact.IsActive,
        contact.CreatedAtUtc,
        contact.UpdatedAtUtc);

    private static async Task<IResult> ListAsync(
        [AsParameters] CustomerListQuery query,
        [AsParameters] PaginationRequest pagination,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        PagedResult<Customer> page = await customerService.ListAsync(query, pagination, cancellationToken);
        return Results.Ok(new PagedResult<CustomerResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id, CustomerService customerService, CancellationToken cancellationToken)
    {
        Customer? customer = await customerService.GetAsync(id, cancellationToken);
        return customer is null ? NotFoundProblem() : Results.Ok(ToResponse(customer));
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Customer not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.not_found" });

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        CustomerMutationResult result = await customerService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            CustomerMutationFailure.None => Results.Created(
                $"/api/v1/customers/{result.Customer!.Id}", ToResponse(result.Customer)),
            CustomerMutationFailure.DuplicateCustomer => DuplicateProblem(),
            CustomerMutationFailure.InactiveDepartment => InactiveReferenceProblem("department"),
            CustomerMutationFailure.InactiveBranch => InactiveReferenceProblem("branch"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        CustomerMutationResult result = await customerService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            CustomerMutationFailure.None => Results.Ok(ToResponse(result.Customer!)),
            CustomerMutationFailure.NotFound => NotFoundProblem(),
            CustomerMutationFailure.InactiveDepartment => InactiveReferenceProblem("department"),
            CustomerMutationFailure.InactiveBranch => InactiveReferenceProblem("branch"),
            CustomerMutationFailure.ConcurrencyConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This customer was updated by someone else. Reload and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.update_conflict" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult DuplicateProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A matching customer already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.duplicate_customer" });

    private static IResult InactiveReferenceProblem(string reference) => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: $"The selected {reference} is not active.",
        extensions: new Dictionary<string, object?> { ["code"] = $"customers.inactive_{reference}" });

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.CustomerNumber,
        customer.FirstName,
        customer.LastName,
        customer.PreferredLanguage,
        customer.DepartmentId,
        customer.BranchId,
        customer.Status,
        customer.Version,
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc);
}
