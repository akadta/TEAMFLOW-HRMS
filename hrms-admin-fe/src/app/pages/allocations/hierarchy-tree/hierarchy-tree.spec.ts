import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HierarchyTree } from './hierarchy-tree';

describe('HierarchyTree', () => {
  let component: HierarchyTree;
  let fixture: ComponentFixture<HierarchyTree>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HierarchyTree],
    }).compileComponents();

    fixture = TestBed.createComponent(HierarchyTree);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
