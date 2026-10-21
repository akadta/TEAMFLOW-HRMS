import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AttendanceSettings } from './attendance-settings';

describe('AttendanceSettings', () => {
  let component: AttendanceSettings;
  let fixture: ComponentFixture<AttendanceSettings>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttendanceSettings],
    }).compileComponents();

    fixture = TestBed.createComponent(AttendanceSettings);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
