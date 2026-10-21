import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AttendanceCheckins } from './attendance-checkins';

describe('AttendanceCheckins', () => {
  let component: AttendanceCheckins;
  let fixture: ComponentFixture<AttendanceCheckins>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttendanceCheckins],
    }).compileComponents();

    fixture = TestBed.createComponent(AttendanceCheckins);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
