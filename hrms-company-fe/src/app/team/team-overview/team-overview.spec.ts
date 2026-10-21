import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TeamOverview } from './team-overview';

describe('TeamOverview', () => {
  let component: TeamOverview;
  let fixture: ComponentFixture<TeamOverview>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamOverview],
    }).compileComponents();

    fixture = TestBed.createComponent(TeamOverview);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
