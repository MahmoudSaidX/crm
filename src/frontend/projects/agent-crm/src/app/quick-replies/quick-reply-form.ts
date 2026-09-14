import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { QuickReplyScope, QuickRepliesService } from './quick-replies.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';
import { AuthorizationState } from '../auth/authorization.state';

interface ScopeOption {
  readonly label: string;
  readonly value: QuickReplyScope;
}

/**
 * At least one language must be supplied — the same rule the backend enforces
 * (`quickreplies.content_required`). It is a form-level validator rather than a
 * per-control one because neither field is required on its own.
 */
function atLeastOneLanguage(control: AbstractControl): ValidationErrors | null {
  const arabic = (control.get('arabicContent')?.value ?? '').trim();
  const english = (control.get('englishContent')?.value ?? '').trim();
  return arabic === '' && english === '' ? { contentRequired: true } : null;
}

@Component({
  selector: 'crm-quick-reply-form',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
  ],
  templateUrl: './quick-reply-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuickReplyForm {
  private readonly quickRepliesService = inject(QuickRepliesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  private quickReplyId: string | null = null;
  readonly isEdit = signal(false);
  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);

  /**
   * Offering Global only to holders of the global permission keeps the form
   * honest; the backend rejects the scope regardless, so this is UX, not the
   * control itself.
   */
  readonly scopeOptions = computed<ScopeOption[]>(() => {
    const options: ScopeOption[] = [
      { label: this.localization.translate('quickReplies.scope.personal'), value: 'Personal' },
    ];
    if (this.authorization.has('quickreplies.manageglobal')) {
      options.push({
        label: this.localization.translate('quickReplies.scope.global'),
        value: 'Global',
      });
    }
    return options;
  });

  readonly form = new FormGroup(
    {
      name: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(200)],
      }),
      scope: new FormControl<QuickReplyScope>('Personal', {
        nonNullable: true,
        validators: [Validators.required],
      }),
      arabicContent: new FormControl('', {
        nonNullable: true,
        validators: [Validators.maxLength(4000)],
      }),
      englishContent: new FormControl('', {
        nonNullable: true,
        validators: [Validators.maxLength(4000)],
      }),
    },
    { validators: atLeastOneLanguage },
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.quickReplyId = id;
      this.isEdit.set(true);
      // Scope is immutable after creation (the backend ignores any change), so
      // the control is disabled rather than shown as an editable choice.
      this.form.controls.scope.disable();
      void this.loadQuickReply(id);
    }
  }

  private async loadQuickReply(id: string): Promise<void> {
    const quickReply = await this.quickRepliesService.get(id);
    this.form.setValue({
      name: quickReply.name,
      scope: quickReply.scope,
      arabicContent: quickReply.arabicContent ?? '',
      englishContent: quickReply.englishContent ?? '',
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
      const arabicContent = raw.arabicContent.trim() === '' ? null : raw.arabicContent;
      const englishContent = raw.englishContent.trim() === '' ? null : raw.englishContent;

      if (this.quickReplyId) {
        await this.quickRepliesService.update(this.quickReplyId, {
          name: raw.name,
          arabicContent,
          englishContent,
        });
      } else {
        await this.quickRepliesService.create({
          name: raw.name,
          arabicContent,
          englishContent,
          scope: raw.scope,
        });
      }

      await this.router.navigateByUrl('/quick-replies');
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private resolveErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse)) {
      return 'common.errors.generic';
    }

    switch ((error.error as { code?: string } | null)?.code) {
      case 'quickreplies.duplicate_name':
        return 'quickReplies.errors.duplicateName';
      case 'quickreplies.global_permission_required':
        return 'quickReplies.errors.globalPermissionRequired';
      case 'quickreplies.not_owner':
        return 'quickReplies.errors.notOwner';
      case 'quickreplies.content_required':
        return 'quickReplies.validation.content';
      default:
        return 'common.errors.generic';
    }
  }
}
