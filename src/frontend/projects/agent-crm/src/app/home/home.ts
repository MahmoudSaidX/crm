import { Component, inject } from '@angular/core';
import { APP_CONFIG, LocalizationService } from '@squad-crm/platform';
import { ButtonModule } from 'primeng/button';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

/**
 * Temporary integration smoke marker for CRM-104: it proves PrimeNG + PrimeIcons
 * render and that the locale/direction foundation is wired end to end.
 * TODO(CRM-117): replaced by the real application shell.
 */
@Component({
  selector: 'crm-home',
  imports: [ButtonModule, AgentLanguageSwitcher],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly config = inject(APP_CONFIG);
  protected readonly localization = inject(LocalizationService);

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/login');
  }
}
