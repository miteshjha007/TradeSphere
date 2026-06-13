export interface Backtest {
    id: number;
    strategyId: number;
    strategyName: string;
    symbol: string;
    interval: string;
    startDate: Date;
    endDate: Date;
    totalReturn: number;
    maxDrawdown: number;
    tradeCount: number;
    status: string;
}

export interface BacktestResultDetails extends Backtest {
    resultJson: string;
}

export interface RunBacktestDto {
    strategyId: number;
    symbol: string;
    interval: string;
    startDate: Date;
    endDate: Date;
    initialCapital: number;
    configOverrides?: string;
}
