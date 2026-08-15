import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SectionHeader } from './section-header';

describe('SectionHeader', () => {
  let component: SectionHeader;
  let fixture: ComponentFixture<SectionHeader>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SectionHeader],
    }).compileComponents();

    fixture = TestBed.createComponent(SectionHeader);
    fixture.componentRef.setInput('title', 'Risk allocation');
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('renders its title', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('h2')?.textContent).toContain('Risk allocation');
  });
});
