/* ============================================================
   GiveAID - Contact Reply Workflow Migration
   Purpose:
     1. Link contact messages to registered users.
     2. Store an administrator reply inside the database.
     3. Record which administrator replied and when.
     4. Support a secure "My Messages" user-dashboard module.

   This script is idempotent:
     - It does not drop any table or existing record.
     - It can be executed again safely.
   ============================================================ */

USE GiveAID;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ========================================================
       1. ADD USER AND REPLY COLUMNS
       ======================================================== */

    IF COL_LENGTH(N'dbo.ContactMessages', N'UserID') IS NULL
    BEGIN
        ALTER TABLE dbo.ContactMessages
        ADD UserID INT NULL;
    END;

    IF COL_LENGTH(N'dbo.ContactMessages', N'AdminReply') IS NULL
    BEGIN
        ALTER TABLE dbo.ContactMessages
        ADD AdminReply NVARCHAR(2000) NULL;
    END;

    IF COL_LENGTH(N'dbo.ContactMessages', N'RepliedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.ContactMessages
        ADD RepliedAt DATETIME2(0) NULL;
    END;

    IF COL_LENGTH(N'dbo.ContactMessages', N'RepliedByUserID') IS NULL
    BEGIN
        ALTER TABLE dbo.ContactMessages
        ADD RepliedByUserID INT NULL;
    END;

    /* ========================================================
       2. LINK EXISTING MESSAGES TO REGISTERED USERS

       Users.Email is the account login identity. Existing
       messages are linked only where the stored email matches
       an existing registered account.
       ======================================================== */

    UPDATE messageRecord
    SET messageRecord.UserID = account.UserID
    FROM dbo.ContactMessages AS messageRecord
    INNER JOIN dbo.Users AS account
        ON LOWER(LTRIM(RTRIM(account.Email))) =
           LOWER(LTRIM(RTRIM(messageRecord.Email)))
    WHERE messageRecord.UserID IS NULL;

    /* ========================================================
       3. ADD FOREIGN KEYS
       ======================================================== */

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ContactMessages_User'
          AND parent_object_id = OBJECT_ID(N'dbo.ContactMessages')
    )
    BEGIN
        ALTER TABLE dbo.ContactMessages WITH CHECK
        ADD CONSTRAINT FK_ContactMessages_User
            FOREIGN KEY (UserID)
            REFERENCES dbo.Users (UserID);

        ALTER TABLE dbo.ContactMessages
        CHECK CONSTRAINT FK_ContactMessages_User;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ContactMessages_RepliedByUser'
          AND parent_object_id = OBJECT_ID(N'dbo.ContactMessages')
    )
    BEGIN
        ALTER TABLE dbo.ContactMessages WITH CHECK
        ADD CONSTRAINT FK_ContactMessages_RepliedByUser
            FOREIGN KEY (RepliedByUserID)
            REFERENCES dbo.Users (UserID);

        ALTER TABLE dbo.ContactMessages
        CHECK CONSTRAINT FK_ContactMessages_RepliedByUser;
    END;

    /* ========================================================
       4. ADD PERFORMANCE INDEXES
       ======================================================== */

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ContactMessages_UserID_CreatedAt'
          AND object_id = OBJECT_ID(N'dbo.ContactMessages')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_ContactMessages_UserID_CreatedAt
            ON dbo.ContactMessages (UserID, CreatedAt DESC)
            INCLUDE (Subject, Status, RepliedAt);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ContactMessages_RepliedByUserID'
          AND object_id = OBJECT_ID(N'dbo.ContactMessages')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_ContactMessages_RepliedByUserID
            ON dbo.ContactMessages (RepliedByUserID)
            WHERE RepliedByUserID IS NOT NULL;
    END;

    COMMIT TRANSACTION;

    PRINT N'GiveAID contact reply workflow migration completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO

/* ============================================================
   5. VERIFICATION
   ============================================================ */

SELECT
    columnInfo.COLUMN_NAME,
    columnInfo.DATA_TYPE,
    columnInfo.CHARACTER_MAXIMUM_LENGTH,
    columnInfo.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS AS columnInfo
WHERE columnInfo.TABLE_SCHEMA = N'dbo'
  AND columnInfo.TABLE_NAME = N'ContactMessages'
  AND columnInfo.COLUMN_NAME IN
      (N'UserID', N'AdminReply', N'RepliedAt', N'RepliedByUserID')
ORDER BY columnInfo.ORDINAL_POSITION;

SELECT
    COUNT(1) AS TotalContactMessages,
    SUM(CASE WHEN UserID IS NOT NULL THEN 1 ELSE 0 END) AS LinkedToRegisteredUsers,
    SUM(CASE WHEN UserID IS NULL THEN 1 ELSE 0 END) AS AnonymousMessages,
    SUM(CASE WHEN AdminReply IS NOT NULL THEN 1 ELSE 0 END) AS MessagesWithReplies
FROM dbo.ContactMessages;
GO
