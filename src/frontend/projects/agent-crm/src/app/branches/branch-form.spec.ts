import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { BranchForm } from './branch-form';
import { BranchesService } from './branches.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { BRANCH_TRANSLATIONS } from './branch-translations';

describe('BranchForm', () => {
  let branchesService: jasmine.SpyObj<BranchesService>;
  let router: jasmine.SpyObj<Router>;

  function configure(paramMap: Record<string, string> = {}): void {
    branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', [
      'get',
      'create',
      'update',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(BRANCH_TRANSLATIONS),
        { provide: BranchesService, useValue: branchesService },
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
    const fixture = TestBed.createComponent(BranchForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(branchesService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.code.touched).toBeTrue();
  });

  it('surfaces a duplicate-code error from a mocked 409 response', async () => {
    configure();
    branchesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'branches.duplicate_code' } }),
    );
    const fixture = TestBed.createComponent(BranchForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('branches.errors.duplicateCode');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('navigates to the list on successful submit', async () => {
    configure();
    branchesService.create.and.resolveTo({
      id: 'branch-1',
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    const fixture = TestBed.createComponent(BranchForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/branches');
  });

  it('loads the existing branch when the route carries an id', async () => {
    configure({ id: 'branch-1' });
    branchesService.get.and.resolveTo({
      id: 'branch-1',
      code: 'SALES',
      arabicName: 'المبيعات',
      englishName: 'Sales',
      description: 'Existing description',
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });

    const fixture = TestBed.createComponent(BranchForm);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.isEdit()).toBeTrue();
    expect(fixture.componentInstance.form.controls.englishName.value).toBe('Sales');
  });
});
