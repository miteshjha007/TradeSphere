import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StrategyCreateWizardComponent } from './strategy-create-wizard.component';

describe('StrategyCreateWizardComponent', () => {
  let component: StrategyCreateWizardComponent;
  let fixture: ComponentFixture<StrategyCreateWizardComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [StrategyCreateWizardComponent]
    });
    fixture = TestBed.createComponent(StrategyCreateWizardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
