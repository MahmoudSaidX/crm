import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { LocalizationService } from '@squad-crm/platform';
import { ResponsiveShell, ShellNavigationItem } from '@squad-crm/shared-ui';
import { PortalLanguageSwitcher } from '../i18n/portal-language-switcher';

@Component({
  selector: 'portal-shell',
  imports: [ResponsiveShell, PortalLanguageSwitcher],
  templateUrl: './portal-shell.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalShell {
  protected readonly localization = inject(LocalizationService);
  protected readonly navigationItems = computed<readonly ShellNavigationItem[]>(() => [
    {
      label: this.localization.translate('portal.navigation.home'),
      icon: 'pi pi-home',
      routerLink: '/',
      exact: true,
    },
  ]);
}
