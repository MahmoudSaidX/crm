import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { APP_CONFIG, LocalizationService } from '@squad-crm/platform';
import { DetailGrid, PageContainer, PageHeader } from '@squad-crm/shared-ui';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'crm-home',
  imports: [CardModule, DetailGrid, PageContainer, PageHeader],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  protected readonly config = inject(APP_CONFIG);
  protected readonly localization = inject(LocalizationService);
}
