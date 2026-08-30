import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

export type LanguageSwitcherLocale = 'en' | 'ar';

@Component({
  selector: 'sc-language-switcher',
  imports: [ButtonModule],
  templateUrl: './language-switcher.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitcher {
  readonly locale = input.required<LanguageSwitcherLocale>();
  readonly label = input.required<string>();
  readonly accessibleLabel = input.required<string>();
  readonly localeChange = output<LanguageSwitcherLocale>();

  protected switchLanguage(): void {
    this.localeChange.emit(this.locale() === 'en' ? 'ar' : 'en');
  }
}
