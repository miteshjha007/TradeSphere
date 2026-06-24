import { Component, OnInit } from '@angular/core';
import { StrategyService } from '../../services/strategy.service';
import { UserStrategy, Strategy } from '../../models/strategy.model';
import { MatDialog } from '@angular/material/dialog';

@Component({
  selector: 'app-strategy-list',
  templateUrl: './strategy-list.component.html',
  styleUrls: []
})
export class StrategyListComponent implements OnInit {
  activeStrategies: UserStrategy[] = [];
  availableStrategies: Strategy[] = [];
  isLoading = true;
  confirmDeleteId: number | null = null;  // Track which strategy is awaiting delete confirm
  errorMessage: string | null = null;

  constructor(private strategyService: StrategyService, private dialog: MatDialog) { }

  ngOnInit(): void {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;
    this.errorMessage = null;
    this.confirmDeleteId = null;
    this.strategyService.getUserStrategies().subscribe({
      next: (data: UserStrategy[]) => {
        this.activeStrategies = data;
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });
  }

  toggleStatus(strategy: UserStrategy) {
    const newStatus = strategy.status === 'Running' ? 'Stopped' : 'Running';
    this.strategyService.toggleStatus(strategy.id, newStatus).subscribe({
      next: () => {
        strategy.status = newStatus;
      },
      error: (err) => {
        this.errorMessage = 'Failed to update status: ' + (err.error?.message || err.message);
      }
    });
  }

  requestDelete(id: number) {
    this.confirmDeleteId = id;   // Show inline confirmation for this strategy
    this.errorMessage = null;
  }

  cancelDelete() {
    this.confirmDeleteId = null;
  }

  confirmDelete(id: number) {
    this.strategyService.deleteStrategy(id).subscribe({
      next: () => {
        this.confirmDeleteId = null;
        this.loadData();
      },
      error: (err) => {
        this.confirmDeleteId = null;
        this.errorMessage = 'Cannot delete: ' + (err.error?.message || err.message || 'Unknown error');
      }
    });
  }

  getPositionLabel(position: number | undefined): string {
    if (position === 1) {
      return 'Long open';
    }

    if (position === -1) {
      return 'Short open';
    }

    return 'Flat';
  }

  getHealthTone(status: string | undefined): string {
    const value = (status || '').toLowerCase();
    if (value.includes('ready') || value.includes('managing')) {
      return 'bg-green-50 text-green-700 border-green-200';
    }

    if (value.includes('waiting')) {
      return 'bg-amber-50 text-amber-700 border-amber-200';
    }

    return 'bg-slate-50 text-slate-600 border-slate-200';
  }
}
