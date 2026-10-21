import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MainEmployeePermission } from './main-employee-permission';

describe('MainEmployeePermission', () => {
  let component: MainEmployeePermission;
  let fixture: ComponentFixture<MainEmployeePermission>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MainEmployeePermission],
    }).compileComponents();

    fixture = TestBed.createComponent(MainEmployeePermission);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
