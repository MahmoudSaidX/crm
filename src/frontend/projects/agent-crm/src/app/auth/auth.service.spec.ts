import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('keeps the access credential in memory and sends the refresh session as a cookie', async () => {
    const completion = auth.signIn({
      email: 'agent@example.test',
      password: 'SyntheticPassword!42',
      rememberSession: true,
    });
    const request = http.expectOne('/api/v1/auth/login');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({ accessToken: 'memory-only-token', expiresAt: '2026-08-29T15:00:00Z' });
    await completion;

    expect(auth.accessToken()).toBe('memory-only-token');
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('accessToken')).toBeNull();
  });

  it('fails closed when refresh is rejected', async () => {
    const completion = auth.restoreSession();
    const request = http.expectOne('/api/v1/auth/refresh');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(await completion).toBeFalse();
    expect(auth.isAuthenticated()).toBeFalse();
  });
});
