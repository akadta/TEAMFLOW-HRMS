import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssignTasks } from './assign-tasks';

describe('AssignTasks', () => {
  let component: AssignTasks;
  let fixture: ComponentFixture<AssignTasks>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignTasks],
    }).compileComponents();

    fixture = TestBed.createComponent(AssignTasks);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
