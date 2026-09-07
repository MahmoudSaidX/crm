import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { TicketCategoryForm } from './ticket-category-form';
import { TicketCategoriesService } from './ticket-categories.service';
import { DepartmentsService } from '../departments/departments.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_CATEGORY_TRANSLATIONS } from './ticket-category-translations';

describe('TicketCategoryForm', () => {
  let ticketCategoriesService: jasmine.SpyObj<TicketCategoriesService>;
  let departmentsService: jasmine.SpyObj<DepartmentsService>;
  let router: jasmine.SpyObj<Router>;

  function configure(paramMap: Record<string, string> = {}): void {
    ticketCategoriesService = jasmine.createSpyObj<TicketCategoriesService>('TicketCategoriesService', [
      'get',
      'create',
      'update',
    ]);
    departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', ['list']);
    departmentsService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_CATEGORY_TRANSLATIONS),
        { provide: TicketCategoriesService, useValue: ticketCategoriesService },
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
    const fixture = TestBed.createComponent(TicketCategoryForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(ticketCategoriesService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.code.touched).toBeTrue();
  });

  it('surfaces a duplicate-code error from a mocked 409 response', async () => {
    configure();
    ticketCategoriesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'ticketcategories.duplicate_code' } }),
    );
    const fixture = TestBed.createComponent(TicketCategoryForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'BILLING',
      arabicName: 'الفواتير',
      englishName: 'Billing',
      defaultDepartmentId: null,
      sortOrder: 0,
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('ticketCategories.errors.duplicateCode');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('surfaces an inactive-department error from a mocked 409 response', async () => {
    configure();
    ticketCategoriesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'ticketcategories.inactive_department' } }),
    );
    const fixture = TestBed.createComponent(TicketCategoryForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'BILLING',
      arabicName: 'الفواتير',
      englishName: 'Billing',
      defaultDepartmentId: 'department-a',
      sortOrder: 0,
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('ticketCategories.errors.inactiveDepartment');
  });

  it('navigates to the list on successful submit', async () => {
    configure();
    ticketCategoriesService.create.and.resolveTo({
      id: 'category-1',
      code: 'BILLING',
      arabicName: 'الفواتير',
      englishName: 'Billing',
      defaultDepartmentId: null,
      sortOrder: 0,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    const fixture = TestBed.createComponent(TicketCategoryForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'BILLING',
      arabicName: 'الفواتير',
      englishName: 'Billing',
      defaultDepartmentId: null,
      sortOrder: 0,
    });

    await fixture.componentInstance.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/ticket-categories');
  });

  it('loads the existing category when the route carries an id', async () => {
    configure({ id: 'category-1' });
    ticketCategoriesService.get.and.resolveTo({
      id: 'category-1',
      code: 'BILLING',
      arabicName: 'الفواتير',
      englishName: 'Billing',
      defaultDepartmentId: null,
      sortOrder: 5,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });

    const fixture = TestBed.createComponent(TicketCategoryForm);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.isEdit()).toBeTrue();
    expect(fixture.componentInstance.form.controls.englishName.value).toBe('Billing');
    expect(fixture.componentInstance.form.controls.sortOrder.value).toBe(5);
  });
});
