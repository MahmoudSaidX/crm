import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';
import { ConfigurationValue, SystemConfigurationService } from './system-configuration.service';

@Component({
  selector: 'crm-system-configuration-list',
  imports: [
    FormsModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    TableModule,
    TagModule,
    ToggleSwitchModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './system-configuration-list.html',
  styleUrl: './system-configuration-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SystemConfigurationList {
  private readonly service = inject(SystemConfigurationService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  readonly values = signal<ConfigurationValue[]>([]);
  readonly loading = signal(false);
  readonly editingKey = signal<string | null>(null);
  readonly editText = signal('');
  readonly editNumber = signal<number | null>(null);
  readonly editBoolean = signal(false);
  readonly saving = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.values.set(await this.service.list());
    } finally {
      this.loading.set(false);
    }
  }

  displayName(item: ConfigurationValue): string {
    return this.localization.locale() === 'ar' ? item.displayNameAr : item.displayNameEn;
  }

  description(item: ConfigurationValue): string | null {
    return this.localization.locale() === 'ar' ? item.descriptionAr : item.descriptionEn;
  }

  startEdit(item: ConfigurationValue): void {
    this.errorKey.set(null);
    this.editingKey.set(item.key);
    const current = item.isSensitive ? item.defaultValue : (item.value ?? item.defaultValue);
    this.editText.set(item.isSensitive ? '' : current);
    this.editNumber.set(item.valueType === 'Number' ? Number(current) : null);
    this.editBoolean.set(current === 'true');
  }

  cancelEdit(): void {
    this.editingKey.set(null);
    this.errorKey.set(null);
  }

  async save(item: ConfigurationValue): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.errorKey.set(null);
    try {
      const value =
        item.valueType === 'Boolean'
          ? String(this.editBoolean())
          : item.valueType === 'Number'
            ? String(this.editNumber() ?? '')
            : this.editText();
      const updated = await this.service.update(item.key, value);
      this.values.set(
        this.values().map((candidate) => (candidate.key === item.key ? updated : candidate)),
      );
      this.editingKey.set(null);
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    } finally {
      this.saving.set(false);
    }
  }

  private resolveErrorKey(error: unknown): TranslationKey {
    if (error instanceof HttpErrorResponse && error.status === 422) {
      return 'systemConfiguration.errors.invalidValue';
    }
    return 'common.errors.generic';
  }
}
