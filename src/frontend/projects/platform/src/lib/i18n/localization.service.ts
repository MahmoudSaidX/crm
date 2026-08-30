import { Injectable, inject } from '@angular/core';
import { LocaleService } from './locale.service';
import { TRANSLATION_RESOURCES, TranslationDictionary, TranslationKey } from './localization';
import { SupportedLocale } from './locale';

@Injectable({ providedIn: 'root' })
export class LocalizationService {
  private readonly localeService = inject(LocaleService);
  private readonly resources = inject(TRANSLATION_RESOURCES, { optional: true }) ?? [];
  private readonly dictionaries = this.mergeResources();
  private readonly reportedMissingKeys = new Set<TranslationKey>();

  readonly locale = this.localeService.locale;
  readonly direction = this.localeService.direction;

  translate(key: TranslationKey): string {
    const translated = this.dictionaries[this.locale()][key] ?? this.dictionaries.en[key];
    if (translated !== undefined) {
      return translated;
    }

    if (!this.reportedMissingKeys.has(key)) {
      this.reportedMissingKeys.add(key);
      console.warn(`[squad-crm] Missing English translation for "${key}".`);
    }
    return key;
  }

  formatDate(value: Date | number | string, options?: Intl.DateTimeFormatOptions): string {
    const date = typeof value === 'string' ? new Date(value) : value;
    return new Intl.DateTimeFormat(this.locale(), options).format(date);
  }

  formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
    return new Intl.NumberFormat(this.locale(), options).format(value);
  }

  private mergeResources(): Record<SupportedLocale, Record<TranslationKey, string>> {
    const merged: Record<SupportedLocale, Record<TranslationKey, string>> = { en: {}, ar: {} };
    for (const resource of this.resources) {
      this.mergeDictionary(merged.en, resource.en, 'en');
      this.mergeDictionary(merged.ar, resource.ar, 'ar');
    }
    return merged;
  }

  private mergeDictionary(
    target: Record<TranslationKey, string>,
    source: TranslationDictionary,
    locale: SupportedLocale,
  ): void {
    for (const [key, value] of Object.entries(source)) {
      const existing = target[key];
      if (existing !== undefined && existing !== value) {
        throw new Error(`[squad-crm] Conflicting ${locale} translation for "${key}".`);
      }
      target[key] = value;
    }
  }
}
