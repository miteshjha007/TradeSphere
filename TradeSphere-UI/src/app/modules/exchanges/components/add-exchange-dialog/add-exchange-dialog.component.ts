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
          { id: 1, name: 'Delta Exchange', baseUrl: 'https://api.delta.exchange' },
          { id: 2, name: 'Cosmic Exchange', baseUrl: 'https://api.cosmic.exchange' }
        ];
      }
    });
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
