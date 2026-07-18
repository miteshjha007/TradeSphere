import { Component } from '@angular/core';
import { StockAnalysis, StockPicksService } from '../../services/stock-picks.service';

interface ResearchLink {
  label: string;
  hint: string;
  url: string;
}

@Component({
  selector: 'app-stock-analyzer',
  templateUrl: './stock-analyzer.component.html',
  styleUrls: []
})
export class StockAnalyzerComponent {
  symbol = 'RELIANCE';
  horizon: 'ShortTerm' | 'LongTerm' = 'ShortTerm';
  isLoading = false;
  errorMessage: string | null = null;
  analysis: StockAnalysis | null = null;

  constructor(private stockPicksService: StockPicksService) { }

  analyze(): void {
    const cleanSymbol = this.symbol.trim().toUpperCase().replace('NSE:', '').replace('.NS', '');
    if (!cleanSymbol) {
      this.errorMessage = 'Please enter a stock symbol.';
      return;
    }

    this.symbol = cleanSymbol;
    this.isLoading = true;
    this.errorMessage = null;
    this.analysis = null;

    this.stockPicksService.analyzeStock({ symbol: cleanSymbol, horizon: this.horizon }).subscribe({
      next: data => {
        this.analysis = data;
        this.isLoading = false;
      },
      error: err => {
        this.errorMessage = err.error?.message || 'Could not analyze this stock right now.';
        this.isLoading = false;
      }
    });
  }

  verdictClass(verdict?: string): string {
    const value = (verdict || '').toLowerCase();
    if (value.includes('buy')) {
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    }

    if (value.includes('avoid')) {
      return 'border-red-200 bg-red-50 text-red-700';
    }

    if (value.includes('wait')) {
      return 'border-amber-200 bg-amber-50 text-amber-700';
    }

    return 'border-slate-200 bg-slate-50 text-slate-700';
  }

  scoreClass(score?: number): string {
    const value = score || 0;
    if (value >= 72) {
      return 'text-emerald-600';
    }

    if (value >= 58) {
      return 'text-blue-600';
    }

    if (value >= 45) {
      return 'text-amber-600';
    }

    return 'text-red-500';
  }

  formatDate(date?: string): string {
    return date ? new Date(date).toLocaleString('en-IN', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-';
  }

  tradingViewUrl(symbol?: string): string {
    const cleanSymbol = this.cleanSymbol(symbol);
    return `https://www.tradingview.com/chart/?symbol=NSE%3A${encodeURIComponent(cleanSymbol)}`;
  }

  researchLinks(symbol?: string): ResearchLink[] {
    const cleanSymbol = this.cleanSymbol(symbol);
    return [
      {
        label: 'Screener FA',
        hint: '10Y P&L, balance sheet, cash flow, ratios, annual reports',
        url: `https://www.screener.in/company/${encodeURIComponent(cleanSymbol)}/consolidated/`
      },
      {
        label: 'NSE Quote',
        hint: 'Official exchange quote, corporate actions, announcements',
        url: `https://www.nseindia.com/get-quotes/equity?symbol=${encodeURIComponent(cleanSymbol)}`
      },
      {
        label: 'Tickertape',
        hint: 'Beginner-friendly checklist, peer view, valuation context',
        url: `https://www.tickertape.in/search?text=${encodeURIComponent(cleanSymbol)}`
      },
      {
        label: 'Trendlyne',
        hint: 'DVM score, broker views, institutional activity context',
        url: `https://trendlyne.com/search/?q=${encodeURIComponent(cleanSymbol)}`
      }
    ];
  }

  private cleanSymbol(symbol?: string): string {
    return (symbol || this.symbol || '')
      .trim()
      .toUpperCase()
      .replace('NSE:', '')
      .replace('.NS', '');
  }
}


