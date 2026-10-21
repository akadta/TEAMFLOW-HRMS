import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TaskInbox } from './task-inbox';

describe('TaskInbox', () => {
  let component: TaskInbox;
  let fixture: ComponentFixture<TaskInbox>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskInbox],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskInbox);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
