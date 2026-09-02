import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';
import {
  BrandingLogoKind,
  BrandingSettings as BrandingSettingsModel,
  BrandingSettingsService,
} from './branding-settings.service';

const LOGO_KINDS: readonly BrandingLogoKind[] = ['primary', 'compact', 'favicon'];

@Component({
  selector: 'crm-branding-settings',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './branding-settings.html',
  styleUrl: './branding-settings.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandingSettings {
  private readonly brandingSettingsService = inject(BrandingSettingsService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  protected readonly logoKinds = LOGO_KINDS;
  readonly settings = signal<BrandingSettingsModel | null>(null);
  readonly submitting = signal(false);
  readonly savedMessage = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);

  readonly form = new FormGroup({
    organizationDisplayNameEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    organizationDisplayNameAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    productDisplayNameEn: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    productDisplayNameAr: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    primaryColor: new FormControl('', { nonNullable: true }),
    accentColor: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    void this.reload();
  }

  private async reload(): Promise<void> {
    const settings = await this.brandingSettingsService.get();
    this.applySettings(settings);
  }

  private applySettings(settings: BrandingSettingsModel): void {
    this.settings.set(settings);
    this.form.setValue({
      organizationDisplayNameEn: settings.organizationDisplayNameEn,
      organizationDisplayNameAr: settings.organizationDisplayNameAr ?? '',
      productDisplayNameEn: settings.productDisplayNameEn,
      productDisplayNameAr: settings.productDisplayNameAr ?? '',
      primaryColor: settings.themeTokens['primaryColor'] ?? '',
      accentColor: settings.themeTokens['accentColor'] ?? '',
    });
  }

  assetFor(kind: BrandingLogoKind): { originalFileName: string } | undefined {
    return this.settings()?.assets.find((asset) => asset.kind.toLowerCase() === kind);
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorKey.set(null);
    this.savedMessage.set(false);
    try {
      const raw = this.form.getRawValue();
      const themeTokens: Record<string, string> = {};
      if (raw.primaryColor.trim() !== '') {
        themeTokens['primaryColor'] = raw.primaryColor.trim();
      }
      if (raw.accentColor.trim() !== '') {
        themeTokens['accentColor'] = raw.accentColor.trim();
      }

      const settings = await this.brandingSettingsService.update({
        organizationDisplayNameEn: raw.organizationDisplayNameEn,
        organizationDisplayNameAr:
          raw.organizationDisplayNameAr.trim() === '' ? null : raw.organizationDisplayNameAr,
        productDisplayNameEn: raw.productDisplayNameEn,
        productDisplayNameAr:
          raw.productDisplayNameAr.trim() === '' ? null : raw.productDisplayNameAr,
        themeTokens,
      });
      this.applySettings(settings);
      this.savedMessage.set(true);
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }

  async onLogoSelected(kind: BrandingLogoKind, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.errorKey.set(null);
    try {
      const settings = await this.brandingSettingsService.uploadLogo(kind, file);
      this.applySettings(settings);
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    }
  }

  async removeLogo(kind: BrandingLogoKind): Promise<void> {
    this.errorKey.set(null);
    try {
      await this.brandingSettingsService.deleteLogo(kind);
      await this.reload();
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    }
  }

  private resolveErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse)) {
      return 'common.errors.generic';
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'branding.invalid_file') {
      return 'branding.errors.invalidFile';
    }
    if (code === 'branding.invalid_theme_token') {
      return 'branding.errors.invalidThemeToken';
    }
    return 'common.errors.generic';
  }
}
