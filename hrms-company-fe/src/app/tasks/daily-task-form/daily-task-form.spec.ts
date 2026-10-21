import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyTaskForm } from './daily-task-form';

describe('DailyTaskForm', () => {
  let component: DailyTaskForm;
  let fixture: ComponentFixture<DailyTaskForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DailyTaskForm],
    }).compileComponents();

    fixture = TestBed.createComponent(DailyTaskForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
