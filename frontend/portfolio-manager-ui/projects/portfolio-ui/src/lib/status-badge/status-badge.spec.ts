import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  let component: StatusBadge;
  let fixture: ComponentFixture<StatusBadge>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatusBadge],
    }).compileComponents();

    fixture = TestBed.createComponent(StatusBadge);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('reflects its semantic tone on the host', () => {
    fixture.componentRef.setInput('tone', 'positive');
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(fixture.nativeElement.getAttribute('data-tone')).toBe('positive');
  });
});
