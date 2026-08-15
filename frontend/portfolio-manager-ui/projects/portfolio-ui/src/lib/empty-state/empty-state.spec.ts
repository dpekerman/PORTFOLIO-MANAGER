import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EmptyState } from './empty-state';

describe('EmptyState', () => {
  let component: EmptyState;
  let fixture: ComponentFixture<EmptyState>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EmptyState],
    }).compileComponents();

    fixture = TestBed.createComponent(EmptyState);
    fixture.componentRef.setInput('title', 'No transactions');
    fixture.componentRef.setInput('description', 'Transactions will appear here.');
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('renders its empty-state guidance', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('h2')?.textContent).toContain('No transactions');
    expect(fixture.nativeElement.querySelector('p')?.textContent).toContain(
      'Transactions will appear here.',
    );
  });
});
