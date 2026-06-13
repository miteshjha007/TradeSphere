-- TradeSphere Database Schema

-- Users Table
CREATE TABLE Users (
    Id SERIAL PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    Role VARCHAR(20) NOT NULL CHECK (Role IN ('Admin', 'User')),
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Exchanges Table (System supported exchanges)
CREATE TABLE Exchanges (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    BaseUrl VARCHAR(255) NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE
);

-- Coins Table (Market Data)
CREATE TABLE Coins (
    Id SERIAL PRIMARY KEY,
    Symbol VARCHAR(20) NOT NULL UNIQUE, -- e.g., BTCUSD
    Name VARCHAR(50) NOT NULL, -- e.g., Bitcoin
    ExchangeId INT REFERENCES Exchanges(Id), -- Primary exchange for price
    IsActive BOOLEAN DEFAULT TRUE
);

-- Initial Seed for Exchanges
INSERT INTO Exchanges (Name, BaseUrl) VALUES 
('Delta Exchange', 'https://api.delta.exchange'),
('Cosmic Exchange', 'https://api.cosmic.exchange');

-- User Exchanges (Connected API Keys)
CREATE TABLE UserExchanges (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ExchangeId INT NOT NULL REFERENCES Exchanges(Id),
    Name VARCHAR(50), -- Nickname for the key
    ApiKey TEXT NOT NULL, -- Encrypted
    ApiSecret TEXT NOT NULL, -- Encrypted
    Status VARCHAR(20) DEFAULT 'Active', -- Active, Error, Expired
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Strategies Table (Templates)
CREATE TABLE Strategies (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Description TEXT,
    LogicType VARCHAR(50) NOT NULL, -- e.g., 'MovingAverage', 'RSI', 'Custom'
    DefaultConfig JSONB, -- Default parameters
    IsPublic BOOLEAN DEFAULT TRUE,
    CreatedBy INT REFERENCES Users(Id) -- Null for system strategies
);

-- User Deployed Strategies
CREATE TABLE UserStrategies (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    StrategyId INT NOT NULL REFERENCES Strategies(Id),
    ExchangeId INT NOT NULL REFERENCES Exchanges(Id),
    Symbol VARCHAR(20) NOT NULL,
    Config JSONB, -- User specific parameters
    Status VARCHAR(20) DEFAULT 'Stopped', -- Running, Stopped, Error
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    StartedAt TIMESTAMP WITH TIME ZONE,
    StoppedAt TIMESTAMP WITH TIME ZONE
);

-- Trades Table
CREATE TABLE Trades (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    UserStrategyId INT REFERENCES UserStrategies(Id), -- Null if manual trade
    ExchangeId INT NOT NULL REFERENCES Exchanges(Id),
    Symbol VARCHAR(20) NOT NULL,
    Side VARCHAR(10) NOT NULL CHECK (Side IN ('Buy', 'Sell')),
    OrderType VARCHAR(20) NOT NULL DEFAULT 'Market',
    Price DECIMAL(18, 8),
    Quantity DECIMAL(18, 8) NOT NULL,
    Status VARCHAR(20) NOT NULL, -- Open, Filled, Canceled, Failed
    ExecutedAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    Pnl DECIMAL(18, 8) DEFAULT 0,
    ExternalOrderId VARCHAR(100)
);

-- Backtest Results
CREATE TABLE Backtests (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    StrategyId INT REFERENCES Strategies(Id),
    Symbol VARCHAR(20) NOT NULL,
    StartDate TIMESTAMP WITH TIME ZONE NOT NULL,
    EndDate TIMESTAMP WITH TIME ZONE NOT NULL,
    InitialCapital DECIMAL(18, 2) DEFAULT 1000,
    TotalReturn DECIMAL(18, 2),
    MaxDrawdown DECIMAL(18, 2),
    TotalTrades INT,
    WinRate DECIMAL(5, 2),
    ResultJson JSONB, -- Detailed detailed chart data
    RanAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- AI Screener Results
CREATE TABLE AiScreenerResults (
    Id SERIAL PRIMARY KEY,
    Symbol VARCHAR(20) NOT NULL,
    Timeframe VARCHAR(10) NOT NULL,
    Signal VARCHAR(20), -- Bullish, Bearish, Neutral
    ConfidenceScore DECIMAL(5, 2),
    AnalysisData JSONB,
    GeneratedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance
CREATE INDEX idx_trades_userid ON Trades(UserId);
CREATE INDEX idx_userstrategies_userid ON UserStrategies(UserId);
CREATE INDEX idx_backtests_userid ON Backtests(UserId);
