import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LocaleService, LocalizationService } from '@squad-crm/platform';
import { LanguageSwitcher, LanguageSwitcherLocale } from '@squad-crm/shared-ui';

@Component({
  selector: 'portal-language-switcher',
  imports: [LanguageSwitcher],
  template: `
    <sc-language-switcher
      [locale]="localeService.locale()"
      [label]="
        localization.translate(
          localeService.locale() === 'en'
            ? 'common.language.switchToArabic'
            : 'common.language.switchToEnglish'
        )
      "
      [accessibleLabel]="localization.translate('common.language.switcherLabel')"
      (localeChange)="setLocale($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalLanguageSwitcher {
  protected readonly localeService = inject(LocaleService);
  protected readonly localization = inject(LocalizationService);

  protected setLocale(locale: LanguageSwitcherLocale): void {
    this.localeService.setLocale(locale);
  }
}
