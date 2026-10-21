import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LeaveSettings } from './leave-settings';

describe('LeaveSettings', () => {
  let component: LeaveSettings;
  let fixture: ComponentFixture<LeaveSettings>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeaveSettings],
    }).compileComponents();

    fixture = TestBed.createComponent(LeaveSettings);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
