import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TaskApprovals } from './task-approvals';

describe('TaskApprovals', () => {
  let component: TaskApprovals;
  let fixture: ComponentFixture<TaskApprovals>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskApprovals],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskApprovals);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
