import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PageHeader } from './page-header';

describe('PageHeader', () => {
  let component: PageHeader;
  let fixture: ComponentFixture<PageHeader>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PageHeader],
    }).compileComponents();

    fixture = TestBed.createComponent(PageHeader);
    fixture.componentRef.setInput('title', 'Portfolio');
    fixture.componentRef.setInput('subtitle', 'Current positions');
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('renders its title and subtitle', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Portfolio');
    expect(fixture.nativeElement.querySelector('p')?.textContent).toContain('Current positions');
  });
});
