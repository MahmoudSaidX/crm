import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { BranchList } from './branch-list';
import { BranchesService } from './branches.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { BRANCH_TRANSLATIONS } from './branch-translations';
import { Paginator } from 'primeng/paginator';
import { AuthorizationState } from '../auth/authorization.state';

describe('BranchList', () => {
  const branchA = {
    id: 'branch-a',
    code: 'SALES',
    arabicName: 'المبيعات',
    englishName: 'Sales',
    description: null,
    isActive: true,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };
  const branchB = {
    ...branchA,
    id: 'branch-b',
    englishName: 'Support',
    isActive: false,
  };

  let list: jasmine.SpyObj<BranchesService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    list = jasmine.createSpyObj<BranchesService>('BranchesService', [
      'list',
      'activate',
      'deactivate',
    ]);
    list.list.and.resolveTo({
      items: [branchA, branchB],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    list.activate.and.resolveTo({ ...branchB, isActive: true });
    list.deactivate.and.resolveTo({ ...branchA, isActive: false });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(BRANCH_TRANSLATIONS),
        { provide: BranchesService, useValue: list },
      ],
    });
    TestBed.inject(AppConfigStore).set(
      validateAppConfig({
        apiBaseUrl: 'http://localhost:5080',
        defaultLocale: 'en',
        supportedLocales: ['en', 'ar'],
        appSurface: 'agent-crm',
      }),
    );
    TestBed.inject(LocaleService).initialize();
  });

  afterEach(() => {
    localStorage.removeItem('sc.locale');
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('renders rows from a mocked BranchesService', async () => {
    const fixture = TestBed.createComponent(BranchList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.branches().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20);
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(BranchList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(branchB);

    expect(list.activate).toHaveBeenCalledWith('branch-b');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(BranchList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(branchA);

    expect(list.deactivate).toHaveBeenCalledWith('branch-a');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('localizes feature chrome without translating branch values', async () => {
    TestBed.inject(LocaleService).setLocale('ar');
    const fixture = TestBed.createComponent(BranchList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('الفروع');
    expect(fixture.nativeElement.textContent).toContain('Sales');
    const paginator = fixture.debugElement.query(By.directive(Paginator))
      .componentInstance as Paginator;
    expect(paginator.locale).toBe('ar');
  });

  it('hides management actions until branches.manage is granted', async () => {
    const fixture = TestBed.createComponent(BranchList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Edit');

    TestBed.inject(AuthorizationState).set(['branches.view', 'branches.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });
});
