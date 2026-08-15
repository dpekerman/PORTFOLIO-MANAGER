import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DialogShell } from './dialog-shell';

describe('DialogShell', () => {
  let component: DialogShell;
  let fixture: ComponentFixture<DialogShell>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DialogShell],
    }).compileComponents();

    fixture = TestBed.createComponent(DialogShell);
    fixture.componentRef.setInput('title', 'Add position');
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('renders an accessible heading', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('h2')?.textContent).toContain('Add position');
  });
});
