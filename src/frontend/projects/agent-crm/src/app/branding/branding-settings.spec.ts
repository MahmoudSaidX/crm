import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { BrandingSettings } from './branding-settings';
import { BrandingSettingsService } from './branding-settings.service';
import { BRANDING_TRANSLATIONS } from './branding-translations';

describe('BrandingSettings', () => {
  let brandingSettingsService: jasmine.SpyObj<BrandingSettingsService>;

  const existing = {
    organizationDisplayNameEn: 'Acme',
    organizationDisplayNameAr: null,
    productDisplayNameEn: 'Acme CRM',
    productDisplayNameAr: null,
    themeTokens: { primaryColor: '#112233' },
    assets: [],
    updatedAtUtc: '2026-09-01T00:00:00Z',
    updatedByHandle: 'admin@example.test',
  };

  function configure(): void {
    brandingSettingsService = jasmine.createSpyObj<BrandingSettingsService>('BrandingSettingsService', [
      'get',
      'update',
      'uploadLogo',
      'deleteLogo',
    ]);
    brandingSettingsService.get.and.resolveTo(existing);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(BRANDING_TRANSLATIONS),
        { provide: BrandingSettingsService, useValue: brandingSettingsService },
      ],
    });
  }

  it('loads current settings into the form', async () => {
    configure();
    const fixture = TestBed.createComponent(BrandingSettings);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.form.controls.productDisplayNameEn.value).toBe('Acme CRM');
    expect(fixture.componentInstance.form.controls.primaryColor.value).toBe('#112233');
  });

  it('blocks submit when the required product name is cleared', async () => {
    configure();
    const fixture = TestBed.createComponent(BrandingSettings);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.form.controls.productDisplayNameEn.setValue('');

    await fixture.componentInstance.submit();

    expect(brandingSettingsService.update).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.productDisplayNameEn.touched).toBeTrue();
  });

  it('surfaces an invalid-theme-token error from a mocked 422 response', async () => {
    configure();
    brandingSettingsService.update.and.rejectWith(
      new HttpErrorResponse({ status: 422, error: { code: 'branding.invalid_theme_token' } }),
    );
    const fixture = TestBed.createComponent(BrandingSettings);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('branding.errors.invalidThemeToken');
  });

  it('applies the returned settings and shows the saved message on success', async () => {
    configure();
    brandingSettingsService.update.and.resolveTo({ ...existing, productDisplayNameEn: 'Updated CRM' });
    const fixture = TestBed.createComponent(BrandingSettings);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.savedMessage()).toBeTrue();
    expect(fixture.componentInstance.settings()?.productDisplayNameEn).toBe('Updated CRM');
  });
});
