import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { TicketDetail } from './ticket-detail';
import { TicketDetail as TicketDetailModel, TicketsService } from './tickets.service';
import { CustomersService } from '../customers/customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_TRANSLATIONS } from './ticket-translations';

describe('TicketDetail', () => {
  const ticket: TicketDetailModel = {
    id: 'ticket-1',
    ticketNumber: 'TKT-000001',
    customerId: 'customer-1',
    subject: 'Cannot log in',
    description: 'The customer cannot log in to the portal.',
    categoryId: 'category-1',
    categoryArabicName: 'فئة',
    categoryEnglishName: 'Category',
    categoryIsActive: true,
    subcategoryId: null,
    priorityId: 'priority-1',
    priorityArabicName: 'أولوية',
    priorityEnglishName: 'Priority',
    priorityIsActive: true,
    priorityRank: 1,
    departmentId: 'department-1',
    branchId: 'branch-1',
    status: 'Open',
    channel: 'Agent',
    assignedAgentId: null,
    createdAtUtc: '2026-09-11T00:00:00Z',
    updatedAtUtc: null,
    version: 1,
  };

  function configure(options: {
    get?: jasmine.Spy;
    customerGet?: jasmine.Spy;
    departmentGet?: jasmine.Spy;
    branchGet?: jasmine.Spy;
  }): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_TRANSLATIONS),
        {
          provide: TicketsService,
          useValue: { get: options.get ?? jasmine.createSpy().and.resolveTo(ticket) },
        },
        {
          provide: CustomersService,
          useValue: {
            get:
              options.customerGet ??
              jasmine.createSpy().and.resolveTo({ firstName: 'Sara', lastName: 'Ali' }),
          },
        },
        {
          provide: DepartmentsService,
          useValue: {
            get:
              options.departmentGet ??
              jasmine.createSpy().and.resolveTo({ arabicName: 'الدعم', englishName: 'Support' }),
          },
        },
        {
          provide: BranchesService,
          useValue: {
            get:
              options.branchGet ??
              jasmine.createSpy().and.resolveTo({ arabicName: 'الرياض', englishName: 'Riyadh' }),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'ticket-1' }) } },
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
    TestBed.inject(LocaleService).initialize();
  }

  async function createComponent() {
    const fixture = TestBed.createComponent(TicketDetail);
    for (let tick = 0; tick < 6; tick += 1) {
      await Promise.resolve();
    }
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => {
    localStorage.removeItem('sc.locale');
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('loads the ticket and resolves reference labels from their owning modules', async () => {
    configure({});
    const fixture = await createComponent();

    expect(fixture.componentInstance.ticket()).toEqual(ticket);
    expect(fixture.componentInstance.notFound()).toBeFalse();
    expect(fixture.componentInstance.customerName()).toBe('Sara Ali');
    expect(fixture.componentInstance.departmentName()).toBe('Support');
    expect(fixture.componentInstance.branchName()).toBe('Riyadh');
    expect(fixture.componentInstance.categoryName()).toBe('Category');
    expect(fixture.componentInstance.priorityName()).toBe('Priority');
  });

  it('sets notFound when the ticket cannot be loaded', async () => {
    configure({ get: jasmine.createSpy().and.rejectWith(new Error('not found')) });
    const fixture = await createComponent();

    expect(fixture.componentInstance.notFound()).toBeTrue();
    expect(fixture.componentInstance.ticket()).toBeNull();
  });

  it('leaves a reference label unresolved when its owning module denies the read', async () => {
    configure({ customerGet: jasmine.createSpy().and.rejectWith(new Error('forbidden')) });
    const fixture = await createComponent();

    // The template falls back to the raw id, and the rest of the ticket still
    // renders — a missing permission must not blank out the whole screen.
    expect(fixture.componentInstance.customerName()).toBeNull();
    expect(fixture.componentInstance.ticket()).toEqual(ticket);
    expect(fixture.componentInstance.departmentName()).toBe('Support');
  });
});
