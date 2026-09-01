import { DatePipe, KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuditRecord, AuditService } from './audit.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

/**
 * Read-only detail view: no form, no edit — audit records are append-only
 * (Story CRM-114). No PrimeNG Card/detail-screen precedent existed elsewhere
 * in this app at the time this was written, so a plain definition-list
 * layout is used instead of introducing a new, unvetted component.
 */
@Component({
  selector: 'crm-audit-detail',
  imports: [RouterLink, ButtonModule, DatePipe, KeyValuePipe, AgentLanguageSwitcher],
  templateUrl: './audit-detail.html',
  styleUrl: './audit-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditDetail {
  private readonly auditService = inject(AuditService);
  private readonly route = inject(ActivatedRoute);
  protected readonly localization = inject(LocalizationService);
  readonly auditRecord = signal<AuditRecord | null>(null);
  readonly notFound = signal(false);
  private readonly id = this.route.snapshot.paramMap.get('id');

  constructor() {
    if (this.id) {
      void this.load(this.id);
    }
  }

  private async load(id: string): Promise<void> {
    try {
      this.auditRecord.set(await this.auditService.get(id));
    } catch {
      this.notFound.set(true);
    }
  }
}
