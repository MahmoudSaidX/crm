import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StaffUsersService } from './staff-users.service';

describe('StaffUsersService', () => {
  let service: StaffUsersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(StaffUsersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists staff users with page/pageSize/search query parameters', async () => {
    const completion = service.list(2, 10, 'agent');
    const request = http.expectOne((candidate) => candidate.url === '/api/v1/staff-users');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('search')).toBe('agent');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0 });
    await completion;
  });

  it('gets a staff user by id', async () => {
    const completion = service.get('user-1');
    const request = http.expectOne('/api/v1/staff-users/user-1');
    expect(request.request.method).toBe('GET');
    request.flush({
      id: 'user-1',
      email: 'AGENT@EXAMPLE.TEST',
      displayName: 'Agent One',
      department: null,
      branch: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('creates a staff user', async () => {
    const request = {
      email: 'agent@example.test',
      password: 'P@ssword123',
      displayName: 'Agent One',
      department: null,
      branch: null,
    };
    const completion = service.create(request);
    const httpRequest = http.expectOne('/api/v1/staff-users');
    expect(httpRequest.request.method).toBe('POST');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush({
      id: 'user-1',
      email: request.email,
      displayName: request.displayName,
      department: null,
      branch: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('updates a staff user', async () => {
    const request = { displayName: 'Agent Uno', department: 'Sales', branch: null };
    const completion = service.update('user-1', request);
    const httpRequest = http.expectOne('/api/v1/staff-users/user-1');
    expect(httpRequest.request.method).toBe('PUT');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush({
      id: 'user-1',
      email: 'agent@example.test',
      ...request,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('activates a staff user', async () => {
    const completion = service.activate('user-1');
    const request = http.expectOne('/api/v1/staff-users/user-1/activate');
    expect(request.request.method).toBe('POST');
    request.flush({
      id: 'user-1',
      email: 'agent@example.test',
      displayName: null,
      department: null,
      branch: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('deactivates a staff user', async () => {
    const completion = service.deactivate('user-1');
    const request = http.expectOne('/api/v1/staff-users/user-1/deactivate');
    expect(request.request.method).toBe('POST');
    request.flush({
      id: 'user-1',
      email: 'agent@example.test',
      displayName: null,
      department: null,
      branch: null,
      isActive: false,
      createdAtUtc: '2026-08-29T00:00:00Z',
    });
    await completion;
  });

  it('loads and replaces a staff user role assignment', async () => {
    const load = service.roles('user-1');
    const getRequest = http.expectOne('/api/v1/staff-users/user-1/roles');
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush([]);
    await load;

    const replace = service.replaceRoles('user-1', ['role-1']);
    const putRequest = http.expectOne('/api/v1/staff-users/user-1/roles');
    expect(putRequest.request.method).toBe('PUT');
    expect(putRequest.request.body).toEqual({ roleIds: ['role-1'] });
    putRequest.flush(null);
    await replace;
  });
});
