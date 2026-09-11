import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationService } from '@squad-crm/platform';
import { ButtonModule } from 'primeng/button';
import { PageContainer, PageHeader, StatePanel } from '@squad-crm/shared-ui';

@Component({
  selector: 'crm-forbidden',
  imports: [RouterLink, ButtonModule, PageContainer, PageHeader, StatePanel],
  template: `
    <sc-page-container>
      <sc-page-header [title]="localization.translate('authorization.forbidden.title')" />
      <sc-state-panel
        state="error"
        icon="pi pi-lock"
        [message]="localization.translate('authorization.forbidden.message')"
      >
        <p-button
          actions
          routerLink="/"
          [label]="localization.translate('authorization.forbidden.home')"
          icon="pi pi-home"
        />
      </sc-state-panel>
    </sc-page-container>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Forbidden {
  protected readonly localization = inject(LocalizationService);
}
