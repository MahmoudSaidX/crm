import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { TagModule } from 'primeng/tag';
import { Customer, CustomersService } from './customers.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-customer-detail',
  imports: [RouterLink, TagModule, AgentLanguageSwitcher],
  templateUrl: './customer-detail.html',
  styleUrl: './customer-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customersService = inject(CustomersService);
  protected readonly localization = inject(LocalizationService);

  readonly customer = signal<Customer | null>(null);
  readonly loading = signal(false);
  readonly notFound = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      return;
    }

    this.loading.set(true);
    try {
      this.customer.set(await this.customersService.get(id));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.notFound.set(true);
      } else {
        throw error;
      }
    } finally {
      this.loading.set(false);
    }
  }
}
