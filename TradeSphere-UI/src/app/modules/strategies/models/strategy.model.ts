export interface Strategy {
    id: number;
    name: string;
    description: string;
    logicType: string;
    defaultConfig: string;
    isPublic: boolean;
    createdBy: number;
}

export interface UserStrategy {
    id: number;
    strategyId: number;
    strategyName: string;
    exchangeId: number;
    exchangeName: string;
    executionProvider: string;
    mt5AccountId?: number;
    mt5AccountName?: string;
    symbol: string;
    config: string;
    status: string;
    startedAt?: Date;
}

export interface DeployStrategyDto {
    strategyId: number;
    executionProvider: string;
    userExchangeId?: number;
    mt5AccountId?: number;
    symbol: string;
    config: string;
}

export interface CreateStrategyDto {
    name: string;
    description: string;
    logicType: string; // e.g., 'RSI_Crossover', 'MACD', 'Custom'
    defaultConfig: string;
    isPublic: boolean;
}
