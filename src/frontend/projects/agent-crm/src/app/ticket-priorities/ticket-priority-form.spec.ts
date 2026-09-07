import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { TicketPriorityForm } from './ticket-priority-form';
import { TicketPrioritiesService } from './ticket-priorities.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_PRIORITY_TRANSLATIONS } from './ticket-priority-translations';

describe('TicketPriorityForm', () => {
  let ticketPrioritiesService: jasmine.SpyObj<TicketPrioritiesService>;
  let router: jasmine.SpyObj<Router>;

  function configure(paramMap: Record<string, string> = {}): void {
    ticketPrioritiesService = jasmine.createSpyObj<TicketPrioritiesService>(
      'TicketPrioritiesService',
      ['get', 'create', 'update'],
    );
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_PRIORITY_TRANSLATIONS),
        { provide: TicketPrioritiesService, useValue: ticketPrioritiesService },
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
    const fixture = TestBed.createComponent(TicketPriorityForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(ticketPrioritiesService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.code.touched).toBeTrue();
  });

  it('surfaces a duplicate-code error from a mocked 409 response', async () => {
    configure();
    ticketPrioritiesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'ticketpriorities.duplicate_code' } }),
    );
    const fixture = TestBed.createComponent(TicketPriorityForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'URGENT',
      arabicName: 'عاجل',
      englishName: 'Urgent',
      rank: 1,
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('ticketPriorities.errors.duplicateCode');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('navigates to the list on successful submit', async () => {
    configure();
    ticketPrioritiesService.create.and.resolveTo({
      id: 'priority-1',
      code: 'URGENT',
      arabicName: 'عاجل',
      englishName: 'Urgent',
      rank: 1,
      description: null,
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });
    const fixture = TestBed.createComponent(TicketPriorityForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      code: 'URGENT',
      arabicName: 'عاجل',
      englishName: 'Urgent',
      rank: 1,
      description: '',
    });

    await fixture.componentInstance.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/ticket-priorities');
  });

  it('loads the existing priority when the route carries an id', async () => {
    configure({ id: 'priority-1' });
    ticketPrioritiesService.get.and.resolveTo({
      id: 'priority-1',
      code: 'URGENT',
      arabicName: 'عاجل',
      englishName: 'Urgent',
      rank: 5,
      description: 'Highest urgency',
      isActive: true,
      createdAtUtc: '2026-08-29T00:00:00Z',
      updatedAtUtc: '2026-08-29T00:00:00Z',
    });

    const fixture = TestBed.createComponent(TicketPriorityForm);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.isEdit()).toBeTrue();
    expect(fixture.componentInstance.form.controls.englishName.value).toBe('Urgent');
    expect(fixture.componentInstance.form.controls.rank.value).toBe(5);
  });
});
