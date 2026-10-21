import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CompanyEmployees } from './company-employees';

describe('CompanyEmployees', () => {
  let component: CompanyEmployees;
  let fixture: ComponentFixture<CompanyEmployees>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyEmployees],
    }).compileComponents();

    fixture = TestBed.createComponent(CompanyEmployees);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
