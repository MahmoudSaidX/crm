import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuditService } from './audit.service';

describe('AuditService', () => {
  let service: AuditService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuditService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists audit records with page/pageSize and filter query parameters', async () => {
    const completion = service.list(2, 10, {
      entityType: 'StaffSubjectRole',
      action: 'role_assigned',
    });
    const request = http.expectOne((candidate) => candidate.url === '/api/v1/audit-records');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('entityType')).toBe('StaffSubjectRole');
    expect(request.request.params.get('action')).toBe('role_assigned');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0 });
    await completion;
  });

  it('omits filter query parameters that are not supplied', async () => {
    const completion = service.list(1, 20);
    const request = http.expectOne((candidate) => candidate.url === '/api/v1/audit-records');
    expect(request.request.params.has('entityType')).toBeFalse();
    expect(request.request.params.has('action')).toBeFalse();
    expect(request.request.params.has('actorHandle')).toBeFalse();
    request.flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    await completion;
  });

  it('gets an audit record by id', async () => {
    const completion = service.get('1');
    const request = http.expectOne('/api/v1/audit-records/1');
    expect(request.request.method).toBe('GET');
    request.flush({
      id: 1,
      actorHandle: 'bootstrap-tool',
      action: 'role_assigned',
      entityType: 'StaffSubjectRole',
      entityId: 'subject:role',
      metadata: { roleCode: 'ADMIN' },
      occurredAtUtc: '2026-08-31T00:00:00Z',
    });
    await completion;
  });
});
