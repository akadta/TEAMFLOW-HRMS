import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssignedTasks } from './assigned-tasks';

describe('AssignedTasks', () => {
  let component: AssignedTasks;
  let fixture: ComponentFixture<AssignedTasks>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignedTasks],
    }).compileComponents();

    fixture = TestBed.createComponent(AssignedTasks);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
