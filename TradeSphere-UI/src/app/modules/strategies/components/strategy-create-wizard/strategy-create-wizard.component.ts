import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { StrategyService } from '../../services/strategy.service';
import { ExchangeService } from '../../../exchanges/services/exchange.service';
import { UserExchange } from '../../../exchanges/models/exchange.model';
import { Strategy } from '../../models/strategy.model';
import { Mt5Account, Mt5Service } from '../../../mt5/services/mt5.service';

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
  mt5Accounts: Mt5Account[] = [];
  private isSyncing = false;

  constructor(
    private _formBuilder: FormBuilder,
    private strategyService: StrategyService,
    private exchangeService: ExchangeService,
    private mt5Service: Mt5Service,
    private router: Router
  ) {
    this.firstFormGroup = this._formBuilder.group({
      strategyId: ['', Validators.required],
      executionProvider: ['Delta', Validators.required],
      userExchangeId: [''],
      mt5AccountId: [''],
      symbol: ['', Validators.required]
    });
    this.secondFormGroup = this._formBuilder.group({
      config: ['{}', Validators.required],
      tradeSizeType: ['Contracts', Validators.required],
      tradeSizeValue: [1, [Validators.required, Validators.min(0.0001)]],
      leverage: [10, [Validators.required, Validators.min(1), Validators.max(200)]]
    });
  }

  ngOnInit() {
    this.loadData();
    this.syncExecutionValidators('Delta');

    this.firstFormGroup.get('executionProvider')?.valueChanges.subscribe(provider => {
      this.syncExecutionValidators(provider);
    });

    // Subscribe to sizing controls changes
    this.secondFormGroup.get('tradeSizeType')?.valueChanges.subscribe(() => this.updateConfigJson());
    this.secondFormGroup.get('tradeSizeValue')?.valueChanges.subscribe(() => this.updateConfigJson());
    this.secondFormGroup.get('leverage')?.valueChanges.subscribe(() => this.updateConfigJson());

    // Subscribe to config text changes (in case they edit JSON directly)
    this.secondFormGroup.get('config')?.valueChanges.subscribe(val => {
      this.parseConfigJson(val);
    });
  }

  loadData() {
    this.strategyService.getAvailableStrategies().subscribe(data => this.availableStrategies = data);
    this.exchangeService.getUserExchanges().subscribe(data => this.userExchanges = data);
    this.mt5Service.getAccounts().subscribe(data => this.mt5Accounts = data);
  }

  syncExecutionValidators(provider: string) {
    const userExchange = this.firstFormGroup.get('userExchangeId');
    const mt5Account = this.firstFormGroup.get('mt5AccountId');

    if (provider === 'MT5') {
      userExchange?.clearValidators();
      userExchange?.setValue('', { emitEvent: false });
      mt5Account?.setValidators([Validators.required]);
      if (!this.firstFormGroup.get('symbol')?.value) {
        this.firstFormGroup.patchValue({ symbol: 'XAUUSD' }, { emitEvent: false });
      }
    } else {
      mt5Account?.clearValidators();
      mt5Account?.setValue('', { emitEvent: false });
      userExchange?.setValidators([Validators.required]);
      if (!this.firstFormGroup.get('symbol')?.value) {
        this.firstFormGroup.patchValue({ symbol: 'BTCUSD' }, { emitEvent: false });
      }
    }

    userExchange?.updateValueAndValidity({ emitEvent: false });
    mt5Account?.updateValueAndValidity({ emitEvent: false });
  }

  onStrategySelect(strategyId: number) {
    const strategy = this.availableStrategies.find(s => s.id === strategyId);
    if (strategy) {
      this.secondFormGroup.patchValue({
        config: strategy.defaultConfig
      });
      this.parseConfigJson(strategy.defaultConfig);
    }
  }

  parseConfigJson(configStr: string) {
    if (this.isSyncing) return;
    try {
      const configObj = JSON.parse(configStr);
      this.isSyncing = true;
      this.secondFormGroup.patchValue({
        tradeSizeType: configObj.tradeSizeType || 'Contracts',
        tradeSizeValue: configObj.tradeSizeValue !== undefined ? configObj.tradeSizeValue : 1,
        leverage: configObj.leverage !== undefined ? configObj.leverage : 10
      }, { emitEvent: false });
      this.isSyncing = false;
    } catch (e) {
      // Ignore JSON parse errors while user is typing
    }
  }

  updateConfigJson() {
    if (this.isSyncing) return;
    try {
      const currentConfigStr = this.secondFormGroup.get('config')?.value || '{}';
      const configObj = JSON.parse(currentConfigStr);
      
      configObj.tradeSizeType = this.secondFormGroup.get('tradeSizeType')?.value;
      configObj.tradeSizeValue = Number(this.secondFormGroup.get('tradeSizeValue')?.value);
      configObj.leverage = Number(this.secondFormGroup.get('leverage')?.value);

      this.isSyncing = true;
      this.secondFormGroup.patchValue({
        config: JSON.stringify(configObj, null, 2)
      }, { emitEvent: false });
      this.isSyncing = false;
    } catch (e) {
      // Ignore if config text is currently invalid JSON
    }
  }

  deploy() {
    if (this.firstFormGroup.valid && this.secondFormGroup.valid) {
      const deployData = {
        strategyId: Number(this.firstFormGroup.value.strategyId),
        executionProvider: this.firstFormGroup.value.executionProvider,
        userExchangeId: this.firstFormGroup.value.executionProvider === 'Delta' ? Number(this.firstFormGroup.value.userExchangeId) : undefined,
        mt5AccountId: this.firstFormGroup.value.executionProvider === 'MT5' ? Number(this.firstFormGroup.value.mt5AccountId) : undefined,
        symbol: this.firstFormGroup.value.symbol,
        config: this.secondFormGroup.value.config
      };

      this.strategyService.deployStrategy(deployData).subscribe({
        next: (res) => {
          this.router.navigate(['/strategies']);
        },
        error: (err) => {
          console.error(err);
        }
      });
    }
  }
}
