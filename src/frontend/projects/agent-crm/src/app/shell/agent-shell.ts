import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { LocalizationService } from '@squad-crm/platform';
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
    ...(this.authorization.state.has('users.view')
      ? [
          {
            label: this.localization.translate('agent.navigation.staffUsers'),
            icon: 'pi pi-id-card',
            routerLink: '/staff-users',
          },
        ]
      : []),
  ]);

  constructor() {
    void this.authorization.load();
  }

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/login');
  }
}
