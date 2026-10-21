import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LeaveApprovals } from './leave-approvals';

describe('LeaveApprovals', () => {
  let component: LeaveApprovals;
  let fixture: ComponentFixture<LeaveApprovals>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeaveApprovals],
    }).compileComponents();

    fixture = TestBed.createComponent(LeaveApprovals);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
