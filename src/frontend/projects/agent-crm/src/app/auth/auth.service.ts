import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface AccessCredentialResponse {
  readonly accessToken: string;
  readonly expiresAt: string;
}

export interface SignInCredentials {
  readonly email: string;
  readonly password: string;
  readonly rememberSession: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly accessTokenState = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.accessTokenState() !== null);

  accessToken(): string | null {
    return this.accessTokenState();
  }

  async signIn(credentials: SignInCredentials): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AccessCredentialResponse>('/api/v1/auth/login', credentials, {
        withCredentials: true,
      }),
    );
    this.accessTokenState.set(response.accessToken);
  }

  async restoreSession(): Promise<boolean> {
    if (this.isAuthenticated()) {
      return true;
    }

    try {
      const response = await firstValueFrom(
        this.http.post<AccessCredentialResponse>(
          '/api/v1/auth/refresh',
          {},
          { withCredentials: true },
        ),
      );
      this.accessTokenState.set(response.accessToken);
      return true;
    } catch {
      this.accessTokenState.set(null);
      return false;
    }
  }

  async signOut(): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post('/api/v1/auth/logout', {}, { withCredentials: true, responseType: 'text' }),
      );
    } finally {
      this.accessTokenState.set(null);
    }
  }

  clear(): void {
    this.accessTokenState.set(null);
  }
}
