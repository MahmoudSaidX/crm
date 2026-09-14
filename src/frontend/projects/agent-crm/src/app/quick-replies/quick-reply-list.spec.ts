import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { QuickReplyList } from './quick-reply-list';
import { QuickReply, QuickRepliesService } from './quick-replies.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { QUICK_REPLY_TRANSLATIONS } from './quick-reply-translations';
import { Paginator } from 'primeng/paginator';
import { AuthorizationState } from '../auth/authorization.state';

describe('QuickReplyList', () => {
  const personal: QuickReply = {
    id: 'quick-reply-personal',
    name: 'My greeting',
    arabicContent: 'مرحباً',
    englishContent: 'Hello',
    scope: 'Personal',
    ownerUserId: 'user-a',
    isActive: true,
    createdAtUtc: '2026-09-14T00:00:00Z',
    updatedAtUtc: '2026-09-14T00:00:00Z',
  };
  const global: QuickReply = {
    ...personal,
    id: 'quick-reply-global',
    name: 'Shared closing',
    arabicContent: null,
    scope: 'Global',
    ownerUserId: null,
    isActive: false,
  };

  let quickRepliesService: jasmine.SpyObj<QuickRepliesService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    quickRepliesService = jasmine.createSpyObj<QuickRepliesService>('QuickRepliesService', [
      'list',
      'activate',
      'deactivate',
    ]);
    quickRepliesService.list.and.resolveTo({
      items: [personal, global],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    quickRepliesService.activate.and.resolveTo({ ...global, isActive: true });
    quickRepliesService.deactivate.and.resolveTo({ ...personal, isActive: false });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(QUICK_REPLY_TRANSLATIONS),
        { provide: QuickRepliesService, useValue: quickRepliesService },
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

  it('renders rows from a mocked QuickRepliesService', async () => {
    const fixture = TestBed.createComponent(QuickReplyList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.quickReplies().length).toBe(2);
    expect(quickRepliesService.list).toHaveBeenCalledWith(1, 20);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(QuickReplyList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(personal);

    expect(quickRepliesService.deactivate).toHaveBeenCalledWith('quick-reply-personal');
    expect(quickRepliesService.list).toHaveBeenCalledTimes(2);
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(QuickReplyList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(global);

    expect(quickRepliesService.activate).toHaveBeenCalledWith('quick-reply-global');
    expect(quickRepliesService.list).toHaveBeenCalledTimes(2);
  });

  it('offers managing a global template only with the global permission', async () => {
    const fixture = TestBed.createComponent(QuickReplyList);
    const authorization = TestBed.inject(AuthorizationState);

    authorization.set(['quickreplies.view', 'quickreplies.manage']);
    expect(fixture.componentInstance.canManage(personal)).toBeTrue();
    expect(fixture.componentInstance.canManage(global)).toBeFalse();

    authorization.set(['quickreplies.view', 'quickreplies.manageglobal']);
    expect(fixture.componentInstance.canManage(global)).toBeTrue();
    expect(fixture.componentInstance.canManage(personal)).toBeFalse();
  });

  it('hides management actions until a manage permission is granted', async () => {
    const fixture = TestBed.createComponent(QuickReplyList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Edit');

    TestBed.inject(AuthorizationState).set(['quickreplies.view', 'quickreplies.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });

  it('localizes feature chrome without translating template values', async () => {
    TestBed.inject(LocaleService).setLocale('ar');
    const fixture = TestBed.createComponent(QuickReplyList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('الردود السريعة');
    expect(fixture.nativeElement.textContent).toContain('My greeting');
    const paginator = fixture.debugElement.query(By.directive(Paginator))
      .componentInstance as Paginator;
    expect(paginator.locale).toBe('ar');
  });
});
