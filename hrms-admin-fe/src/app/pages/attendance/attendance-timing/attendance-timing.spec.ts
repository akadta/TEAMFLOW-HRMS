import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AttendanceTiming } from './attendance-timing';

describe('AttendanceTiming', () => {
  let component: AttendanceTiming;
  let fixture: ComponentFixture<AttendanceTiming>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttendanceTiming],
    }).compileComponents();

    fixture = TestBed.createComponent(AttendanceTiming);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
