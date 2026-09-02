import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { BrandingService, LocalizationService } from '@squad-crm/platform';
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
  protected readonly branding = inject(BrandingService);
  protected readonly shellTitle = computed(() => {
    const branding = this.branding.value();
    if (branding.isDefault) {
      return this.localization.translate('portal.shell.title');
    }
    return this.localization.locale() === 'ar' && branding.organizationDisplayNameAr
      ? branding.organizationDisplayNameAr
      : branding.organizationDisplayNameEn;
  });
  protected readonly navigationItems = computed<readonly ShellNavigationItem[]>(() => [
    {
      label: this.localization.translate('portal.navigation.home'),
      icon: 'pi pi-home',
      routerLink: '/',
      exact: true,
    },
  ]);

  constructor() {
    void this.branding.load();
    effect(() => (document.title = this.shellTitle()));
  }
}
