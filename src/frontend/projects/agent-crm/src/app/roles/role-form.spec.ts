import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { RoleForm } from './role-form';
import { RolesService } from './roles.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { ROLE_TRANSLATIONS } from './role-translations';

describe('RoleForm', () => {
  let rolesService: jasmine.SpyObj<RolesService>;
  let router: jasmine.SpyObj<Router>;

  function configure(paramMap: Record<string, string> = {}): void {
    rolesService = jasmine.createSpyObj<RolesService>('RolesService', ['get', 'create', 'update']);
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(ROLE_TRANSLATIONS),
        { provide: RolesService, useValue: rolesService },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(paramMap) } },
        },
      ],
    });
  }

  it('blocks submit when required fields are missing', async () => {
    configure();
    const fixture = TestBed.createComponent(RoleForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(rolesService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.name.touched).toBeTrue();
  });

  it('surfaces a duplicate-name error from a mocked 409 response', async () => {
    configure();
    rolesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'roles.duplicate_name' } }),
    );
    const fixture = TestBed.createComponent(RoleForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('roles.errors.duplicateName');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('surfaces a duplicate-code error from a mocked 409 response', async () => {
    configure();
    rolesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'roles.duplicate_code' } }),
    );
    const fixture = TestBed.createComponent(RoleForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('roles.errors.duplicateCode');
  });

  it('navigates to the list on successful submit', async () => {
    configure();
    rolesService.create.and.resolveTo({
      id: 'role-1',
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    const fixture = TestBed.createComponent(RoleForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/roles');
  });

  it('loads the existing role when the route carries an id', async () => {
    configure({ id: 'role-1' });
    rolesService.get.and.resolveTo({
      id: 'role-1',
      name: 'Sales Manager',
      code: 'SALES_MANAGER',
      description: 'Existing description',
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });

    const fixture = TestBed.createComponent(RoleForm);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.isEdit()).toBeTrue();
    expect(fixture.componentInstance.form.controls.name.value).toBe('Sales Manager');
  });
});
