import { TestBed } from '@angular/core/testing';
import { PrimeNG } from 'primeng/config';
import { PrimeNgLocaleAdapter } from './prime-ng-locale-adapter';

describe('PrimeNgLocaleAdapter', () => {
  it('supplies the matching common PrimeNG labels for each locale', () => {
    const primeNg = jasmine.createSpyObj<PrimeNG>('PrimeNG', ['setTranslation']);
    TestBed.configureTestingModule({ providers: [{ provide: PrimeNG, useValue: primeNg }] });
    const adapter = TestBed.inject(PrimeNgLocaleAdapter);

    adapter.setLocale('en');
    expect(primeNg.setTranslation).toHaveBeenCalledWith(
      jasmine.objectContaining({
        emptyMessage: 'No results found',
        aria: jasmine.objectContaining({
          firstPageLabel: 'First page',
          nextPageLabel: 'Next page',
          rowsPerPageLabel: 'Rows per page',
        }),
      }),
    );

    adapter.setLocale('ar');
    expect(primeNg.setTranslation).toHaveBeenCalledWith(
      jasmine.objectContaining({
        emptyMessage: 'لا توجد نتائج',
        aria: jasmine.objectContaining({
          pageLabel: 'الصفحة {page}',
          firstPageLabel: 'الصفحة الأولى',
          lastPageLabel: 'الصفحة الأخيرة',
          nextPageLabel: 'الصفحة التالية',
          prevPageLabel: 'الصفحة السابقة',
          rowsPerPageLabel: 'عدد الصفوف في الصفحة',
        }),
      }),
    );

    adapter.setLocale('en');
    const reappliedEnglish = primeNg.setTranslation.calls.mostRecent().args[0];
    expect(reappliedEnglish.aria?.nextPageLabel).toBe('Next page');
  });
});
