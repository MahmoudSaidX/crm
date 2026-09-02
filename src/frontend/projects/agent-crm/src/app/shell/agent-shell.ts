import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BrandingService, LocalizationService } from '@squad-crm/platform';
import { ResponsiveShell, ShellNavigationItem } from '@squad-crm/shared-ui';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../auth/auth.service';
import { AuthorizationService } from '../auth/authorization.service';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-agent-shell',
  imports: [ResponsiveShell, ButtonModule, AgentLanguageSwitcher],
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
  protected readonly navigationItems = computed<readonly ShellNavigationItem[]>(() => [
    {
      label: this.localization.translate('agent.navigation.home'),
      icon: 'pi pi-home',
      routerLink: '/',
      exact: true,
    },
    ...(this.authorization.state.has('roles.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.roles'),
            icon: 'pi pi-users',
            routerLink: '/roles',
          },
        ]
      : []),
    ...(this.authorization.state.has('departments.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.departments'),
            icon: 'pi pi-sitemap',
            routerLink: '/departments',
          },
        ]
      : []),
    ...(this.authorization.state.has('branches.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.branches'),
            icon: 'pi pi-building',
            routerLink: '/branches',
          },
        ]
      : []),
    ...(this.authorization.state.has('configuration.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.systemConfiguration'),
            icon: 'pi pi-cog',
            routerLink: '/system-configuration',
          },
        ]
      : []),
    ...(this.authorization.state.has('branding.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.branding'),
            icon: 'pi pi-palette',
            routerLink: '/branding',
          },
        ]
      : []),
    ...(this.authorization.state.has('users.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.staffUsers'),
            icon: 'pi pi-id-card',
            routerLink: '/staff-users',
          },
        ]
      : []),
    ...(this.authorization.state.has('audit.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.audit'),
            icon: 'pi pi-history',
            routerLink: '/audit',
          },
        ]
      : []),
  ]);

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
