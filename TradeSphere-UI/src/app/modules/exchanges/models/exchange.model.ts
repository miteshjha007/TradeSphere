export interface Exchange {
    id: number;
    name: string;
    baseUrl: string;
}

export interface UserExchange {
    id: number;
    exchangeName: string;
    name: string;
    status: string;
    apiKeyPreview: string;
}

export interface ConnectExchangeDto {
    exchangeId: number;
    name: string;
    apiKey: string;
    apiSecret: string;
}
