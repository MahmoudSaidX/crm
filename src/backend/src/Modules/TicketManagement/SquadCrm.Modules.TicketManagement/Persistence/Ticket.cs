using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.TicketManagement.Events;

namespace SquadCrm.Modules.TicketManagement.Persistence;

public enum TicketStatus
{
    /// <summary>The only status this story produces. Status lifecycle
    /// transitions (assigned/in-progress/resolved/closed/reopened) are a
    /// later story's scope, not built here.</summary>
    Open,
}

/// <summary>
/// Intake channel the ticket was created through (Fields Dictionary, CRM-133).
/// </summary>
public enum TicketChannel
{
    Agent,
    Portal,
    Email,
    WhatsApp,
    LiveChat,
    SMS,
    WebForm,
}

/// <summary>
/// A support ticket (CRM-133, third story of the Ticket Management epic
/// CRM-130). The first real consumer of <see cref="HasDomainEvents"/>: creating
/// a ticket raises <see cref="TicketCreatedDomainEvent"/>, translated by
/// <see cref="TicketManagementOutboxInterceptor"/> into a durable
/// <see cref="OutboxMessage"/> row in the same transaction (ADR-005).
/// <para>
/// <see cref="SubcategoryId"/> is a plain optional FK with no cross-validation
/// — no Subcategory catalog/entity exists in this repo yet (no CRM-131-
/// equivalent "subcategories" story in the backlog). This is a documented
/// scope gap, not silently invented business logic; a later story adds the
/// catalog and its validation.
/// </para>
/// </summary>
public sealed class Ticket : HasDomainEvents
{
    public Guid Id { get; private set; }
    public required string TicketNumber { get; init; }
    public Guid CustomerId { get; private set; }
    public required string Subject { get; set; }
    public required string Description { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid PriorityId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid BranchId { get; set; }
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public TicketChannel Channel { get; set; }
    public Guid? AssignedAgentId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Null until a story that mutates a ticket writes it — no update path
    /// exists yet (assignment is CRM-136, status lifecycle CRM-137). Present
    /// because CRM-135's Fields Dictionary requires the detail view to carry
    /// it; null reads as "never updated" rather than as a fabricated value.
    /// </summary>
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token, starting at 1 and configured as a
    /// concurrency token in the model. CRM-135 only reads and exposes it —
    /// the stories that introduce edit actions (CRM-136 assignment, CRM-137
    /// status lifecycle) are the ones that increment and enforce it. An
    /// explicit column is used rather than the Postgres <c>xmin</c> system
    /// column: Npgsql no longer exposes a first-class mapping for <c>xmin</c>,
    /// and hand-mapping a system column produces a migration that tries to
    /// create it.
    /// </summary>
    public int Version { get; private set; } = 1;

    /// <summary>
    /// The only way to construct a ticket outside EF materialization — ensures
    /// <see cref="TicketCreatedDomainEvent"/> is always raised alongside a new
    /// ticket, never forgotten at a second call site.
    /// </summary>
    public static Ticket Create(
        Guid id,
        string ticketNumber,
        Guid customerId,
        string subject,
        string description,
        Guid categoryId,
        Guid? subcategoryId,
        Guid priorityId,
        Guid departmentId,
        Guid branchId,
        TicketChannel channel,
        Guid? assignedAgentId,
        DateTimeOffset createdAtUtc)
    {
        Ticket ticket = new()
        {
            Id = id,
            TicketNumber = ticketNumber,
            CustomerId = customerId,
            Subject = subject,
            Description = description,
            CategoryId = categoryId,
            SubcategoryId = subcategoryId,
            PriorityId = priorityId,
            DepartmentId = departmentId,
            BranchId = branchId,
            Status = TicketStatus.Open,
            Channel = channel,
            AssignedAgentId = assignedAgentId,
            CreatedAtUtc = createdAtUtc,
        };
        ticket.AddDomainEvent(new TicketCreatedDomainEvent(id, ticketNumber, customerId, createdAtUtc));
        return ticket;
    }

    /// <summary>
    /// Applies an ownership change (CRM-136). The single canonical mutation
    /// for <see cref="AssignedAgentId"/>: manual assignment today and the
    /// automatic-assignment stories (CRM-151/152) both go through here, so the
    /// version bump and <see cref="TicketAssignedDomainEvent"/> can never be
    /// forgotten at a second call site. Eligibility, authorization and the
    /// reason policy are enforced by <c>TicketService</c> before this is
    /// called — this method only applies an already-validated change.
    /// </summary>
    public void Assign(
        Guid targetAgentId,
        string? reason,
        TicketAssignmentSource source,
        DateTimeOffset changedAtUtc)
    {
        Guid? previousAgentId = AssignedAgentId;
        AssignedAgentId = targetAgentId;
        UpdatedAtUtc = changedAtUtc;

        // Explicit increment rather than a database-generated value: EF still
        // writes the ORIGINAL value into the UPDATE's WHERE clause because
        // Version is the configured concurrency token, so a concurrent writer
        // loses the race with a DbUpdateConcurrencyException.
        Version++;

        AddDomainEvent(new TicketAssignedDomainEvent(
            Id, TicketNumber, previousAgentId, targetAgentId, reason, source, changedAtUtc));
    }

    private Ticket()
    {
    }
}
