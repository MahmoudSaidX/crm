import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PageHeader } from './page-header';

@Component({
  imports: [PageHeader],
  template: `<sc-page-header title="Tickets" subtitle="All tickets">
    <button actions type="button">New</button>
  </sc-page-header>`,
})
class HostComponent {}

describe('PageHeader', () => {
  it('renders the title, subtitle and projected actions', async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent).toContain('Tickets');
    expect(element.querySelector('.sc-page-header-subtitle')?.textContent).toContain('All tickets');
    expect(element.querySelector('.sc-page-header-actions button')?.textContent).toContain('New');
  });
});
