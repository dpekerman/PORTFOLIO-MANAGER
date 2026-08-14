-- Script 12: Add DecisionSourceClosed column to PortfolioItems
-- Run this script after 11_AddIdentityAndAuth.sql

ALTER TABLE PortfolioItems
ADD DecisionSourceClosed NVARCHAR(50) NULL;
GO
