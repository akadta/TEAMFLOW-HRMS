import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CompanyEmployeePermission } from './company-employee-permission';

describe('CompanyEmployeePermission', () => {
  let component: CompanyEmployeePermission;
  let fixture: ComponentFixture<CompanyEmployeePermission>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyEmployeePermission],
    }).compileComponents();

    fixture = TestBed.createComponent(CompanyEmployeePermission);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
