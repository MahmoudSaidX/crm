import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationService } from '@squad-crm/platform';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'crm-forbidden',
  imports: [RouterLink, ButtonModule],
  template: `
    <main class="forbidden-page">
      <h1>{{ localization.translate('authorization.forbidden.title') }}</h1>
      <p>{{ localization.translate('authorization.forbidden.message') }}</p>
      <p-button
        routerLink="/"
        [label]="localization.translate('authorization.forbidden.home')"
        icon="pi pi-home"
      />
    </main>
  `,
  styles: `
    .forbidden-page {
      max-width: 42rem;
      margin: 4rem auto;
      padding: 1.5rem;
      text-align: center;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Forbidden {
  protected readonly localization = inject(LocalizationService);
}
