import { Component, inject } from '@angular/core';
import { APP_CONFIG, LocalizationService } from '@squad-crm/platform';
import { PortalLanguageSwitcher } from '../i18n/portal-language-switcher';

/**
 * Temporary integration smoke marker for CRM-104: it proves PrimeNG + PrimeIcons
 * render and that the locale/direction foundation is wired end to end.
 * TODO(CRM-117): replaced by the real application shell.
 */
@Component({
  selector: 'portal-home',
  imports: [PortalLanguageSwitcher],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  protected readonly config = inject(APP_CONFIG);
  protected readonly localization = inject(LocalizationService);
}
