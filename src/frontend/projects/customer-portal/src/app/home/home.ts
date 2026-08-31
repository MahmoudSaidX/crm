import { Component, inject } from '@angular/core';
import { APP_CONFIG, LocalizationService } from '@squad-crm/platform';
@Component({
  selector: 'portal-home',
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  protected readonly config = inject(APP_CONFIG);
  protected readonly localization = inject(LocalizationService);
}
