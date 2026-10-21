import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AttendanceLogins } from './attendance-logins';

describe('AttendanceLogins', () => {
  let component: AttendanceLogins;
  let fixture: ComponentFixture<AttendanceLogins>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttendanceLogins],
    }).compileComponents();

    fixture = TestBed.createComponent(AttendanceLogins);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
