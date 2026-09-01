import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { DepartmentForm } from './department-form';
import { DepartmentsService } from './departments.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { DEPARTMENT_TRANSLATIONS } from './department-translations';

describe('DepartmentForm', () => {
  let departmentsService: jasmine.SpyObj<DepartmentsService>;
  let router: jasmine.SpyObj<Router>;

  function configure(paramMap: Record<string, string> = {}): void {
    departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', [
      'get',
      'create',
      'update',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(DEPARTMENT_TRANSLATIONS),
        { provide: DepartmentsService, useValue: departmentsService },
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
    const fixture = TestBed.createComponent(DepartmentForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(departmentsService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.code.touched).toBeTrue();
  });

  it('surfaces a duplicate-code error from a mocked 409 response', async () => {
    configure();
    departmentsService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'departments.duplicate_code' } }),
    );
    const fixture = TestBed.createComponent(DepartmentForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('departments.errors.duplicateCode');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('navigates to the list on successful submit', async () => {
    configure();
    departmentsService.create.and.resolveTo({
      id: 'department-1',
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    const fixture = TestBed.createComponent(DepartmentForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/departments');
  });

  it('loads the existing department when the route carries an id', async () => {
    configure({ id: 'department-1' });
    departmentsService.get.and.resolveTo({
      id: 'department-1',
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: 'Existing description',
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });

    const fixture = TestBed.createComponent(DepartmentForm);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.isEdit()).toBeTrue();
    expect(fixture.componentInstance.form.controls.englishName.value).toBe('Sales');
  });
});
