import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DEFAULT_BRANDING, EffectiveBranding } from './branding';

/**
 * Loaded once per app shell from the anonymous effective-branding endpoint.
 * Never throws and never blocks the shell: a failed/invalid load keeps the
 * safe default (BR — invalid/inaccessible branding must not break login or
 * the application shell).
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly branding = signal<EffectiveBranding>(DEFAULT_BRANDING);
  private loading: Promise<void> | null = null;

  readonly value = this.branding.asReadonly();

  load(): Promise<void> {
    this.loading ??= firstValueFrom(this.http.get<EffectiveBranding>('/api/v1/branding/effective'))
      .then((response) => this.branding.set(response))
      .catch(() => this.branding.set(DEFAULT_BRANDING))
      .finally(() => (this.loading = null));
    return this.loading;
  }
}
