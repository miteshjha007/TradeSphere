import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RunBacktestDialogComponent } from './run-backtest-dialog.component';

describe('RunBacktestDialogComponent', () => {
  let component: RunBacktestDialogComponent;
  let fixture: ComponentFixture<RunBacktestDialogComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [RunBacktestDialogComponent]
    });
    fixture = TestBed.createComponent(RunBacktestDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
