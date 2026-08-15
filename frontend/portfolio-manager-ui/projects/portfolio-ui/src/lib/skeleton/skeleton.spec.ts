import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Skeleton } from './skeleton';

describe('Skeleton', () => {
  let component: Skeleton;
  let fixture: ComponentFixture<Skeleton>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Skeleton],
    }).compileComponents();

    fixture = TestBed.createComponent(Skeleton);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('exposes an accessible loading label', () => {
    fixture.componentRef.setInput('label', 'Loading holdings');
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(fixture.nativeElement.getAttribute('role')).toBe('status');
    expect(fixture.nativeElement.getAttribute('aria-label')).toBe('Loading holdings');
  });
});
