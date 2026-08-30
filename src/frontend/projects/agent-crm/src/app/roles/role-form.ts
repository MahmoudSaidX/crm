import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { RolesService } from './roles.service';

@Component({
  selector: 'crm-role-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, MessageModule, TextareaModule],
  templateUrl: './role-form.html',
  styleUrl: './role-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleForm {
  private readonly rolesService = inject(RolesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private roleId: string | null = null;
  readonly isEdit = signal(false);
  readonly submitting = signal(false);
  readonly duplicateField = signal<'name' | 'code' | null>(null);

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(64)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)],
    }),
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.roleId = id;
      this.isEdit.set(true);
      void this.loadRole(id);
    }
  }

  private async loadRole(id: string): Promise<void> {
    const role = await this.rolesService.get(id);
    this.form.setValue({
      name: role.name,
      code: role.code,
      description: role.description ?? '',
    });
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.duplicateField.set(null);
    try {
      const raw = this.form.getRawValue();
      const request = {
        name: raw.name,
        code: raw.code,
        description: raw.description.trim() === '' ? null : raw.description,
      };

      if (this.roleId) {
        await this.rolesService.update(this.roleId, request);
      } else {
        await this.rolesService.create(request);
      }

      await this.router.navigateByUrl('/roles');
    } catch (error) {
      this.duplicateField.set(this.resolveDuplicateField(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private resolveDuplicateField(error: unknown): 'name' | 'code' | null {
    if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
      return null;
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'roles.duplicate_code') {
      return 'code';
    }
    return 'name';
  }
}
