using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement.DemoData;

/// <summary>
/// CustomerManagement's own demo-data contributor: customer profiles with their
/// contacts and internal notes. Idempotent on
/// <see cref="Customer.CustomerNumber"/> (<c>DEMO-Cnnnn</c>) — an existing demo
/// customer is skipped together with its contacts and notes, so a second run
/// cannot duplicate anything.
/// <para>
/// No attachments are seeded: attachment rows point at bytes in
/// <c>IFileStorage</c>, whose local root belongs to the API container, so
/// metadata written from a host-run CLI would render as a broken download.
/// </para>
/// </summary>
public sealed class CustomerDemoDataContributor : IDemoDataContributor
{
    private static readonly string[] NoteBodies =
    [
        "Customer called about a delayed order and asked for a callback before 4 PM.",
        "العميل يفضل التواصل عبر الرسائل النصية بدل المكالمات.",
        "Verified the registered mobile number during the last support call.",
        "طلب العميل نسخة من الفاتورة الأخيرة عبر البريد الإلكتروني.",
        "Account upgraded to the annual plan; billing questions expected next cycle.",
        "العميل أبلغ عن بطء في التطبيق على أجهزة أندرويد القديمة.",
        "Prefers Arabic correspondence even though the profile language is English.",
        "Escalation history reviewed with the customer; no further action requested.",
    ];

    private static readonly string[] ContactLabels = ["Primary", "Work", "Mobile", "Home"];

    public string Name => "CustomerManagement";

