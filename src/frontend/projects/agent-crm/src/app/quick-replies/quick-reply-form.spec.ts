import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { QuickReplyForm } from './quick-reply-form';
import { QuickReply, QuickRepliesService } from './quick-replies.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { QUICK_REPLY_TRANSLATIONS } from './quick-reply-translations';
import { AuthorizationState } from '../auth/authorization.state';

describe('QuickReplyForm', () => {
  const existing: QuickReply = {
    id: 'quick-reply-a',
    name: 'My greeting',
    arabicContent: 'مرحباً',
    englishContent: 'Hello',
    scope: 'Personal',
    ownerUserId: 'user-a',
    isActive: true,
    createdAtUtc: '2026-09-14T00:00:00Z',
    updatedAtUtc: '2026-09-14T00:00:00Z',
  };

  let quickRepliesService: jasmine.SpyObj<QuickRepliesService>;
  let router: jasmine.SpyObj<Router>;

  function configure(paramMap: Record<string, string> = {}): void {
    quickRepliesService = jasmine.createSpyObj<QuickRepliesService>('QuickRepliesService', [
      'get',
      'create',
      'update',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(QUICK_REPLY_TRANSLATIONS),
        { provide: QuickRepliesService, useValue: quickRepliesService },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(paramMap) } },
        },
      ],
    });
  }

  it('blocks submit when the name is missing', async () => {
    configure();
    const fixture = TestBed.createComponent(QuickReplyForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(quickRepliesService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.name.touched).toBeTrue();
  });

  it('blocks submit when neither Arabic nor English content is supplied', async () => {
    configure();
    const fixture = TestBed.createComponent(QuickReplyForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Greeting',
      scope: 'Personal',
      arabicContent: '   ',
      englishContent: '',
    });

    await fixture.componentInstance.submit();

    expect(quickRepliesService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.hasError('contentRequired')).toBeTrue();
  });

  it('creates with a single language and sends the empty one as null', async () => {
    configure();
    quickRepliesService.create.and.resolveTo(existing);
    const fixture = TestBed.createComponent(QuickReplyForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Greeting',
      scope: 'Personal',
      arabicContent: '',
      englishContent: 'Hello',
    });

    await fixture.componentInstance.submit();

    expect(quickRepliesService.create).toHaveBeenCalledWith({
      name: 'Greeting',
      arabicContent: null,
      englishContent: 'Hello',
      scope: 'Personal',
    });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/quick-replies');
  });

  it('offers the global scope only with the global permission', () => {
    configure();
    const fixture = TestBed.createComponent(QuickReplyForm);
    const authorization = TestBed.inject(AuthorizationState);

    authorization.set(['quickreplies.manage']);
    expect(fixture.componentInstance.scopeOptions().map((option) => option.value)).toEqual([
      'Personal',
    ]);

    authorization.set(['quickreplies.manage', 'quickreplies.manageglobal']);
    expect(fixture.componentInstance.scopeOptions().map((option) => option.value)).toEqual([
      'Personal',
      'Global',
    ]);
  });

  it('disables the scope control when editing, and never sends a scope on update', async () => {
    configure({ id: 'quick-reply-a' });
    quickRepliesService.get.and.resolveTo(existing);
    quickRepliesService.update.and.resolveTo(existing);
    const fixture = TestBed.createComponent(QuickReplyForm);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.form.controls.scope.disabled).toBeTrue();

    await fixture.componentInstance.submit();

    expect(quickRepliesService.update).toHaveBeenCalledWith('quick-reply-a', {
      name: 'My greeting',
      arabicContent: 'مرحباً',
      englishContent: 'Hello',
    });
  });

  it('surfaces a duplicate-name error from a mocked 409 response', async () => {
    configure();
    quickRepliesService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'quickreplies.duplicate_name' } }),
    );
    const fixture = TestBed.createComponent(QuickReplyForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Greeting',
      scope: 'Personal',
      arabicContent: '',
      englishContent: 'Hello',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('quickReplies.errors.duplicateName');
  });

  it('surfaces the global-permission error from a mocked 403 response', async () => {
    configure();
    quickRepliesService.create.and.rejectWith(
      new HttpErrorResponse({
        status: 403,
        error: { code: 'quickreplies.global_permission_required' },
      }),
    );
    const fixture = TestBed.createComponent(QuickReplyForm);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      name: 'Greeting',
      scope: 'Global',
      arabicContent: '',
      englishContent: 'Hello',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe(
      'quickReplies.errors.globalPermissionRequired',
    );
  });
});
