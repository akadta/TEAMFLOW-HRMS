import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SelfTasks } from './self-tasks';

describe('SelfTasks', () => {
  let component: SelfTasks;
  let fixture: ComponentFixture<SelfTasks>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelfTasks],
    }).compileComponents();

    fixture = TestBed.createComponent(SelfTasks);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
