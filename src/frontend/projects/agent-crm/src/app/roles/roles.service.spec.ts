import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RolesService } from './roles.service';

describe('RolesService', () => {
  let service: RolesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RolesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists roles with page/pageSize query parameters', async () => {
    const completion = service.list(2, 10);
    const request = http.expectOne((candidate) => candidate.url === '/api/v1/roles');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0 });
    await completion;
  });

  it('gets a role by id', async () => {
    const completion = service.get('role-1');
    const request = http.expectOne('/api/v1/roles/role-1');
    expect(request.request.method).toBe('GET');
    request.flush({
      id: 'role-1',
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('creates a role', async () => {
    const request = { name: 'Sales Manager', code: 'SALES_MANAGER', description: null };
    const completion = service.create(request);
    const httpRequest = http.expectOne('/api/v1/roles');
    expect(httpRequest.request.method).toBe('POST');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush({
      id: 'role-1',
      ...request,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('updates a role', async () => {
    const request = { name: 'Sales Manager', code: 'SALES_MANAGER', description: 'Updated' };
    const completion = service.update('role-1', request);
    const httpRequest = http.expectOne('/api/v1/roles/role-1');
    expect(httpRequest.request.method).toBe('PUT');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush({
      id: 'role-1',
      ...request,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('activates a role', async () => {
    const completion = service.activate('role-1');
    const request = http.expectOne('/api/v1/roles/role-1/activate');
    expect(request.request.method).toBe('POST');
    request.flush({
      id: 'role-1',
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('deactivates a role', async () => {
    const completion = service.deactivate('role-1');
    const request = http.expectOne('/api/v1/roles/role-1/deactivate');
    expect(request.request.method).toBe('POST');
    request.flush({
      id: 'role-1',
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: null,
      isActive: false,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });
});
