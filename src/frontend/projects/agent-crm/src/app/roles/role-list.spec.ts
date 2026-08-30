import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { RoleList } from './role-list';
import { RolesService } from './roles.service';

describe('RoleList', () => {
  const roleA = {
    id: 'role-a',
    name: 'Sales Manager',
    code: 'SALES_MANAGER',
    description: null,
    isActive: true,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };
  const roleB = { ...roleA, id: 'role-b', name: 'Support Agent', isActive: false };

  let list: jasmine.SpyObj<RolesService>;

  beforeEach(() => {
    list = jasmine.createSpyObj<RolesService>('RolesService', ['list', 'activate', 'deactivate']);
    list.list.and.resolveTo({ items: [roleA, roleB], page: 1, pageSize: 20, totalCount: 2 });
    list.activate.and.resolveTo({ ...roleB, isActive: true });
    list.deactivate.and.resolveTo({ ...roleA, isActive: false });

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: RolesService, useValue: list }],
    });
  });

  it('renders rows from a mocked RolesService', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.roles().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20);
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(roleB);

    expect(list.activate).toHaveBeenCalledWith('role-b');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(roleA);

    expect(list.deactivate).toHaveBeenCalledWith('role-a');
    expect(list.list).toHaveBeenCalledTimes(2);
  });
});
