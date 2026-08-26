/*
    GiveAID Database
    Step 01: Create the database safely.

    This script creates the GiveAID database only when it
    does not already exist. Existing data is not deleted.
*/

USE master;
GO

IF DB_ID(N'GiveAID') IS NULL
BEGIN
    CREATE DATABASE [GiveAID];
    PRINT 'GiveAID database created successfully.';
END
ELSE
BEGIN
    PRINT 'GiveAID database already exists. No changes were made.';
END
GO

USE [GiveAID];
GO