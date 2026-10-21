import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MyCompanies } from './my-companies';

describe('MyCompanies', () => {
  let component: MyCompanies;
  let fixture: ComponentFixture<MyCompanies>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MyCompanies],
    }).compileComponents();

    fixture = TestBed.createComponent(MyCompanies);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
