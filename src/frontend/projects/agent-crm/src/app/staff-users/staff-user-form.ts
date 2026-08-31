import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { StaffUsersService } from './staff-users.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-staff-user-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './staff-user-form.html',
  styleUrl: './staff-user-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffUserForm {
  private readonly staffUsersService = inject(StaffUsersService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  private staffUserId: string | null = null;
  readonly isEdit = signal(false);
  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(320)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8), Validators.maxLength(200)],
    }),
    displayName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    department: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    branch: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.staffUserId = id;
      this.isEdit.set(true);
      this.form.controls.email.disable();
      this.form.controls.password.disable();
      void this.loadStaffUser(id);
    }
  }

  private async loadStaffUser(id: string): Promise<void> {
    const staffUser = await this.staffUsersService.get(id);
    this.form.patchValue({
      email: staffUser.email,
      displayName: staffUser.displayName ?? '',
      department: staffUser.department ?? '',
      branch: staffUser.branch ?? '',
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
      const displayName = raw.displayName.trim() === '' ? null : raw.displayName;
      const department = raw.department.trim() === '' ? null : raw.department;
      const branch = raw.branch.trim() === '' ? null : raw.branch;

      if (this.staffUserId) {
        await this.staffUsersService.update(this.staffUserId, { displayName, department, branch });
      } else {
        await this.staffUsersService.create({
          email: raw.email,
          password: raw.password,
          displayName,
          department,
          branch,
        });
      }

      await this.router.navigateByUrl('/staff-users');
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
    if (code === 'staff_users.duplicate_email') {
      return 'staffUsers.errors.duplicateEmail';
    }
    return 'common.errors.generic';
  }
}
