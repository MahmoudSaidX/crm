import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@squad-crm/platform';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { Permission, Role, RolesService } from './roles.service';

@Component({
  selector: 'crm-role-permissions',
  imports: [FormsModule, ButtonModule, CheckboxModule, MessageModule],
  templateUrl: './role-permissions.html',
  styleUrl: './role-permissions.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolePermissions {
  private readonly service = inject(RolesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);
  readonly role = signal<Role | null>(null);
  readonly permissions = signal<readonly Permission[]>([]);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly saving = signal(false);
  readonly error = signal(false);
  readonly modules = computed(() => [...new Set(this.permissions().map((item) => item.module))]);
  private readonly roleId = this.route.snapshot.paramMap.get('id');

  constructor() {
    if (this.roleId) {
      void this.load(this.roleId);
    }
  }

  permissionsFor(module: string): readonly Permission[] {
    return this.permissions().filter((item) => item.module === module);
  }

  toggle(code: string, granted: boolean): void {
    const next = new Set(this.selected());
    if (granted) {
      next.add(code);
    } else {
      next.delete(code);
    }
    this.selected.set(next);
  }

  async save(): Promise<void> {
    if (!this.roleId || this.saving()) return;
    this.saving.set(true);
    this.error.set(false);
    try {
      await this.service.replacePermissions(this.roleId, [...this.selected()]);
      await this.router.navigateByUrl('/roles');
    } catch {
      this.error.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(id: string): Promise<void> {
    const [role, permissions] = await Promise.all([
      this.service.get(id),
      this.service.permissions(id),
    ]);
    this.role.set(role);
    this.permissions.set(permissions);
    this.selected.set(new Set(permissions.filter((item) => item.granted).map((item) => item.code)));
  }
}
