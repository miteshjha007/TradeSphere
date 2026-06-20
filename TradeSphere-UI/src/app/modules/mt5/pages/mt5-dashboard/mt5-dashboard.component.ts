import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Mt5Account, Mt5ConnectionTestResult, Mt5Service, Mt5SymbolMapping } from '../../services/mt5.service';

interface Mt5AccountView extends Mt5Account {
  testing?: boolean;
  testResult?: Mt5ConnectionTestResult | null;
}

@Component({
  selector: 'app-mt5-dashboard',
  templateUrl: './mt5-dashboard.component.html',
  styleUrls: []
})
export class Mt5DashboardComponent implements OnInit {
  accounts: Mt5AccountView[] = [];
  mappings: Mt5SymbolMapping[] = [];
  isLoading = true;
  isSavingAccount = false;
  isSavingMapping = false;
  errorMessage: string | null = null;

  accountForm = this.fb.group({
    name: ['', Validators.required],
    login: [null as number | null, Validators.required],
    server: ['', Validators.required],
    password: ['', Validators.required],
    accountType: ['Demo', Validators.required],
    currency: ['USD', Validators.required],
    leverage: [100],
    tradingEnabled: [false]
  });

  mappingForm = this.fb.group({
    mt5AccountId: [null as number | null, Validators.required],
    strategySymbol: ['BTCUSD', Validators.required],
    brokerSymbol: ['BTCUSD', Validators.required],
    isActive: [true],
    notes: ['']
  });

  constructor(private fb: FormBuilder, private mt5Service: Mt5Service) { }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = null;
    this.mt5Service.getAccounts().subscribe({
      next: accounts => {
        this.accounts = accounts.map(a => ({ ...a, testing: false, testResult: null }));
        if (this.accounts.length > 0 && !this.mappingForm.value.mt5AccountId) {
          this.mappingForm.patchValue({ mt5AccountId: this.accounts[0].id });
        }
        this.loadMappings();
      },
      error: err => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || 'Failed to load MT5 accounts.';
      }
    });
  }

  loadMappings(): void {
    this.mt5Service.getSymbolMappings().subscribe({
      next: mappings => {
        this.mappings = mappings;
        this.isLoading = false;
      },
      error: err => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || 'Failed to load MT5 symbol mappings.';
      }
    });
  }

  connectAccount(): void {
    if (this.accountForm.invalid) {
      this.accountForm.markAllAsTouched();
      this.errorMessage = 'Please fill Account Name, MT5 Login, Broker Server, Password, Type, and Currency before saving.';
      return;
    }

    this.isSavingAccount = true;
    this.errorMessage = null;
    const raw = this.accountForm.getRawValue();
    const payload = {
      name: raw.name?.trim() || '',
      login: Number(raw.login),
      server: raw.server?.trim() || '',
      password: raw.password || '',
      accountType: raw.accountType || 'Demo',
      currency: (raw.currency || 'USD').trim().toUpperCase(),
      leverage: Number(raw.leverage || 0),
      tradingEnabled: raw.tradingEnabled === true
    };

    this.mt5Service.connectAccount(payload).subscribe({
      next: () => {
        this.isSavingAccount = false;
        this.accountForm.reset({
          name: '',
          login: null,
          server: '',
          password: '',
          accountType: 'Demo',
          currency: 'USD',
          leverage: 100,
          tradingEnabled: false
        });
        this.load();
      },
      error: err => {
        this.isSavingAccount = false;
        this.errorMessage = err.error?.message || 'Could not save MT5 account.';
      }
    });
  }

  testConnection(account: Mt5AccountView): void {
    account.testing = true;
    account.testResult = null;
    this.mt5Service.testConnection(account.id).subscribe({
      next: result => {
        account.testing = false;
        account.testResult = result;
        account.status = result.status;
      },
      error: err => {
        account.testing = false;
        account.testResult = err.error || {
          success: false,
          status: 'Error',
          message: 'MT5 bridge is not connected yet.'
        };
        account.status = account.testResult?.status || 'Error';
      }
    });
  }

  deleteAccount(id: number): void {
    this.mt5Service.deleteAccount(id).subscribe({
      next: () => this.load(),
      error: err => this.errorMessage = err.error?.message || 'Could not delete MT5 account.'
    });
  }

  saveMapping(): void {
    if (this.mappingForm.invalid) {
      this.mappingForm.markAllAsTouched();
      this.errorMessage = 'Please select MT5 account and enter both strategy symbol and broker symbol.';
      return;
    }

    this.isSavingMapping = true;
    this.errorMessage = null;
    const raw = this.mappingForm.getRawValue();
    const payload = {
      mt5AccountId: Number(raw.mt5AccountId),
      strategySymbol: (raw.strategySymbol || '').trim().toUpperCase(),
      brokerSymbol: (raw.brokerSymbol || '').trim(),
      isActive: raw.isActive === true,
      notes: raw.notes || undefined
    };

    this.mt5Service.upsertSymbolMapping(payload).subscribe({
      next: () => {
        this.isSavingMapping = false;
        this.mappingForm.patchValue({ strategySymbol: 'BTCUSD', brokerSymbol: 'BTCUSD', notes: '' });
        this.loadMappings();
      },
      error: err => {
        this.isSavingMapping = false;
        this.errorMessage = err.error?.message || 'Could not save symbol mapping.';
      }
    });
  }

  deleteMapping(id: number): void {
    this.mt5Service.deleteSymbolMapping(id).subscribe({
      next: () => this.loadMappings(),
      error: err => this.errorMessage = err.error?.message || 'Could not delete symbol mapping.'
    });
  }
}
