import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthorizationState {
  private readonly permissionCodes = signal<ReadonlySet<string>>(new Set());
  private readonly loadedState = signal(false);

  readonly loaded = this.loadedState.asReadonly();

  has(code: string): boolean {
    return this.permissionCodes().has(code);
  }

  set(codes: readonly string[]): void {
    this.permissionCodes.set(new Set(codes));
    this.loadedState.set(true);
  }

  clear(): void {
    this.permissionCodes.set(new Set());
    this.loadedState.set(false);
  }
}
