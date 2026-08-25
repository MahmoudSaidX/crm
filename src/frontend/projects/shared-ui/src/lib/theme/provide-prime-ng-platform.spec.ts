import { TestBed } from '@angular/core/testing';
import { PrimeNG } from 'primeng/config';
import { providePrimeNgPlatform } from './provide-prime-ng-platform';

describe('providePrimeNgPlatform', () => {
  it('configures a PrimeNG theme preset for the application', () => {
    TestBed.configureTestingModule({ providers: [providePrimeNgPlatform()] });

    const primeng = TestBed.inject(PrimeNG);

    expect(primeng.theme()).toBeTruthy();
    expect(primeng.ripple()).toBeTrue();
  });
});
