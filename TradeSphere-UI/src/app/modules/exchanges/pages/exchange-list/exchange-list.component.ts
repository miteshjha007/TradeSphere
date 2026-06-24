import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ExchangeService, ConnectionTestResult } from '../../services/exchange.service';
import { UserExchange } from '../../models/exchange.model';
import { AddExchangeDialogComponent } from '../../components/add-exchange-dialog/add-exchange-dialog.component';

interface ExchangeWithTestState extends UserExchange {
  testing?: boolean;
  testResult?: ConnectionTestResult | null;
}

@Component({
  selector: 'app-exchange-list',
  templateUrl: './exchange-list.component.html',
  styleUrls: []
})
export class ExchangeListComponent implements OnInit {
  exchanges: ExchangeWithTestState[] = [];
  displayedColumns: string[] = ['name', 'exchangeName', 'status', 'apiKey', 'actions'];
  isLoading = true;
  confirmDeleteId: number | null = null;
  errorMessage: string | null = null;

  constructor(private exchangeService: ExchangeService, private dialog: MatDialog) { }

  ngOnInit(): void {
    this.loadExchanges();
  }

  loadExchanges(): void {
    this.isLoading = true;
    this.errorMessage = null;
    this.confirmDeleteId = null;
    this.exchangeService.getUserExchanges().subscribe({
      next: (data) => {
        this.exchanges = data.map(e => ({ ...e, testing: false, testResult: null }));
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load exchanges', err);
        this.isLoading = false;
      }
    });
  }

  openAddExchangeDialog(): void {
    const dialogRef = this.dialog.open(AddExchangeDialogComponent, { width: '400px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadExchanges();
    });
  }

  testConnection(exchange: ExchangeWithTestState): void {
    exchange.testing = true;
    exchange.testResult = null;
    this.exchangeService.testConnection(exchange.id).subscribe({
      next: (result) => {
        exchange.testing = false;
        exchange.testResult = result;
        // Also update status badge if needed
        if (result.success) exchange.status = 'Active';
      },
      error: (err) => {
        exchange.testing = false;
        exchange.testResult = {
          success: false,
          message: err.error?.message || 'Connection failed. Check your API keys.'
        };
        exchange.status = 'Error';
      }
    });
  }

  isCoinDcx(exchange: ExchangeWithTestState): boolean {
    return exchange.exchangeName.toLowerCase().includes('coindcx');
  }

  requestDelete(id: number): void {
    this.confirmDeleteId = id;
    this.errorMessage = null;
  }

  cancelDelete(): void {
    this.confirmDeleteId = null;
  }

  confirmDelete(id: number): void {
    this.exchangeService.deleteExchange(id).subscribe({
      next: () => {
        this.confirmDeleteId = null;
        this.loadExchanges();
      },
      error: (err) => {
        this.confirmDeleteId = null;
        this.errorMessage = 'Cannot delete: ' + (err.error?.message || err.message || 'Unknown error');
      }
    });
  }
}
