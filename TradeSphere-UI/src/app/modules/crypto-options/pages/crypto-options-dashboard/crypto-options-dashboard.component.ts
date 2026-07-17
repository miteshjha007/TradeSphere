import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CryptoOptionBacktestRun, CryptoOptionChainFetchResult, CryptoOptionConfig, CryptoOptionDailyPnl, CryptoOptionExpiry, CryptoOptionLeg, CryptoOptionSnapshot, CryptoOptionSuggestion, CryptoOptionsService } from '../../services/crypto-options.service';

@Component({
  selector: 'app-crypto-options-dashboard',
  templateUrl: './crypto-options-dashboard.component.html',
  styleUrls: []
})
export class CryptoOptionsDashboardComponent implements OnInit {
  configs: CryptoOptionConfig[] = [];
  runs: CryptoOptionBacktestRun[] = [];
  snapshots: CryptoOptionSnapshot[] = [];
  dailyPnl: CryptoOptionDailyPnl[] = [];
  trades: CryptoOptionLeg[] = [];
  risk: any;
  loading = false;
  error = '';
  selectedRun?: CryptoOptionBacktestRun;
  selectedTabIndex = 0;
  expiries: CryptoOptionExpiry[] = [];
  liveChainRows: CryptoOptionSnapshot[] = [];
  chainSuggestions: CryptoOptionSuggestion[] = [];
  chainWarnings: string[] = [];
  chainResult?: CryptoOptionChainFetchResult;
  chainLoading = false;

  private readonly tabRoutes = [
    'dashboard',
    'backtest',
    'strategy-config',
    'option-chain',
    'runs',
    'daily-pnl',
    'risk-report'
  ];

  backtestForm: FormGroup = this.fb.group({
    strategyConfigId: [null],
    exchange: ['Delta Exchange', Validators.required],
    symbol: ['BTCUSD', Validators.required],
    fromDate: ['', Validators.required],
    toDate: ['', Validators.required],
    initialCapital: [10000, Validators.required],
    entryTime: ['09:00'],
    exitTime: ['17:15'],
    targetPremiumPerLeg: [100],
    stopLossPercentPerLeg: [100],
    strikeSelectionMode: ['PremiumBased'],
    strikeDistancePercent: [1.5],
    maxDailyLoss: [250],
    lotSize: [1]
  });


  chainForm: FormGroup = this.fb.group({
    exchange: ['Delta Exchange India', Validators.required],
    underlying: ['BTC', Validators.required],
    symbol: ['BTCUSD', Validators.required],
    expiryDate: [''],
    saveSnapshot: [true]
  });
  configForm: FormGroup = this.fb.group({
    name: ['BTC 0DTE Short Strangle', Validators.required],
    strategyType: ['ShortStrangle', Validators.required],
    underlying: ['BTC', Validators.required],
    symbol: ['BTCUSD', Validators.required],
    exchange: ['Delta Exchange', Validators.required],
    expiryType: ['Today'],
    entryTime: ['09:00'],
    exitTime: ['17:15'],
    targetPremiumPerLeg: [100],
    stopLossPercentPerLeg: [100],
    strikeSelectionMode: ['PremiumBased'],
    strikeDistancePercent: [1.5],
    maxDailyLoss: [250],
    lotSize: [1],
    useAtrFilter: [true],
    atrLength: [14],
    maxAtrPercent: [1.2],
    useTrendFilter: [false],
    emaLength: [50],
    maxTrendDistancePercent: [1],
    useSlippage: [true],
    slippagePercent: [0.5],
    brokeragePerOrder: [0],
    exchangeFeePercent: [0],
    isActive: [true]
  });

  constructor(private fb: FormBuilder, private service: CryptoOptionsService, private router: Router) { }

  ngOnInit(): void {
    this.syncTabFromUrl();
    this.refresh();
  }

  onTabChange(index: number): void {
    this.selectedTabIndex = index;
    const route = this.tabRoutes[index] || 'dashboard';
    this.router.navigate(['/crypto-options', route]);
  }

  refresh(): void {
    this.loading = true;
    this.error = '';
    this.service.getConfigs().subscribe({
      next: configs => {
        this.configs = configs;
        if (configs.length && !this.backtestForm.value.strategyConfigId) {
          this.backtestForm.patchValue({ strategyConfigId: configs[0].id, exchange: configs[0].exchange, symbol: configs[0].symbol });
        }
      },
      error: err => this.error = err?.error?.message || 'Failed to load configs.'
    });
    this.service.getRuns().subscribe({ next: runs => this.runs = runs, error: () => { } });
    this.service.getSnapshots().subscribe({ next: rows => this.snapshots = rows, error: () => { } });
    this.service.getRiskReport().subscribe({ next: risk => this.risk = risk, complete: () => this.loading = false, error: () => this.loading = false });
  }

