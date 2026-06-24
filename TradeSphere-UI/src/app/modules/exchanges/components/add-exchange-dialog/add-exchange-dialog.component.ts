import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { ExchangeService } from '../../services/exchange.service';
import { Exchange } from '../../models/exchange.model';

@Component({
  selector: 'app-add-exchange-dialog',
  templateUrl: './add-exchange-dialog.component.html',
  styleUrls: []
})
export class AddExchangeDialogComponent implements OnInit {
  form: FormGroup;
  supportedExchanges: Exchange[] = [];
  isLoading = true;

  constructor(
    private fb: FormBuilder,
    private exchangeService: ExchangeService,
    private dialogRef: MatDialogRef<AddExchangeDialogComponent>
  ) {
    this.form = this.fb.group({
      exchangeId: ['', Validators.required],
      name: ['', Validators.required],
      apiKey: ['', Validators.required],
      apiSecret: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.exchangeService.getSupportedExchanges().subscribe({
      next: (data) => {
        this.supportedExchanges = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        // Mock data
        this.supportedExchanges = [
          { id: 1, name: 'Delta Exchange India', baseUrl: 'https://api.india.delta.exchange' },
          { id: 2, name: 'Delta Exchange Testnet', baseUrl: 'https://cdn-ind.testnet.deltaex.org' },
          { id: 3, name: 'Cosmic Exchange', baseUrl: 'https://api.cosmic.exchange' },
          { id: 4, name: 'CoinDCX Futures', baseUrl: 'https://api.coindcx.com' },
          { id: 5, name: 'Dhan', baseUrl: 'https://api.dhan.co/v2' }
        ];
      }
    });
  }

  selectedExchangeName(): string {
    const exchangeId = Number(this.form.get('exchangeId')?.value);
    return this.supportedExchanges.find(ex => ex.id === exchangeId)?.name || '';
  }

  isDhanSelected(): boolean {
    return this.selectedExchangeName().toLowerCase() === 'dhan';
  }

  onSubmit(): void {
    if (this.form.valid) {
      this.exchangeService.connectExchange(this.form.value).subscribe({
        next: (res) => {
          this.dialogRef.close(res);
        },
        error: (err) => {
          console.error(err);
          // Show error message logic
        }
      });
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
