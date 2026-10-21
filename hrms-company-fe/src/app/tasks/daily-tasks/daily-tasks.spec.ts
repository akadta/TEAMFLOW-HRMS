import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyTasks } from './daily-tasks';

describe('DailyTasks', () => {
  let component: DailyTasks;
  let fixture: ComponentFixture<DailyTasks>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DailyTasks],
    }).compileComponents();

    fixture = TestBed.createComponent(DailyTasks);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
