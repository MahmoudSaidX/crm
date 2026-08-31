import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthorizationState } from './authorization.state';

interface CurrentPermissionsResponse {
  readonly permissionCodes: readonly string[];
}

@Injectable({ providedIn: 'root' })
export class AuthorizationService {
  private readonly http = inject(HttpClient);
  readonly state = inject(AuthorizationState);
  private loading: Promise<void> | null = null;

  load(): Promise<void> {
    if (this.state.loaded()) {
      return Promise.resolve();
    }
    this.loading ??= firstValueFrom(
      this.http.get<CurrentPermissionsResponse>('/api/v1/authorization/me'),
    )
      .then((response) => this.state.set(response.permissionCodes))
      .finally(() => (this.loading = null));
    return this.loading;
  }
}
