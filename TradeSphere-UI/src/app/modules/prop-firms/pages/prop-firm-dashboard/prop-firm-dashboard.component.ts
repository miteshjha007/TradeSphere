import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Mt5Account, Mt5Service } from '../../../mt5/services/mt5.service';
import { PropFirm, PropFirmAccount, PropFirmService } from '../../services/prop-firm.service';

@Component({
  selector: 'app-prop-firm-dashboard',
  templateUrl: './prop-firm-dashboard.component.html',
  styleUrls: []
})
export class PropFirmDashboardComponent implements OnInit {
  firms: PropFirm[] = [];
  accounts: PropFirmAccount[] = [];
  mt5Accounts: Mt5Account[] = [];
  isLoading = true;
  isSavingFirm = false;
  isSavingAccount = false;
  errorMessage: string | null = null;

  firmForm = this.fb.group({
    name: ['FundingPips', Validators.required],
    websiteUrl: [''],
    notes: ['']
  });

  accountForm = this.fb.group({
    propFirmId: [null as number | null, Validators.required],
    mt5AccountId: [null as number | null],
    name: ['5K Challenge', Validators.required],
    accountSize: [5000, Validators.required],
    profitTarget: [400, Validators.required],
    dailyDrawdownLimit: [250, Validators.required],
    maxDrawdownLimit: [500, Validators.required],
    minimumTradingDays: [3, Validators.required],
    maxRiskPerTradePercent: [1, Validators.required],
    newsTradingAllowed: [false],
    weekendHoldingAllowed: [false],
    startedAt: [''],
    notes: ['']
  });

  constructor(
    private fb: FormBuilder,
    private propFirmService: PropFirmService,
    private mt5Service: Mt5Service
  ) { }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = null;
    this.propFirmService.getFirms().subscribe({
      next: firms => {
        this.firms = firms;
        if (firms.length > 0 && !this.accountForm.value.propFirmId) {
          this.accountForm.patchValue({ propFirmId: firms[0].id });
        }
        this.loadAccounts();
      },
      error: err => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || 'Failed to load prop firms.';
      }
    });

    this.mt5Service.getAccounts().subscribe({
      next: accounts => this.mt5Accounts = accounts,
      error: () => this.mt5Accounts = []
    });
  }

  loadAccounts(): void {
    this.propFirmService.getAccounts().subscribe({
      next: accounts => {
        this.accounts = accounts;
        this.isLoading = false;
      },
      error: err => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || 'Failed to load prop firm accounts.';
      }
    });
  }

  createFirm(): void {
    if (this.firmForm.invalid) {
      this.firmForm.markAllAsTouched();
      return;
    }

    this.isSavingFirm = true;
    this.propFirmService.createFirm(this.firmForm.getRawValue() as any).subscribe({
      next: firm => {
        this.isSavingFirm = false;
        this.firmForm.reset({ name: '', websiteUrl: '', notes: '' });
        this.accountForm.patchValue({ propFirmId: firm.id });
        this.load();
      },
      error: err => {
        this.isSavingFirm = false;
        this.errorMessage = err.error?.message || 'Could not save prop firm.';
      }
    });
  }

  createAccount(): void {
    if (this.accountForm.invalid) {
      this.accountForm.markAllAsTouched();
      return;
    }

    this.isSavingAccount = true;
    const raw = this.accountForm.getRawValue() as any;
    const payload = {
      ...raw,
      mt5AccountId: raw.mt5AccountId || null,
      startedAt: raw.startedAt || null
    };

    this.propFirmService.createAccount(payload).subscribe({
      next: () => {
        this.isSavingAccount = false;
        this.accountForm.patchValue({
          name: '5K Challenge',
          accountSize: 5000,
          profitTarget: 400,
          dailyDrawdownLimit: 250,
          maxDrawdownLimit: 500,
          minimumTradingDays: 3,
          maxRiskPerTradePercent: 1,
          newsTradingAllowed: false,
          weekendHoldingAllowed: false,
          notes: ''
        });
        this.loadAccounts();
      },
      error: err => {
        this.isSavingAccount = false;
        this.errorMessage = err.error?.message || 'Could not save prop firm account.';
      }
    });
  }

  deleteFirm(id: number): void {
    this.propFirmService.deleteFirm(id).subscribe({
      next: () => this.load(),
      error: err => this.errorMessage = err.error?.message || 'Could not delete prop firm.'
    });
  }

  deleteAccount(id: number): void {
    this.propFirmService.deleteAccount(id).subscribe({
      next: () => this.loadAccounts(),
      error: err => this.errorMessage = err.error?.message || 'Could not delete prop firm account.'
    });
  }
}
