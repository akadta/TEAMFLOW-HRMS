import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CompanyRoles } from './company-roles';

describe('CompanyRoles', () => {
  let component: CompanyRoles;
  let fixture: ComponentFixture<CompanyRoles>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyRoles],
    }).compileComponents();

    fixture = TestBed.createComponent(CompanyRoles);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
