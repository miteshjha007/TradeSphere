import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  DhanAccount,
  DhanConnectionTestResult,
  IndianMarketService,
  IndianUnderlying,
  OptionChain,
  OptionChainRow,
  OptionLeg
} from '../../services/indian-market.service';

@Component({
  selector: 'app-indian-market-dashboard',
  templateUrl: './indian-market-dashboard.component.html',
  styleUrls: []
})
export class IndianMarketDashboardComponent implements OnInit {
  accountForm: FormGroup;
  accounts: DhanAccount[] = [];
  underlyings: IndianUnderlying[] = [];
  expiries: string[] = [];
  optionChain: OptionChain | null = null;
  selectedAccountId: number | null = null;
  selectedUnderlying = 'NIFTY';
  selectedExpiry = '';
  lotMultiplier = 1;
  loadingAccounts = false;
  loadingExpiries = false;
  loadingChain = false;
  placingOrderKey: string | null = null;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  testResult: DhanConnectionTestResult | null = null;

  constructor(private fb: FormBuilder, private indianMarketService: IndianMarketService) {
    this.accountForm = this.fb.group({
      name: ['Dhan Main', Validators.required],
      dhanClientId: ['', Validators.required],
      accessToken: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.loadInitialData();
  }

  loadInitialData(): void {
    this.loadingAccounts = true;
    this.indianMarketService.getUnderlyings().subscribe({
      next: data => {
        this.underlyings = data;
      },
      error: err => this.errorMessage = err.error?.message || 'Could not load Indian underlyings.'
    });

    this.indianMarketService.getAccounts().subscribe({
      next: data => {
        this.accounts = data;
        this.loadingAccounts = false;
        if (!this.selectedAccountId && data.length > 0) {
          this.selectedAccountId = data[0].id;
          this.loadExpiries();
        }
      },
      error: err => {
        this.loadingAccounts = false;
        this.errorMessage = err.error?.message || 'Could not load Dhan accounts.';
      }
    });
  }

  connectAccount(): void {
    if (this.accountForm.invalid) {
      this.accountForm.markAllAsTouched();
      return;
    }

    this.errorMessage = null;
    this.successMessage = null;
    this.indianMarketService.connectAccount(this.accountForm.value).subscribe({
      next: account => {
        this.accounts = [account, ...this.accounts];
        this.selectedAccountId = account.id;
        this.accountForm.patchValue({ accessToken: '' });
        this.successMessage = 'Dhan account saved. Test connection before placing live orders.';
        this.loadExpiries();
      },
      error: err => this.errorMessage = err.error?.message || 'Could not save Dhan account.'
    });
  }

  testConnection(): void {
    if (!this.selectedAccountId) {
      this.errorMessage = 'Select a Dhan account first.';
      return;
    }

    this.errorMessage = null;
    this.testResult = null;
    this.indianMarketService.testConnection(this.selectedAccountId).subscribe({
      next: result => {
        this.testResult = result;
        this.successMessage = result.message;
      },
      error: err => {
        this.testResult = err.error;
        this.errorMessage = err.error?.message || 'Dhan connection test failed.';
      }
    });
  }

  deleteAccount(account: DhanAccount): void {
    if (!confirm(`Delete Dhan account "${account.name}"?`)) {
      return;
    }

    this.indianMarketService.deleteAccount(account.id).subscribe({
      next: () => {
        this.accounts = this.accounts.filter(a => a.id !== account.id);
        this.selectedAccountId = this.accounts[0]?.id ?? null;
        this.optionChain = null;
        this.expiries = [];
        if (this.selectedAccountId) {
          this.loadExpiries();
        }
      },
      error: err => this.errorMessage = err.error?.message || 'Could not delete Dhan account.'
    });
  }

  onAccountChanged(): void {
    this.optionChain = null;
    this.loadExpiries();
  }

  onUnderlyingChanged(): void {
    this.optionChain = null;
    this.selectedExpiry = '';
    this.loadExpiries();
  }

  loadExpiries(): void {
    if (!this.selectedAccountId) {
      return;
    }

    this.loadingExpiries = true;
    this.errorMessage = null;
    this.indianMarketService.getExpiries(this.selectedAccountId, this.selectedUnderlying).subscribe({
      next: expiries => {
        this.expiries = expiries;
        this.selectedExpiry = expiries[0] || '';
        this.loadingExpiries = false;
        if (this.selectedExpiry) {
          this.loadOptionChain();
        }
      },
      error: err => {
        this.loadingExpiries = false;
        this.errorMessage = err.error?.message || 'Could not load option expiries.';
      }
    });
  }

  loadOptionChain(): void {
    if (!this.selectedAccountId || !this.selectedExpiry) {
      this.errorMessage = 'Select Dhan account and expiry first.';
      return;
    }

    this.loadingChain = true;
    this.errorMessage = null;
    this.indianMarketService.getOptionChain(this.selectedAccountId, this.selectedUnderlying, this.selectedExpiry).subscribe({
      next: chain => {
        this.optionChain = chain;
        this.loadingChain = false;
      },
      error: err => {
        this.loadingChain = false;
        this.errorMessage = err.error?.message || 'Could not load option chain.';
      }
    });
  }

  placeOrder(row: OptionChainRow, leg: OptionLeg, transactionType: 'BUY' | 'SELL'): void {
    if (!this.selectedAccountId || !this.selectedExpiry) {
      this.errorMessage = 'Select Dhan account and expiry first.';
      return;
    }

    const underlying = this.currentUnderlying();
    const quantity = Math.max(1, this.lotMultiplier) * (underlying?.lotSize || 1);
    const label = `${transactionType} ${this.selectedUnderlying} ${this.selectedExpiry} ${row.strikePrice} ${leg.optionType} x ${quantity}`;
    if (!confirm(`Place ${label}? This is a live Dhan order if your token/IP permits trading.`)) {
      return;
    }

    this.placingOrderKey = `${row.strikePrice}-${leg.optionType}-${transactionType}`;
    this.errorMessage = null;
    this.successMessage = null;
    this.indianMarketService.placeOrder({
      dhanAccountId: this.selectedAccountId,
      underlying: this.selectedUnderlying,
      expiry: this.selectedExpiry,
      strikePrice: row.strikePrice,
      optionType: leg.optionType,
      securityId: leg.securityId,
      transactionType,
      quantity,
      productType: 'INTRADAY',
      orderType: 'MARKET'
    }).subscribe({
      next: result => {
        this.placingOrderKey = null;
        this.successMessage = `${result.message} Order: ${result.orderId || 'n/a'} ${result.orderStatus || ''}`;
      },
      error: err => {
        this.placingOrderKey = null;
        this.errorMessage = err.error?.message || 'Dhan order failed.';
      }
    });
  }

  currentUnderlying(): IndianUnderlying | undefined {
    return this.underlyings.find(u => u.symbol === this.selectedUnderlying);
  }

  visibleRows(): OptionChainRow[] {
    if (!this.optionChain) {
      return [];
    }

    const spot = this.optionChain.underlyingLastPrice;
    return this.optionChain.rows
      .filter(row => Math.abs(row.strikePrice - spot) <= (this.currentUnderlying()?.strikeStep || 50) * 8)
      .slice(0, 21);
  }

  orderKey(row: OptionChainRow, leg: OptionLeg, transactionType: 'BUY' | 'SELL'): string {
    return `${row.strikePrice}-${leg.optionType}-${transactionType}`;
  }

  oiChange(leg?: OptionLeg): number {
    if (!leg) {
      return 0;
    }

    return leg.openInterest - leg.previousOpenInterest;
  }

  formatNumber(value?: number): string {
    return (value ?? 0).toLocaleString('en-IN', { maximumFractionDigits: 2 });
  }
}
