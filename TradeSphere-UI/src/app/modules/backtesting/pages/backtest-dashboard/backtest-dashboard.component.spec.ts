import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BacktestDashboardComponent } from './backtest-dashboard.component';

describe('BacktestDashboardComponent', () => {
  let component: BacktestDashboardComponent;
  let fixture: ComponentFixture<BacktestDashboardComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [BacktestDashboardComponent]
    });
    fixture = TestBed.createComponent(BacktestDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
