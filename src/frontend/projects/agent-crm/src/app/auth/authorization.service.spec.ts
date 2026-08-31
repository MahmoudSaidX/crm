import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthorizationService } from './authorization.service';

describe('AuthorizationService', () => {
  it('loads current server grants and exposes exact capability checks', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const service = TestBed.inject(AuthorizationService);
    const http = TestBed.inject(HttpTestingController);

    const completion = service.load();
    const request = http.expectOne('/api/v1/authorization/me');
    expect(request.request.method).toBe('GET');
    request.flush({ permissionCodes: ['roles.view'] });
    await completion;

    expect(service.state.has('roles.view')).toBeTrue();
    expect(service.state.has('roles.manage')).toBeFalse();
    http.verify();
  });
});
