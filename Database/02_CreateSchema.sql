/*
    GiveAID Database
    Step 02: Create the application schema.

    Scope:
      - Public website
      - Registered users
      - Administrator-managed NGOs
      - Causes, programmes, donations and dummy payments
      - Programme participation, partners and gallery
      - Help-centre queries, invitations and contact messages
      - Editable website pages

    This script is non-destructive. It creates a table only when that
    table does not already exist. It never drops tables or deletes data.
*/

USE [GiveAID];
GO

/* ================================================================
   1. ROLES
   ================================================================ */
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        RoleID      INT IDENTITY(1,1) NOT NULL,
        RoleName    NVARCHAR(50) NOT NULL,
        Description NVARCHAR(250) NULL,
        CONSTRAINT PK_Roles PRIMARY KEY (RoleID),
        CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
    );
END
GO

/* ================================================================
   2. USERS
   ================================================================ */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserID          INT IDENTITY(1,1) NOT NULL,
        FullName        NVARCHAR(150) NOT NULL,
        Email           NVARCHAR(256) NOT NULL,
        PasswordHash    NVARCHAR(500) NOT NULL,
        Phone           NVARCHAR(30) NULL,
        Gender          NVARCHAR(20) NULL,
        Profession      NVARCHAR(120) NULL,
        Address         NVARCHAR(500) NULL,
        City            NVARCHAR(100) NULL,
        Country         NVARCHAR(100) NULL,
        ProfileImageURL NVARCHAR(500) NULL,
        IsActive        BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       DATETIME2(0) NULL,
        LastLoginAt     DATETIME2(0) NULL,
        CONSTRAINT PK_Users PRIMARY KEY (UserID),
        CONSTRAINT UQ_Users_Email UNIQUE (Email),
        CONSTRAINT CK_Users_Gender CHECK
        (
            Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other', N'Prefer not to say')
        )
    );
END
GO

/* ================================================================
   3. USER ROLES
   ================================================================ */
IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles
    (
        UserID    INT NOT NULL,
        RoleID    INT NOT NULL,
        AssignedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_UserRoles PRIMARY KEY (UserID, RoleID),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleID) REFERENCES dbo.Roles(RoleID)
    );
END
GO

/* ================================================================
   4. ASSOCIATED NGOs (managed by Admin; no NGO login/dashboard)
   ================================================================ */
