import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { CustomerDetail } from './customer-detail';
import { CustomersService } from './customers.service';
import {
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
  AppConfigStore,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { CUSTOMER_TRANSLATIONS } from './customer-translations';

describe('CustomerDetail', () => {
  let customersService: jasmine.SpyObj<CustomersService>;

  function configure(id: string | null): void {
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', ['get']);

    TestBed.configureTestingModule({
      providers: [
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(CUSTOMER_TRANSLATIONS),
        { provide: CustomersService, useValue: customersService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
        },
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
  }

  it('renders customer fields on successful load', async () => {
    configure('customer-a');
    customersService.get.and.resolveTo({
      id: 'customer-a',
      customerNumber: 'CUS-AAA111',
      firstName: 'Sara',
      lastName: 'Ahmed',
      preferredLanguage: 'Arabic',
      departmentId: null,
      branchId: null,
      status: 'Active',
      createdAtUtc: '2026-09-02T00:00:00Z',
      updatedAtUtc: '2026-09-02T00:00:00Z',
    });
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(customersService.get).toHaveBeenCalledWith('customer-a');
    expect(fixture.nativeElement.textContent).toContain('Sara Ahmed');
    expect(fixture.nativeElement.textContent).toContain('CUS-AAA111');
  });

  it('shows a not-found message on a 404 response', async () => {
    configure('missing-id');
    customersService.get.and.rejectWith(new HttpErrorResponse({ status: 404 }));
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.notFound()).toBeTrue();
  });
});
