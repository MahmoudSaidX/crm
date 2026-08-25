import { Component, inject } from '@angular/core';
import { APP_CONFIG, LocaleService, SupportedLocale } from '@squad-crm/platform';
import { ButtonModule } from 'primeng/button';

/**
 * Temporary integration smoke marker for CRM-104: it proves PrimeNG + PrimeIcons
 * render and that the locale/direction foundation is wired end to end.
 * TODO(CRM-117): replaced by the real application shell.
 */
@Component({
  selector: 'crm-home',
  imports: [ButtonModule],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  private readonly localeService = inject(LocaleService);

  protected readonly config = inject(APP_CONFIG);
  protected readonly locale = this.localeService.locale;
  protected readonly direction = this.localeService.direction;

  protected toggleLocale(): void {
    const next: SupportedLocale = this.locale() === 'en' ? 'ar' : 'en';
    this.localeService.setLocale(next);
  }
}