IF OBJECT_ID(N'dbo.NGOs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NGOs
    (
        NGOID              INT IDENTITY(1,1) NOT NULL,
        NGOName            NVARCHAR(180) NOT NULL,
        RegistrationNumber NVARCHAR(100) NULL,
        Category           NVARCHAR(100) NULL,
        Description        NVARCHAR(2000) NULL,
        Email              NVARCHAR(256) NULL,
        Phone              NVARCHAR(30) NULL,
        Address            NVARCHAR(500) NULL,
        City               NVARCHAR(100) NULL,
        Country            NVARCHAR(100) NULL,
        WebsiteURL         NVARCHAR(500) NULL,
        LogoURL            NVARCHAR(500) NULL,
        ContactPerson      NVARCHAR(150) NULL,
        IsActive           BIT NOT NULL CONSTRAINT DF_NGOs_IsActive DEFAULT (1),
        CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_NGOs_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt          DATETIME2(0) NULL,
        CONSTRAINT PK_NGOs PRIMARY KEY (NGOID),
        CONSTRAINT UQ_NGOs_NGOName UNIQUE (NGOName)
    );
END
GO

/* ================================================================
   5. CAUSES
   ================================================================ */
IF OBJECT_ID(N'dbo.Causes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Causes
    (
        CauseID      INT IDENTITY(1,1) NOT NULL,
        CauseName    NVARCHAR(120) NOT NULL,
        Slug         NVARCHAR(140) NOT NULL,
        ShortDescription NVARCHAR(300) NULL,
        Description  NVARCHAR(2000) NULL,
        ImageURL     NVARCHAR(500) NULL,
        IconName     NVARCHAR(100) NULL,
        IsFeatured   BIT NOT NULL CONSTRAINT DF_Causes_IsFeatured DEFAULT (0),
        IsActive     BIT NOT NULL CONSTRAINT DF_Causes_IsActive DEFAULT (1),
        DisplayOrder INT NOT NULL CONSTRAINT DF_Causes_DisplayOrder DEFAULT (0),
        CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Causes_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt    DATETIME2(0) NULL,
        CONSTRAINT PK_Causes PRIMARY KEY (CauseID),
        CONSTRAINT UQ_Causes_CauseName UNIQUE (CauseName),
        CONSTRAINT UQ_Causes_Slug UNIQUE (Slug),
        CONSTRAINT CK_Causes_DisplayOrder CHECK (DisplayOrder >= 0)
    );
END
GO

/* ================================================================
   6. PROGRAMMES
   ================================================================ */
IF OBJECT_ID(N'dbo.Programmes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Programmes
    (
        ProgrammeID   INT IDENTITY(1,1) NOT NULL,
        NGOID          INT NOT NULL,
        CauseID        INT NOT NULL,
        ProgrammeName  NVARCHAR(180) NOT NULL,
        Slug           NVARCHAR(200) NOT NULL,
        ShortDescription NVARCHAR(350) NULL,
        Description    NVARCHAR(3000) NULL,
        Location       NVARCHAR(500) NULL,
        StartDate      DATE NOT NULL,
        EndDate        DATE NULL,
        TargetAmount   DECIMAL(18,2) NOT NULL CONSTRAINT DF_Programmes_TargetAmount DEFAULT (0),
        ImageURL       NVARCHAR(500) NULL,
        Status         NVARCHAR(20) NOT NULL CONSTRAINT DF_Programmes_Status DEFAULT (N'Upcoming'),
        IsFeatured     BIT NOT NULL CONSTRAINT DF_Programmes_IsFeatured DEFAULT (0),
        CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Programmes_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt      DATETIME2(0) NULL,
        CONSTRAINT PK_Programmes PRIMARY KEY (ProgrammeID),
        CONSTRAINT FK_Programmes_NGOs FOREIGN KEY (NGOID) REFERENCES dbo.NGOs(NGOID),
        CONSTRAINT FK_Programmes_Causes FOREIGN KEY (CauseID) REFERENCES dbo.Causes(CauseID),
        CONSTRAINT UQ_Programmes_Slug UNIQUE (Slug),
        CONSTRAINT CK_Programmes_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate),
        CONSTRAINT CK_Programmes_TargetAmount CHECK (TargetAmount >= 0),
        CONSTRAINT CK_Programmes_Status CHECK
        (
            Status IN (N'Upcoming', N'Active', N'Completed', N'Cancelled')
        )
    );
END
GO

/* ================================================================
   7. PROGRAMME INTERESTS / PARTICIPATION
   ================================================================ */
IF OBJECT_ID(N'dbo.ProgrammeInterests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProgrammeInterests
    (
        InterestID  INT IDENTITY(1,1) NOT NULL,
        UserID      INT NOT NULL,
        ProgrammeID INT NOT NULL,
        Message     NVARCHAR(500) NULL,
        Status      NVARCHAR(20) NOT NULL CONSTRAINT DF_ProgrammeInterests_Status DEFAULT (N'Interested'),
        CreatedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_ProgrammeInterests_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt   DATETIME2(0) NULL,
        CONSTRAINT PK_ProgrammeInterests PRIMARY KEY (InterestID),
        CONSTRAINT FK_ProgrammeInterests_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_ProgrammeInterests_Programmes FOREIGN KEY (ProgrammeID) REFERENCES dbo.Programmes(ProgrammeID),
        CONSTRAINT UQ_ProgrammeInterests_UserProgramme UNIQUE (UserID, ProgrammeID),
        CONSTRAINT CK_ProgrammeInterests_Status CHECK
        (
            Status IN (N'Interested', N'Confirmed', N'Cancelled')
        )
    );
END
GO

/* ================================================================
   8. DONATIONS (registered users only)
   ================================================================ */
IF OBJECT_ID(N'dbo.Donations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Donations
    (
        DonationID       INT IDENTITY(1,1) NOT NULL,
        UserID           INT NOT NULL,
        CauseID          INT NOT NULL,
        NGOID            INT NULL,
        ProgrammeID      INT NULL,
        Amount           DECIMAL(18,2) NOT NULL,
        CurrencyCode     CHAR(3) NOT NULL CONSTRAINT DF_Donations_CurrencyCode DEFAULT ('PKR'),
        DonorMessage     NVARCHAR(500) NULL,
        IsAnonymous      BIT NOT NULL CONSTRAINT DF_Donations_IsAnonymous DEFAULT (0),
        DonationStatus   NVARCHAR(20) NOT NULL CONSTRAINT DF_Donations_Status DEFAULT (N'Pending'),
        DonationDate     DATETIME2(0) NOT NULL CONSTRAINT DF_Donations_Date DEFAULT (SYSUTCDATETIME()),
        CompletedAt      DATETIME2(0) NULL,
        CONSTRAINT PK_Donations PRIMARY KEY (DonationID),
        CONSTRAINT FK_Donations_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_Donations_Causes FOREIGN KEY (CauseID) REFERENCES dbo.Causes(CauseID),
        CONSTRAINT FK_Donations_NGOs FOREIGN KEY (NGOID) REFERENCES dbo.NGOs(NGOID),
        CONSTRAINT FK_Donations_Programmes FOREIGN KEY (ProgrammeID) REFERENCES dbo.Programmes(ProgrammeID),
        CONSTRAINT CK_Donations_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Donations_CurrencyCode CHECK (CurrencyCode IN ('PKR', 'MYR', 'USD')),
        CONSTRAINT CK_Donations_Status CHECK
        (
            DonationStatus IN (N'Pending', N'Completed', N'Failed', N'Cancelled')
        )
    );
END
GO

/* ================================================================
   9. DUMMY PAYMENTS
   Never store a full card number, CVV or PIN.
   ================================================================ */
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments
    (
        PaymentID       INT IDENTITY(1,1) NOT NULL,
        DonationID      INT NOT NULL,
        PaymentReference NVARCHAR(100) NOT NULL,
        PaymentMethod   NVARCHAR(30) NOT NULL,
        CardBrand       NVARCHAR(30) NULL,
        CardLastFour    CHAR(4) NULL,
        Amount          DECIMAL(18,2) NOT NULL,
        CurrencyCode    CHAR(3) NOT NULL,
        PaymentStatus   NVARCHAR(20) NOT NULL CONSTRAINT DF_Payments_Status DEFAULT (N'Pending'),
        ProcessedAt     DATETIME2(0) NULL,
        CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Payments PRIMARY KEY (PaymentID),
        CONSTRAINT FK_Payments_Donations FOREIGN KEY (DonationID) REFERENCES dbo.Donations(DonationID),
        CONSTRAINT UQ_Payments_DonationID UNIQUE (DonationID),
        CONSTRAINT UQ_Payments_Reference UNIQUE (PaymentReference),
        CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Payments_Method CHECK
        (
            PaymentMethod IN (N'Credit Card', N'Debit Card', N'Bank Transfer', N'Dummy Payment')
        ),
        CONSTRAINT CK_Payments_CardLastFour CHECK
        (
            CardLastFour IS NULL OR CardLastFour NOT LIKE '%[^0-9]%'
        ),
        CONSTRAINT CK_Payments_Status CHECK
        (
            PaymentStatus IN (N'Pending', N'Successful', N'Failed', N'Refunded')
        )
    );
END
GO

/* ================================================================
   10. PARTNERS
   ================================================================ */
IF OBJECT_ID(N'dbo.Partners', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Partners
    (
        PartnerID    INT IDENTITY(1,1) NOT NULL,
        PartnerName  NVARCHAR(180) NOT NULL,
        Description  NVARCHAR(1000) NULL,
        LogoURL      NVARCHAR(500) NULL,
        WebsiteURL   NVARCHAR(500) NULL,
        IsActive     BIT NOT NULL CONSTRAINT DF_Partners_IsActive DEFAULT (1),
        DisplayOrder INT NOT NULL CONSTRAINT DF_Partners_DisplayOrder DEFAULT (0),
        CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Partners_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt    DATETIME2(0) NULL,
        CONSTRAINT PK_Partners PRIMARY KEY (PartnerID),
        CONSTRAINT UQ_Partners_PartnerName UNIQUE (PartnerName),
        CONSTRAINT CK_Partners_DisplayOrder CHECK (DisplayOrder >= 0)
    );
END
GO

/* ================================================================
   11. GALLERY ITEMS
   ================================================================ */
IF OBJECT_ID(N'dbo.GalleryItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GalleryItems
    (
        GalleryItemID INT IDENTITY(1,1) NOT NULL,
        ProgrammeID   INT NULL,
        NGOID          INT NULL,
        Title          NVARCHAR(180) NOT NULL,
        Caption        NVARCHAR(600) NULL,
        ImageURL       NVARCHAR(500) NOT NULL,
        AltText        NVARCHAR(250) NOT NULL,
        IsFeatured     BIT NOT NULL CONSTRAINT DF_GalleryItems_IsFeatured DEFAULT (0),
        IsActive       BIT NOT NULL CONSTRAINT DF_GalleryItems_IsActive DEFAULT (1),
        UploadedBy     INT NOT NULL,
        UploadedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_GalleryItems_UploadedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_GalleryItems PRIMARY KEY (GalleryItemID),
        CONSTRAINT FK_GalleryItems_Programmes FOREIGN KEY (ProgrammeID) REFERENCES dbo.Programmes(ProgrammeID),
        CONSTRAINT FK_GalleryItems_NGOs FOREIGN KEY (NGOID) REFERENCES dbo.NGOs(NGOID),
        CONSTRAINT FK_GalleryItems_Users FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(UserID)
    );
END
GO

/* ================================================================
   12. HELP-CENTRE QUERIES
   ================================================================ */
IF OBJECT_ID(N'dbo.Queries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Queries
    (
        QueryID    INT IDENTITY(1,1) NOT NULL,
        UserID     INT NOT NULL,
        Subject    NVARCHAR(200) NOT NULL,
        Message    NVARCHAR(2000) NOT NULL,
        Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_Queries_Status DEFAULT (N'Open'),
        CreatedAt  DATETIME2(0) NOT NULL CONSTRAINT DF_Queries_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt  DATETIME2(0) NULL,
        CONSTRAINT PK_Queries PRIMARY KEY (QueryID),
        CONSTRAINT FK_Queries_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT CK_Queries_Status CHECK
        (
            Status IN (N'Open', N'In Progress', N'Resolved', N'Closed')
        )
    );
END
GO

/* ================================================================
   13. QUERY REPLIES
   ================================================================ */
IF OBJECT_ID(N'dbo.QueryReplies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QueryReplies
    (
        ReplyID     INT IDENTITY(1,1) NOT NULL,
        QueryID     INT NOT NULL,
        RepliedBy   INT NOT NULL,
        ReplyMessage NVARCHAR(2000) NOT NULL,
        RepliedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_QueryReplies_RepliedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_QueryReplies PRIMARY KEY (ReplyID),
        CONSTRAINT FK_QueryReplies_Queries FOREIGN KEY (QueryID) REFERENCES dbo.Queries(QueryID),
        CONSTRAINT FK_QueryReplies_Users FOREIGN KEY (RepliedBy) REFERENCES dbo.Users(UserID)
    );
END
GO

/* ================================================================
   14. FRIEND INVITATIONS
   ================================================================ */
IF OBJECT_ID(N'dbo.Invitations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invitations
    (
        InvitationID   INT IDENTITY(1,1) NOT NULL,
        UserID         INT NOT NULL,
        RecipientName  NVARCHAR(150) NULL,
        RecipientEmail NVARCHAR(256) NOT NULL,
        InvitationMessage NVARCHAR(1000) NULL,
        Status         NVARCHAR(20) NOT NULL CONSTRAINT DF_Invitations_Status DEFAULT (N'Created'),
        CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Invitations_CreatedAt DEFAULT (SYSUTCDATETIME()),
        SentAt         DATETIME2(0) NULL,
        CONSTRAINT PK_Invitations PRIMARY KEY (InvitationID),
        CONSTRAINT FK_Invitations_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT CK_Invitations_Status CHECK
        (
            Status IN (N'Created', N'Sent', N'Failed')
        )
    );
END
GO

/* ================================================================
   15. PUBLIC CONTACT MESSAGES
   ================================================================ */
IF OBJECT_ID(N'dbo.ContactMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContactMessages
    (
        ContactMessageID INT IDENTITY(1,1) NOT NULL,
        FullName         NVARCHAR(150) NOT NULL,
        Email            NVARCHAR(256) NOT NULL,
        Phone            NVARCHAR(30) NULL,
        Subject          NVARCHAR(200) NOT NULL,
        Message          NVARCHAR(2000) NOT NULL,
        Status           NVARCHAR(20) NOT NULL CONSTRAINT DF_ContactMessages_Status DEFAULT (N'New'),
        CreatedAt        DATETIME2(0) NOT NULL CONSTRAINT DF_ContactMessages_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ResolvedAt       DATETIME2(0) NULL,
        CONSTRAINT PK_ContactMessages PRIMARY KEY (ContactMessageID),
        CONSTRAINT CK_ContactMessages_Status CHECK
        (
            Status IN (N'New', N'Read', N'Replied', N'Closed')
        )
    );
END
GO

/* ================================================================
   16. EDITABLE WEBSITE PAGES
   ================================================================ */
IF OBJECT_ID(N'dbo.WebsitePages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebsitePages
    (
        PageID        INT IDENTITY(1,1) NOT NULL,
        PageTitle     NVARCHAR(180) NOT NULL,
        PageSlug      NVARCHAR(180) NOT NULL,
        Content       NVARCHAR(MAX) NOT NULL,
        MetaTitle     NVARCHAR(180) NULL,
        MetaDescription NVARCHAR(300) NULL,
        IsPublished   BIT NOT NULL CONSTRAINT DF_WebsitePages_IsPublished DEFAULT (1),
        UpdatedBy     INT NULL,
        CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_WebsitePages_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     DATETIME2(0) NULL,
        CONSTRAINT PK_WebsitePages PRIMARY KEY (PageID),
        CONSTRAINT UQ_WebsitePages_PageSlug UNIQUE (PageSlug),
        CONSTRAINT FK_WebsitePages_Users FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(UserID)
    );
END
GO

/* ================================================================
   INDEXES
   ================================================================ */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_IsActive' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE INDEX IX_Users_IsActive ON dbo.Users(IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NGOs_IsActive' AND object_id = OBJECT_ID(N'dbo.NGOs'))
    CREATE INDEX IX_NGOs_IsActive ON dbo.NGOs(IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Causes_ActiveFeatured' AND object_id = OBJECT_ID(N'dbo.Causes'))
    CREATE INDEX IX_Causes_ActiveFeatured ON dbo.Causes(IsActive, IsFeatured, DisplayOrder);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Programmes_StatusStartDate' AND object_id = OBJECT_ID(N'dbo.Programmes'))
    CREATE INDEX IX_Programmes_StatusStartDate ON dbo.Programmes(Status, StartDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Donations_UserDate' AND object_id = OBJECT_ID(N'dbo.Donations'))
    CREATE INDEX IX_Donations_UserDate ON dbo.Donations(UserID, DonationDate DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Donations_Status' AND object_id = OBJECT_ID(N'dbo.Donations'))
    CREATE INDEX IX_Donations_Status ON dbo.Donations(DonationStatus);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Queries_UserStatus' AND object_id = OBJECT_ID(N'dbo.Queries'))
    CREATE INDEX IX_Queries_UserStatus ON dbo.Queries(UserID, Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContactMessages_Status' AND object_id = OBJECT_ID(N'dbo.ContactMessages'))
    CREATE INDEX IX_ContactMessages_Status ON dbo.ContactMessages(Status, CreatedAt DESC);
GO

PRINT 'GiveAID schema verification completed successfully.';
GO