    /// <summary>Customer count for each dataset size.</summary>
    public static int CountFor(DemoDataSize size) => size switch
    {
        DemoDataSize.Small => 20,
        DemoDataSize.Large => 1000,
        _ => 150,
    };

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.References.Departments.Count == 0 || scope.References.Branches.Count == 0)
        {
            throw new InvalidOperationException(
                "Demo departments and branches must be seeded before customers.");
        }

        int target = CountFor(scope.Size);
        if (target > DemoCustomerNames.Capacity)
        {
            throw new InvalidOperationException(
                $"The demo name pool produces {DemoCustomerNames.Capacity} unique pairs, fewer than the "
                + $"{target} customers requested. Extend DemoCustomerNames.");
        }

        await using CustomerManagementDbContext dbContext =
            new CustomerManagementDbContextFactory().CreateDbContext([]);

        HashSet<string> existingNumbers = [.. await dbContext.Customers
            .Where(customer => customer.CustomerNumber.StartsWith(DemoCustomerNumberPrefix))
            .Select(customer => customer.CustomerNumber)
            .ToListAsync(cancellationToken)];

        Random random = new(scope.RandomSeed);
        Guid[] departmentIds = [.. scope.References.Departments.Select(department => department.Id)];
        Guid[] branchIds = [.. scope.References.Branches.Select(branch => branch.Id)];
        Guid[] authorIds = [.. scope.References.Staff.Select(staff => staff.Id)];

        int customersCreated = 0;
        int contactsCreated = 0;
        int notesCreated = 0;

        for (int index = 0; index < target; index++)
        {
            string customerNumber = $"{DemoCustomerNumberPrefix}{index + 1:D4}";
            (string firstName, string lastName) = DemoCustomerNames.At(index);
            Guid departmentId = departmentIds[index % departmentIds.Length];
            Guid branchId = branchIds[index % branchIds.Length];

            // The random sequence is consumed for every customer, present or not,
            // so skipping an existing row cannot shift the data generated for the
            // rows after it.
            int ageInDays = random.Next(0, 90);
            int noteCount = random.Next(0, 4);
            bool hasSecondaryContacts = random.Next(0, 3) == 0;
            int statusRoll = random.Next(0, 100);
            int languageRoll = random.Next(0, 10);
            int phoneSuffix = random.Next(1000000, 9999999);
            int[] noteRolls = [.. Enumerable.Range(0, noteCount).Select(_ => random.Next(0, NoteBodies.Length))];
            int[] noteAgeRolls = [.. Enumerable.Range(0, noteCount).Select(_ => random.Next(0, 90))];

            if (existingNumbers.Contains(customerNumber))
            {
                Guid existingId = await dbContext.Customers
                    .Where(customer => customer.CustomerNumber == customerNumber)
                    .Select(customer => customer.Id)
                    .SingleAsync(cancellationToken);
                scope.References.Customers.Add(
                    new DemoCustomerReference(customerNumber, existingId, departmentId, branchId));
                continue;
            }

            DateTimeOffset createdAtUtc = scope.NowUtc.AddDays(-ageInDays).AddHours(-random.Next(0, 24));
            Customer customer = new()
            {
                Id = Guid.NewGuid(),
                CustomerNumber = customerNumber,
                FirstName = firstName,
                LastName = lastName,
                NormalizedFirstName = Normalize(firstName),
                NormalizedLastName = Normalize(lastName),
                PreferredLanguage = languageRoll switch
                {
                    < 5 => CustomerPreferredLanguage.Arabic,
                    < 9 => CustomerPreferredLanguage.English,
                    _ => null,
                },
                DepartmentId = departmentId,
                BranchId = branchId,
                DepartmentMatchId = departmentId,
                BranchMatchId = branchId,

                // ~85% active / ~15% inactive, so status filters have both sides.
                Status = statusRoll < 85 ? CustomerStatus.Active : CustomerStatus.Inactive,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc,
            };
            dbContext.Customers.Add(customer);
            customersCreated++;

            string emailLocalPart = $"{firstName}.{lastName}.{index + 1}"
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();

            // At most one ACTIVE PRIMARY contact per type per customer — the
            // partial unique index ix_customer_contact_active_primary.
            contactsCreated += AddContact(
                dbContext, customer.Id, CustomerContactType.Email,
                $"demo.{emailLocalPart}@example.invalid", "Primary", isPrimary: true, createdAtUtc);
            contactsCreated += AddContact(
                dbContext, customer.Id, CustomerContactType.Phone,
                $"+9665{phoneSuffix:D7}", "Primary", isPrimary: true, createdAtUtc);

            if (hasSecondaryContacts)
            {
                contactsCreated += AddContact(
                    dbContext, customer.Id, CustomerContactType.Email,
                    $"demo.{emailLocalPart}.work@example.invalid",
                    ContactLabels[index % ContactLabels.Length], isPrimary: false, createdAtUtc);
                contactsCreated += AddContact(
                    dbContext, customer.Id, CustomerContactType.Phone,
                    $"+9664{phoneSuffix:D7}",
                    ContactLabels[(index + 1) % ContactLabels.Length], isPrimary: false, createdAtUtc);
            }

            for (int note = 0; note < noteCount; note++)
            {
                DateTimeOffset noteAtUtc = scope.NowUtc.AddDays(-Math.Min(noteAgeRolls[note], ageInDays));
                dbContext.CustomerNotes.Add(new CustomerNote
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    Body = NoteBodies[noteRolls[note]],
                    AuthorUserId = authorIds[(index + note) % authorIds.Length],
                    CreatedAtUtc = noteAtUtc < createdAtUtc ? createdAtUtc : noteAtUtc,
                });
                notesCreated++;
            }

            scope.References.Customers.Add(
                new DemoCustomerReference(customerNumber, customer.Id, departmentId, branchId));

            // Batched so the large dataset does not build one enormous change set.
            if (customersCreated % 200 == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["customers created"] = customersCreated,
                ["customers already present"] = target - customersCreated,
                ["contacts created"] = contactsCreated,
                ["notes created"] = notesCreated,
            });
    }

    internal const string DemoCustomerNumberPrefix = "DEMO-C";

    private static int AddContact(
        CustomerManagementDbContext dbContext,
        Guid customerId,
        CustomerContactType type,
        string value,
        string label,
        bool isPrimary,
        DateTimeOffset createdAtUtc)
    {
        dbContext.CustomerContacts.Add(new CustomerContact
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Type = type,
            Value = value,
            // Same normalization CustomerContactService.TryNormalize applies:
            // lowercase for email, digits only for phone.
            NormalizedValue = type == CustomerContactType.Email
                ? value.Trim().ToLowerInvariant()
                : new string([.. value.Where(char.IsDigit)]),
            Label = label,
            IsPrimary = isPrimary,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        });
        return 1;
    }

    /// <summary>Same normalization rule <c>CustomerService</c> applies.</summary>
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
