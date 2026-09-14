using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.RoleManagement.Application.Services;
using SquadCrm.Modules.RoleManagement.Infrastructure.Authorization;
using SquadCrm.Modules.RoleManagement.Infrastructure.Persistence;

using SquadCrm.Modules.RoleManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.RoleManagement;

public sealed class RoleManagementModule : IModule
{
    public string Name => "RoleManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RoleManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    RoleManagementSchema.MigrationsHistoryTable,
                    RoleManagementSchema.Name)));
        services.AddScoped<RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<AuthorizationBootstrapService>();
        services.AddScoped<StaffRoleAssignmentService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PermissionPolicies.RolesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.RolesView)));
            options.AddPolicy(PermissionPolicies.RolesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.RolesManage)));
            options.AddPolicy(PermissionPolicies.UsersView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.UsersView)));
            options.AddPolicy(PermissionPolicies.UsersManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.UsersManage)));
            options.AddPolicy(PermissionPolicies.AuditView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.AuditView)));
            options.AddPolicy(PermissionPolicies.DepartmentsView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.DepartmentsView)));
            options.AddPolicy(PermissionPolicies.DepartmentsManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.DepartmentsManage)));
            options.AddPolicy(PermissionPolicies.ConfigurationView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.ConfigurationView)));
            options.AddPolicy(PermissionPolicies.ConfigurationManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.ConfigurationManage)));
            options.AddPolicy(PermissionPolicies.BranchesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.BranchesView)));
            options.AddPolicy(PermissionPolicies.BranchesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.BranchesManage)));
            options.AddPolicy(PermissionPolicies.BrandingView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.BrandingView)));
            options.AddPolicy(PermissionPolicies.BrandingManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.BrandingManage)));
            options.AddPolicy(PermissionPolicies.CustomersView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.CustomersView)));
            options.AddPolicy(PermissionPolicies.CustomersManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.CustomersManage)));
            options.AddPolicy(PermissionPolicies.TicketCategoriesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketCategoriesView)));
            options.AddPolicy(PermissionPolicies.TicketCategoriesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketCategoriesManage)));
            options.AddPolicy(PermissionPolicies.TicketPrioritiesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketPrioritiesView)));
            options.AddPolicy(PermissionPolicies.TicketPrioritiesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketPrioritiesManage)));
            options.AddPolicy(PermissionPolicies.TicketsCreate, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketsCreate)));
            options.AddPolicy(PermissionPolicies.TicketsView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketsView)));
            options.AddPolicy(PermissionPolicies.TicketsAssign, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketsAssign)));
            options.AddPolicy(PermissionPolicies.TicketsChangeStatus, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketsChangeStatus)));
            options.AddPolicy(PermissionPolicies.TicketsEscalate, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketsEscalate)));
            options.AddPolicy(PermissionPolicies.TicketsCollaborate, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TicketsCollaborate)));
            options.AddPolicy(PermissionPolicies.TasksView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TasksView)));
            options.AddPolicy(PermissionPolicies.TasksCreate, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TasksCreate)));
            options.AddPolicy(PermissionPolicies.TasksEdit, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TasksEdit)));
            options.AddPolicy(PermissionPolicies.TasksComplete, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.TasksComplete)));
            options.AddPolicy(PermissionPolicies.QuickRepliesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.QuickRepliesView)));
            options.AddPolicy(PermissionPolicies.QuickRepliesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.QuickRepliesManage)));
            options.AddPolicy(PermissionPolicies.QuickRepliesManageGlobal, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.QuickRepliesManageGlobal)));
        });

        // ICurrentUserAccessor is already registered by StaffIdentityModule
        // (RegisterServices order in Program.cs runs StaffIdentity first); DI
        // resolves that same registration. No duplicate registration and no
        // project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        RoleManagementEndpoints.Map(endpoints);
}
