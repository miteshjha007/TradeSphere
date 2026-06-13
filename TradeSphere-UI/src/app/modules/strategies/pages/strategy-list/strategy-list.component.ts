import { Component, OnInit } from '@angular/core';
import { StrategyService } from '../../services/strategy.service';
import { UserStrategy, Strategy } from '../../models/strategy.model';
import { MatDialog } from '@angular/material/dialog';
import { StrategyCreateWizardComponent } from '../../components/strategy-create-wizard/strategy-create-wizard.component';

@Component({
  selector: 'app-strategy-list',
  templateUrl: './strategy-list.component.html',
  styleUrls: []
})
export class StrategyListComponent implements OnInit {
  activeStrategies: UserStrategy[] = [];
  availableStrategies: Strategy[] = [];
  isLoading = true;

  constructor(private strategyService: StrategyService, private dialog: MatDialog) { }

  ngOnInit(): void {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;
    this.strategyService.getUserStrategies().subscribe({
      next: (data: UserStrategy[]) => {
        this.activeStrategies = data;
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });

    // Also load available templates if needed, or in a separate tab
  }

  toggleStatus(strategy: UserStrategy) {
    const newStatus = strategy.status === 'Running' ? 'Stopped' : 'Running';
    this.strategyService.toggleStatus(strategy.id, newStatus).subscribe(() => {
      strategy.status = newStatus;
    });
  }

  deleteStrategy(id: number) {
    if (confirm('Are you sure you want to delete this strategy instance?')) {
      this.strategyService.deleteStrategy(id).subscribe(() => {
        this.loadData();
      });
    }
  }

  openCreateWizard() {
    // Logic to open wizard or navigate to create page
    // alert('Wizard to be implemented');
  }
}
