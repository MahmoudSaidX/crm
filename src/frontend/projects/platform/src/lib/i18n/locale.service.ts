import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Direction, SupportedLocale, directionForLocale, isSupportedLocale } from './locale';
import { AppConfigStore } from '../config/app-config.store';

/** localStorage key holding the visitor's locale choice. Stable across releases. */
export const LOCALE_STORAGE_KEY = 'sc.locale';

/**
 * Owns the active locale and the document direction derived from it.
 *
 * Foundation only: this ships no translation dictionaries. CRM-116 owns content.
 */
@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly document = inject(DOCUMENT);
  private readonly configStore = inject(AppConfigStore);

  private readonly current = signal<SupportedLocale>('en');

  readonly locale = this.current.asReadonly();
  readonly direction = computed<Direction>(() => directionForLocale(this.current()));

  /** Locales this deployment allows, from the validated runtime configuration. */
  get supportedLocales(): readonly SupportedLocale[] {
    return this.configStore.require().supportedLocales;
  }

  /**
   * Resolves the startup locale from persisted state, falling back to the runtime
   * default whenever the persisted value is absent, malformed or no longer supported.
   */
  initialize(): void {
    this.setLocale(this.readPersistedLocale() ?? this.configStore.require().defaultLocale);
  }

  /** Applies a locale, persists it and updates `<html lang>` / `<html dir>`. */
  setLocale(locale: SupportedLocale): void {
    if (!this.isAllowed(locale)) {
      throw new Error(`[squad-crm] Locale "${locale}" is not supported by this deployment.`);
    }
    this.current.set(locale);
    this.applyToDocument();
    this.persist(locale);
  }

  private applyToDocument(): void {
    const root = this.document.documentElement;
    root.setAttribute('lang', this.current());
    root.setAttribute('dir', this.direction());
  }

  private isAllowed(locale: unknown): locale is SupportedLocale {
    return isSupportedLocale(locale) && this.supportedLocales.includes(locale);
  }

  private readPersistedLocale(): SupportedLocale | null {
    let persisted: string | null = null;
    try {
      persisted = this.document.defaultView?.localStorage.getItem(LOCALE_STORAGE_KEY) ?? null;
    } catch {
      // Storage can be unavailable (private mode, blocked cookies) — fall back silently.
      return null;
    }
    return this.isAllowed(persisted) ? persisted : null;
  }

  private persist(locale: SupportedLocale): void {
    try {
      this.document.defaultView?.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
    } catch {
      // Persistence is a convenience, not a requirement for correct rendering.
    }
  }
}
