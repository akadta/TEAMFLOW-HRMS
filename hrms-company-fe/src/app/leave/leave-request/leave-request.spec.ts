import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LeaveRequest } from './leave-request';

describe('LeaveRequest', () => {
  let component: LeaveRequest;
  let fixture: ComponentFixture<LeaveRequest>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeaveRequest],
    }).compileComponents();

    fixture = TestBed.createComponent(LeaveRequest);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
