import { Injectable, signal } from '@angular/core';
import { AppConfig } from './app-config';
import { AppConfigError } from './validate-app-config';

/**
 * Holds the runtime configuration between the bootstrap initializer that loads it
 * and the injector consumers that read it.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigStore {
  private readonly config = signal<AppConfig | null>(null);

  readonly value = this.config.asReadonly();

  set(config: AppConfig): void {
    this.config.set(config);
  }

  /** Returns the loaded configuration or throws — never returns a half-initialised value. */
  require(): AppConfig {
    const current = this.config();
    if (current === null) {
      throw new AppConfigError('configuration was read before bootstrap finished loading it.');
    }
    return current;
  }
}
