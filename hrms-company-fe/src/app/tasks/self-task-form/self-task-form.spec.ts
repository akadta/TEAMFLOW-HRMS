import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SelfTaskForm } from './self-task-form';

describe('SelfTaskForm', () => {
  let component: SelfTaskForm;
  let fixture: ComponentFixture<SelfTaskForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelfTaskForm],
    }).compileComponents();

    fixture = TestBed.createComponent(SelfTaskForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
