import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { TicketPrioritiesService } from './ticket-priorities.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

@Component({
  selector: 'crm-ticket-priority-form',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    MessageModule,
    TextareaModule,
  ],
  templateUrl: './ticket-priority-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketPriorityForm {
  private readonly ticketPrioritiesService = inject(TicketPrioritiesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  private priorityId: string | null = null;
  readonly isEdit = signal(false);
  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);

  readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(64)],
    }),
    arabicName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    englishName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    rank: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(500)],
    }),
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.priorityId = id;
      this.isEdit.set(true);
      void this.loadPriority(id);
    }
  }

  private async loadPriority(id: string): Promise<void> {
    const priority = await this.ticketPrioritiesService.get(id);
    this.form.setValue({
      code: priority.code,
      arabicName: priority.arabicName,
      englishName: priority.englishName,
      rank: priority.rank,
      description: priority.description ?? '',
    });
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorKey.set(null);
    try {
      const raw = this.form.getRawValue();
      const request = {
        code: raw.code,
        arabicName: raw.arabicName,
        englishName: raw.englishName,
        rank: raw.rank,
        description: raw.description.trim() === '' ? null : raw.description,
      };

      if (this.priorityId) {
        await this.ticketPrioritiesService.update(this.priorityId, request);
      } else {
        await this.ticketPrioritiesService.create(request);
      }

      await this.router.navigateByUrl('/ticket-priorities');
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private resolveErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
      return 'common.errors.generic';
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'ticketpriorities.duplicate_code') {
      return 'ticketPriorities.errors.duplicateCode';
    }
    return 'common.errors.generic';
  }
}
