import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { StrategyService } from '../../services/strategy.service';
import { ExchangeService } from '../../../exchanges/services/exchange.service';
import { UserExchange } from '../../../exchanges/models/exchange.model';
import { Strategy } from '../../models/strategy.model';

@Component({
  selector: 'app-strategy-create-wizard',
  templateUrl: './strategy-create-wizard.component.html',
  styleUrls: []
})
export class StrategyCreateWizardComponent implements OnInit {
  firstFormGroup: FormGroup;
  secondFormGroup: FormGroup;

  availableStrategies: Strategy[] = [];
  userExchanges: UserExchange[] = [];

  constructor(
    private _formBuilder: FormBuilder,
    private strategyService: StrategyService,
    private exchangeService: ExchangeService,
    private router: Router
  ) {
    this.firstFormGroup = this._formBuilder.group({
      strategyId: ['', Validators.required],
      exchangeId: ['', Validators.required],
      symbol: ['', Validators.required]
    });
    this.secondFormGroup = this._formBuilder.group({
      config: ['{}', Validators.required]
    });
  }

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.strategyService.getAvailableStrategies().subscribe(data => this.availableStrategies = data);
    this.exchangeService.getUserExchanges().subscribe(data => this.userExchanges = data);
  }

  onStrategySelect(strategyId: number) {
    const strategy = this.availableStrategies.find(s => s.id === strategyId);
    if (strategy) {
      this.secondFormGroup.patchValue({
        config: strategy.defaultConfig
      });
    }
  }

  deploy() {
    if (this.firstFormGroup.valid && this.secondFormGroup.valid) {
      const deployData = {
        ...this.firstFormGroup.value,
        config: this.secondFormGroup.value.config
      };

      this.strategyService.deployStrategy(deployData).subscribe({
        next: (res) => {
          this.router.navigate(['/strategies']);
        },
        error: (err) => {
          console.error(err);
          // show error snackbar
        }
      });
    }
  }
}
