import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AttendanceApproval } from './attendance-approval';

describe('AttendanceApproval', () => {
  let component: AttendanceApproval;
  let fixture: ComponentFixture<AttendanceApproval>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttendanceApproval],
    }).compileComponents();

    fixture = TestBed.createComponent(AttendanceApproval);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