  saveConfig(): void {
    if (this.configForm.invalid) return;
    this.loading = true;
    const payload = {
      ...this.configForm.value,
      legs: [
        { legName: 'Short Call', action: 'Sell', optionType: 'CE', expiryType: 'Today', strikeSelectionMode: 'PremiumBased', targetPremium: this.configForm.value.targetPremiumPerLeg, strikeDistancePercent: this.configForm.value.strikeDistancePercent, quantity: 1, sortOrder: 1 },
        { legName: 'Short Put', action: 'Sell', optionType: 'PE', expiryType: 'Today', strikeSelectionMode: 'PremiumBased', targetPremium: this.configForm.value.targetPremiumPerLeg, strikeDistancePercent: this.configForm.value.strikeDistancePercent, quantity: 1, sortOrder: 2 }
      ]
    };
    this.service.createConfig(payload).subscribe({
      next: () => this.refresh(),
      error: err => { this.error = err?.error?.message || 'Failed to save config.'; this.loading = false; }
    });
  }

  runBacktest(): void {
    if (this.backtestForm.invalid) return;
    this.loading = true;
    this.error = '';
    this.service.runBacktest(this.backtestForm.value).subscribe({
      next: run => { this.selectedRun = run; this.refresh(); this.loadRun(run); },
      error: err => { this.error = err?.error?.errorMessage || err?.error?.message || 'Backtest failed.'; this.loading = false; }
    });
  }

  loadRun(run: CryptoOptionBacktestRun): void {
    this.selectedRun = run;
    this.service.getDailyPnl(run.id).subscribe({ next: rows => this.dailyPnl = rows });
    this.service.getTrades(run.id).subscribe({ next: rows => this.trades = rows });
  }


  onUnderlyingChange(): void {
    const underlying = this.chainForm.value.underlying || 'BTC';
    this.chainForm.patchValue({ symbol: `${underlying}USD`, expiryDate: '' });
    this.expiries = [];
    this.liveChainRows = [];
    this.chainSuggestions = [];
    this.chainWarnings = [];
  }

  loadDeltaExpiries(): void {
    this.chainLoading = true;
    this.error = '';
    const { exchange, underlying, symbol } = this.chainForm.value;
    this.service.getDeltaExpiries(exchange, underlying, symbol).subscribe({
      next: rows => {
        this.expiries = rows;
        const firstActive = rows.find(x => !x.isExpired) || rows[0];
        if (firstActive) this.chainForm.patchValue({ expiryDate: firstActive.expiryDate?.substring(0, 10) });
        this.chainLoading = false;
      },
      error: err => { this.error = err?.error?.message || 'Failed to load Delta expiries.'; this.chainLoading = false; }
    });
  }

  fetchDeltaChain(): void {
    if (this.chainForm.invalid) return;
    this.chainLoading = true;
    this.error = '';
    const value = this.chainForm.value;
    const payload = {
      exchange: value.exchange,
      underlying: value.underlying,
      symbol: value.symbol,
      expiryDate: value.expiryDate || null,
      saveSnapshot: value.saveSnapshot
    };
    this.service.fetchDeltaChain(payload).subscribe({
      next: result => {
        this.chainResult = result;
        this.expiries = result.expiries || this.expiries;
        this.liveChainRows = result.rows || [];
        this.chainSuggestions = result.suggestions || [];
        this.chainWarnings = result.warnings || [];
        this.snapshots = result.imported > 0 ? [...this.liveChainRows, ...this.snapshots] : this.snapshots;
        this.chainLoading = false;
      },
      error: err => { this.error = err?.error?.message || 'Failed to fetch Delta option chain.'; this.chainLoading = false; }
    });
  }
  format(value: number | undefined): string {
    return (value ?? 0).toFixed(2);
  }

  private syncTabFromUrl(): void {
    const segment = this.router.url.split('/').filter(Boolean).pop() || 'dashboard';
    const index = this.tabRoutes.indexOf(segment);
    this.selectedTabIndex = index >= 0 ? index : 0;
  }
}

