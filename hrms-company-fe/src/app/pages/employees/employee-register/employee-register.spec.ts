import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EmployeeRegister } from './employee-register';

describe('EmployeeRegister', () => {
  let component: EmployeeRegister;
  let fixture: ComponentFixture<EmployeeRegister>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EmployeeRegister],
    }).compileComponents();

    fixture = TestBed.createComponent(EmployeeRegister);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
