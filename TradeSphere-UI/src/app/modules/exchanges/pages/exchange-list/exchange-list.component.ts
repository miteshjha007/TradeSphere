import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ExchangeService } from '../../services/exchange.service';
import { UserExchange } from '../../models/exchange.model';
import { AddExchangeDialogComponent } from '../../components/add-exchange-dialog/add-exchange-dialog.component';

@Component({
  selector: 'app-exchange-list',
  templateUrl: './exchange-list.component.html',
  styleUrls: []
})
export class ExchangeListComponent implements OnInit {
  exchanges: UserExchange[] = [];
  displayedColumns: string[] = ['name', 'exchangeName', 'status', 'apiKey', 'actions'];
  isLoading = true;

  constructor(private exchangeService: ExchangeService, private dialog: MatDialog) { }

  ngOnInit(): void {
    this.loadExchanges();
  }

  loadExchanges(): void {
    this.isLoading = true;
    this.exchangeService.getUserExchanges().subscribe({
      next: (data) => {
        this.exchanges = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load exchanges', err);
        this.isLoading = false;
      }
    });
  }

  openAddExchangeDialog(): void {
    const dialogRef = this.dialog.open(AddExchangeDialogComponent, {
      width: '400px'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadExchanges(); // Refresh list if exchange added
      }
    });
  }

  deleteExchange(id: number): void {
    if (confirm('Are you sure you want to disconnect this exchange?')) {
      this.exchangeService.deleteExchange(id).subscribe(() => {
        this.loadExchanges();
      });
    }
  }
}
