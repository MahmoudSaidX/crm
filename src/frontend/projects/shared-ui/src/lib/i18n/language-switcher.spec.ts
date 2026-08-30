import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { providePrimeNG } from 'primeng/config';
import { LanguageSwitcher } from './language-switcher';

describe('LanguageSwitcher', () => {
  it('renders plain input copy and emits the next locale', () => {
    TestBed.configureTestingModule({
      imports: [LanguageSwitcher],
      providers: [provideNoopAnimations(), providePrimeNG({})],
    });
    const fixture = TestBed.createComponent(LanguageSwitcher);
    fixture.componentRef.setInput('locale', 'en');
    fixture.componentRef.setInput('label', 'العربية');
    fixture.componentRef.setInput('accessibleLabel', 'Change language');
    const emitted: string[] = [];
    fixture.componentInstance.localeChange.subscribe((locale) => emitted.push(locale));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button').textContent).toContain('العربية');
    expect(fixture.nativeElement.querySelector('button').getAttribute('aria-label')).toBe(
      'Change language',
    );
    fixture.nativeElement.querySelector('button').click();

    expect(emitted).toEqual(['ar']);
  });
});
