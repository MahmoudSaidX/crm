import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BrandingService, LocalizationService } from '@squad-crm/platform';
import { AppLayout, ShellMenuItem } from '@squad-crm/shared-ui';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../auth/auth.service';
import { AuthorizationService } from '../auth/authorization.service';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-agent-shell',
  imports: [AppLayout, ButtonModule, AgentLanguageSwitcher],
  templateUrl: './agent-shell.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgentShell {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly authorization = inject(AuthorizationService);
  protected readonly localization = inject(LocalizationService);
  protected readonly branding = inject(BrandingService);
  protected readonly shellTitle = computed(() => {
    const branding = this.branding.value();
    if (branding.isDefault) {
      return this.localization.translate('agent.shell.title');
    }
    return this.localization.locale() === 'ar' && branding.productDisplayNameAr
      ? branding.productDisplayNameAr
      : branding.productDisplayNameEn;
  });

  /**
   * Sakai groups navigation into labelled sections. Each entry keeps the permission
   * that gated it before the redesign, and a group with no visible entry is dropped
   * so an empty section heading never renders.
   */
  protected readonly navigationItems = computed<readonly ShellMenuItem[]>(() => {
    const t = (key: string): string => this.localization.translate(key);
    const has = (permission: string): boolean => this.authorization.state.has(permission);
    const groups: ShellMenuItem[] = [
      {
        label: t('agent.navigation.groups.overview'),
        items: [
          { label: t('agent.navigation.home'), icon: 'pi pi-home', routerLink: '/', exact: true },
        ],
      },
      {
        label: t('agent.navigation.groups.tickets'),
        items: [
          ...(has('tickets.view')
            ? [
                {
                  label: t('agent.navigation.tickets'),
                  icon: 'pi pi-ticket',
                  routerLink: '/tickets',
                },
                {
                  label: t('agent.navigation.myTickets'),
                  icon: 'pi pi-inbox',
                  routerLink: '/my-tickets',
                },
              ]
            : []),
          ...(has('tickets.create')
            ? [
                {
                  label: t('agent.navigation.ticketCreate'),
                  icon: 'pi pi-plus-circle',
                  routerLink: '/tickets/new',
                },
              ]
            : []),
        ],
      },
      {
        label: t('agent.navigation.groups.customers'),
        items: [
          ...(has('customers.manage')
            ? [
                {
                  label: t('agent.navigation.customers'),
                  icon: 'pi pi-users',
                  // Unchanged from the pre-redesign shell: this entry opened the
                  // create form and was gated by customers.manage.
                  routerLink: '/customers/new',
                },
              ]
            : []),
        ],
      },
      {
        label: t('agent.navigation.groups.administration'),
        items: [
          ...(has('roles.view')
            ? [{ label: t('agent.navigation.roles'), icon: 'pi pi-shield', routerLink: '/roles' }]
            : []),
          ...(has('departments.view')
            ? [
                {
                  label: t('agent.navigation.departments'),
                  icon: 'pi pi-sitemap',
                  routerLink: '/departments',
                },
              ]
            : []),
          ...(has('branches.view')
            ? [
                {
                  label: t('agent.navigation.branches'),
                  icon: 'pi pi-building',
                  routerLink: '/branches',
                },
              ]
            : []),
          ...(has('ticketcategories.view')
            ? [
                {
                  label: t('agent.navigation.ticketCategories'),
                  icon: 'pi pi-tags',
                  routerLink: '/ticket-categories',
                },
              ]
            : []),
          ...(has('ticketpriorities.view')
            ? [
                {
                  label: t('agent.navigation.ticketPriorities'),
                  icon: 'pi pi-flag',
                  routerLink: '/ticket-priorities',
                },
              ]
            : []),
          ...(has('users.view')
            ? [
                {
                  label: t('agent.navigation.staffUsers'),
                  icon: 'pi pi-id-card',
                  routerLink: '/staff-users',
                },
              ]
            : []),
          ...(has('configuration.view')
            ? [
                {
                  label: t('agent.navigation.systemConfiguration'),
                  icon: 'pi pi-cog',
                  routerLink: '/system-configuration',
                },
              ]
            : []),
          ...(has('branding.view')
            ? [
                {
                  label: t('agent.navigation.branding'),
                  icon: 'pi pi-palette',
                  routerLink: '/branding',
                },
              ]
            : []),
          ...(has('audit.view')
            ? [{ label: t('agent.navigation.audit'), icon: 'pi pi-history', routerLink: '/audit' }]
            : []),
        ],
      },
    ];

    return groups.filter((group) => (group.items?.length ?? 0) > 0);
  });

  constructor() {
    void this.authorization.load();
    void this.branding.load();
    effect(() => (document.title = this.shellTitle()));
  }

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/login');
  }
}
