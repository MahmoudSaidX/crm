import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@squad-crm/platform';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { RolesService } from '../roles/roles.service';
import { Role, StaffUser, StaffUsersService } from './staff-users.service';

@Component({
  selector: 'crm-staff-user-roles',
  imports: [FormsModule, ButtonModule, CheckboxModule, MessageModule],
  templateUrl: './staff-user-roles.html',
  styleUrl: './staff-user-roles.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffUserRoles {
  private readonly staffUsersService = inject(StaffUsersService);
  private readonly rolesService = inject(RolesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);
  readonly staffUser = signal<StaffUser | null>(null);
  readonly roles = signal<readonly Role[]>([]);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly saving = signal(false);
  readonly error = signal(false);
  private readonly staffSubjectId = this.route.snapshot.paramMap.get('id');

  constructor() {
    if (this.staffSubjectId) {
      void this.load(this.staffSubjectId);
    }
  }

  toggle(roleId: string, checked: boolean): void {
    const next = new Set(this.selected());
    checked ? next.add(roleId) : next.delete(roleId);
    this.selected.set(next);
  }

  async save(): Promise<void> {
    if (!this.staffSubjectId || this.saving()) return;
    this.saving.set(true);
    this.error.set(false);
    try {
      await this.staffUsersService.replaceRoles(this.staffSubjectId, [...this.selected()]);
      await this.router.navigateByUrl('/staff-users');
    } catch {
      this.error.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(id: string): Promise<void> {
    const [staffUser, allRoles, assignedRoles] = await Promise.all([
      this.staffUsersService.get(id),
      this.rolesService.list(1, 200),
      this.staffUsersService.roles(id),
    ]);
    this.staffUser.set(staffUser);
    this.roles.set(allRoles.items);
    this.selected.set(new Set(assignedRoles.map((role) => role.id)));
  }
}
