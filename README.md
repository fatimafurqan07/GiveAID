db:


USE GiveAID;
GO

/* =========================================================
   WARNING
   =========================================================
   This script DROPS the existing GiveAID tables and recreates
   them.

   ONLY RUN THIS if you have no important data yet.
   ========================================================= */


/* =========================================================
   0. DROP EXISTING TABLES
   ========================================================= */

IF OBJECT_ID('dbo.QueryReplies', 'U') IS NOT NULL
    DROP TABLE dbo.QueryReplies;

IF OBJECT_ID('dbo.Queries', 'U') IS NOT NULL
    DROP TABLE dbo.Queries;

IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL
    DROP TABLE dbo.Payments;

IF OBJECT_ID('dbo.Donations', 'U') IS NOT NULL
    DROP TABLE dbo.Donations;

IF OBJECT_ID('dbo.Gallery', 'U') IS NOT NULL
    DROP TABLE dbo.Gallery;

IF OBJECT_ID('dbo.ProgramInterests', 'U') IS NOT NULL
    DROP TABLE dbo.ProgramInterests;

IF OBJECT_ID('dbo.Programs', 'U') IS NOT NULL
    DROP TABLE dbo.Programs;

IF OBJECT_ID('dbo.Causes', 'U') IS NOT NULL
    DROP TABLE dbo.Causes;

IF OBJECT_ID('dbo.NGOAccounts', 'U') IS NOT NULL
    DROP TABLE dbo.NGOAccounts;

IF OBJECT_ID('dbo.NGOApplications', 'U') IS NOT NULL
    DROP TABLE dbo.NGOApplications;

IF OBJECT_ID('dbo.NGOs', 'U') IS NOT NULL
    DROP TABLE dbo.NGOs;

IF OBJECT_ID('dbo.WebsitePages', 'U') IS NOT NULL
    DROP TABLE dbo.WebsitePages;

IF OBJECT_ID('dbo.Partners', 'U') IS NOT NULL
    DROP TABLE dbo.Partners;

IF OBJECT_ID('dbo.UserRoles', 'U') IS NOT NULL
    DROP TABLE dbo.UserRoles;

IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL
    DROP TABLE dbo.Roles;

IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
    DROP TABLE dbo.Users;
GO


/* =========================================================
   1. ROLES
   ========================================================= */

CREATE TABLE Roles
(
    RoleID INT IDENTITY(1,1) NOT NULL,

    RoleName NVARCHAR(50) NOT NULL,

    CONSTRAINT PK_Roles
        PRIMARY KEY (RoleID),

    CONSTRAINT UQ_Roles_RoleName
        UNIQUE (RoleName)
);
GO


/* =========================================================
   2. USERS
   ========================================================= */

CREATE TABLE Users
(
    UserID INT IDENTITY(1,1) NOT NULL,

    FullName NVARCHAR(150) NOT NULL,

    Email NVARCHAR(150) NOT NULL,

    PasswordHash NVARCHAR(255) NOT NULL,

    Phone NVARCHAR(30) NULL,

    Address NVARCHAR(500) NULL,

    City NVARCHAR(100) NULL,

    ProfileImageURL NVARCHAR(500) NULL,

    IsActive BIT NOT NULL
        CONSTRAINT DF_Users_IsActive DEFAULT 1,

    IsBanned BIT NOT NULL
        CONSTRAINT DF_Users_IsBanned DEFAULT 0,

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE(),

    LastLoginAt DATETIME2 NULL,

    CONSTRAINT PK_Users
        PRIMARY KEY (UserID),

    CONSTRAINT UQ_Users_Email
        UNIQUE (Email)
);
GO


/* =========================================================
   3. USER ROLES
   ========================================================= */

CREATE TABLE UserRoles
(
    UserID INT NOT NULL,

    RoleID INT NOT NULL,

    AssignedAt DATETIME2 NOT NULL
        CONSTRAINT DF_UserRoles_AssignedAt DEFAULT GETDATE(),

    CONSTRAINT PK_UserRoles
        PRIMARY KEY (UserID, RoleID),

    CONSTRAINT FK_UserRoles_Users
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT FK_UserRoles_Roles
        FOREIGN KEY (RoleID)
        REFERENCES Roles(RoleID)
);
GO


/* =========================================================
   4. NGOS
   ========================================================= */

CREATE TABLE NGOs
(
    NGOID INT IDENTITY(1,1) NOT NULL,

    NGOName NVARCHAR(150) NOT NULL,

    Description NVARCHAR(2000) NULL,

    Address NVARCHAR(500) NULL,

    City NVARCHAR(100) NULL,

    Phone NVARCHAR(30) NULL,

    Email NVARCHAR(150) NULL,

    LogoURL NVARCHAR(500) NULL,

    WebsiteURL NVARCHAR(500) NULL,

    Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_NGOs_Status DEFAULT 'Pending',

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_NGOs_CreatedAt DEFAULT GETDATE(),

    UpdatedAt DATETIME2 NULL,

    CONSTRAINT PK_NGOs
        PRIMARY KEY (NGOID),

    CONSTRAINT UQ_NGOs_Name
        UNIQUE (NGOName),

    CONSTRAINT CK_NGOs_Status
        CHECK
        (
            Status IN
            (
                'Pending',
                'Active',
                'Inactive',
                'Suspended',
                'Banned'
            )
        )
);
GO


/* =========================================================
   5. NGO APPLICATIONS
   ========================================================= */

CREATE TABLE NGOApplications
(
    ApplicationID INT IDENTITY(1,1) NOT NULL,

    ApplicantUserID INT NOT NULL,

    NGOName NVARCHAR(150) NOT NULL,

    Description NVARCHAR(2000) NULL,

    Address NVARCHAR(500) NULL,

    City NVARCHAR(100) NULL,

    Phone NVARCHAR(30) NULL,

    Email NVARCHAR(150) NULL,

    ApplicationStatus NVARCHAR(20) NOT NULL
        CONSTRAINT DF_NGOApplications_Status DEFAULT 'Pending',

    SubmittedAt DATETIME2 NOT NULL
        CONSTRAINT DF_NGOApplications_SubmittedAt DEFAULT GETDATE(),

    ReviewedAt DATETIME2 NULL,

    ReviewedBy INT NULL,

    AdminRemarks NVARCHAR(1000) NULL,

    CONSTRAINT PK_NGOApplications
        PRIMARY KEY (ApplicationID),

    CONSTRAINT FK_NGOApplications_Applicant
        FOREIGN KEY (ApplicantUserID)
        REFERENCES Users(UserID),

    CONSTRAINT FK_NGOApplications_Reviewer
        FOREIGN KEY (ReviewedBy)
        REFERENCES Users(UserID),

    CONSTRAINT CK_NGOApplications_Status
        CHECK
        (
            ApplicationStatus IN
            (
                'Pending',
                'Approved',
                'Rejected'
            )
        )
);
GO


/* =========================================================
   6. NGO ACCOUNTS
   ========================================================= */

CREATE TABLE NGOAccounts
(
    NGOAccountID INT IDENTITY(1,1) NOT NULL,

    NGOID INT NOT NULL,

    UserID INT NOT NULL,

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_NGOAccounts_CreatedAt DEFAULT GETDATE(),

    CONSTRAINT PK_NGOAccounts
        PRIMARY KEY (NGOAccountID),

    CONSTRAINT FK_NGOAccounts_NGO
        FOREIGN KEY (NGOID)
        REFERENCES NGOs(NGOID),

    CONSTRAINT FK_NGOAccounts_User
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT UQ_NGOAccounts_NGO
        UNIQUE (NGOID),

    CONSTRAINT UQ_NGOAccounts_User
        UNIQUE (UserID)
);
GO


/* =========================================================
   7. CAUSES
   ========================================================= */

CREATE TABLE Causes
(
    CauseID INT IDENTITY(1,1) NOT NULL,

    CauseName NVARCHAR(100) NOT NULL,

    Description NVARCHAR(1000) NULL,

    ImageURL NVARCHAR(500) NULL,

    IsActive BIT NOT NULL
        CONSTRAINT DF_Causes_IsActive DEFAULT 1,

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Causes_CreatedAt DEFAULT GETDATE(),

    CONSTRAINT PK_Causes
        PRIMARY KEY (CauseID),

    CONSTRAINT UQ_Causes_CauseName
        UNIQUE (CauseName)
);
GO


/* =========================================================
   8. PROGRAMS
   ========================================================= */

CREATE TABLE Programs
(
    ProgramID INT IDENTITY(1,1) NOT NULL,

    NGOID INT NOT NULL,

    CauseID INT NOT NULL,

    ProgramName NVARCHAR(150) NOT NULL,

    Description NVARCHAR(2000) NULL,

    Location NVARCHAR(500) NULL,

    StartDate DATE NOT NULL,

    EndDate DATE NULL,

    TargetAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Programs_TargetAmount DEFAULT 0,

    CurrentAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Programs_CurrentAmount DEFAULT 0,

    ImageURL NVARCHAR(500) NULL,

    Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Programs_Status DEFAULT 'Upcoming',

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Programs_CreatedAt DEFAULT GETDATE(),

    UpdatedAt DATETIME2 NULL,

    CONSTRAINT PK_Programs
        PRIMARY KEY (ProgramID),

    CONSTRAINT FK_Programs_NGO
        FOREIGN KEY (NGOID)
        REFERENCES NGOs(NGOID),

    CONSTRAINT FK_Programs_Cause
        FOREIGN KEY (CauseID)
        REFERENCES Causes(CauseID),

    CONSTRAINT CK_Programs_Status
        CHECK
        (
            Status IN
            (
                'Upcoming',
                'Active',
                'Completed',
                'Cancelled'
            )
        ),

    CONSTRAINT CK_Programs_TargetAmount
        CHECK (TargetAmount >= 0),

    CONSTRAINT CK_Programs_CurrentAmount
        CHECK (CurrentAmount >= 0),

    CONSTRAINT CK_Programs_Dates
        CHECK
        (
            EndDate IS NULL
            OR EndDate >= StartDate
        ),

    /*
       These unique constraints allow us to create
       composite foreign keys later.
    */

    CONSTRAINT UQ_Programs_Program_NGO
        UNIQUE (ProgramID, NGOID),

    CONSTRAINT UQ_Programs_Program_NGO_Cause
        UNIQUE (ProgramID, NGOID, CauseID)
);
GO


/* =========================================================
   9. PROGRAM INTERESTS
   ========================================================= */

CREATE TABLE ProgramInterests
(
    InterestID INT IDENTITY(1,1) NOT NULL,

    UserID INT NOT NULL,

    ProgramID INT NOT NULL,

    InterestDate DATETIME2 NOT NULL
        CONSTRAINT DF_ProgramInterests_Date DEFAULT GETDATE(),

    Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_ProgramInterests_Status DEFAULT 'Interested',

    CONSTRAINT PK_ProgramInterests
        PRIMARY KEY (InterestID),

    CONSTRAINT FK_ProgramInterests_User
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT FK_ProgramInterests_Program
        FOREIGN KEY (ProgramID)
        REFERENCES Programs(ProgramID),

    CONSTRAINT UQ_ProgramInterests_UserProgram
        UNIQUE (UserID, ProgramID),

    CONSTRAINT CK_ProgramInterests_Status
        CHECK
        (
            Status IN
            (
                'Interested',
                'Cancelled'
            )
        )
);
GO


/* =========================================================
   10. DONATIONS
   ========================================================= */

CREATE TABLE Donations
(
    DonationID INT IDENTITY(1,1) NOT NULL,

    UserID INT NOT NULL,

    NGOID INT NOT NULL,

    CauseID INT NOT NULL,

    ProgramID INT NULL,

    Amount DECIMAL(18,2) NOT NULL,

    AdminApprovalStatus NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Donations_AdminApproval
        DEFAULT 'Pending',

    NGOApprovalStatus NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Donations_NGOApproval
        DEFAULT 'Pending',

    DonationStatus NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Donations_Status
        DEFAULT 'Pending',

    DonationDate DATETIME2 NOT NULL
        CONSTRAINT DF_Donations_Date DEFAULT GETDATE(),

    AdminReviewedAt DATETIME2 NULL,

    NGOReviewedAt DATETIME2 NULL,

    AdminRemarks NVARCHAR(1000) NULL,

    NGORemarks NVARCHAR(1000) NULL,

    CONSTRAINT PK_Donations
        PRIMARY KEY (DonationID),

    CONSTRAINT FK_Donations_User
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT FK_Donations_NGO
        FOREIGN KEY (NGOID)
        REFERENCES NGOs(NGOID),

    CONSTRAINT FK_Donations_Cause
        FOREIGN KEY (CauseID)
        REFERENCES Causes(CauseID),

    /*
       Program is optional.
       If a program IS selected, NGO and Cause must
       match that program.
    */

    CONSTRAINT FK_Donations_Program_NGO_Cause
        FOREIGN KEY (ProgramID, NGOID, CauseID)
        REFERENCES Programs
        (
            ProgramID,
            NGOID,
            CauseID
        ),

    CONSTRAINT CK_Donations_Amount
        CHECK (Amount > 0),

    CONSTRAINT CK_Donations_AdminApproval
        CHECK
        (
            AdminApprovalStatus IN
            (
                'Pending',
                'Approved',
                'Rejected'
            )
        ),

    CONSTRAINT CK_Donations_NGOApproval
        CHECK
        (
            NGOApprovalStatus IN
            (
                'Pending',
                'Approved',
                'Rejected'
            )
        ),

    CONSTRAINT CK_Donations_Status
        CHECK
        (
            DonationStatus IN
            (
                'Pending',
                'Approved',
                'Rejected',
                'Completed',
                'Cancelled'
            )
        )
);
GO


/* =========================================================
   11. PAYMENTS
   ========================================================= */

CREATE TABLE Payments
(
    PaymentID INT IDENTITY(1,1) NOT NULL,

    DonationID INT NOT NULL,

    PaymentReference NVARCHAR(100) NOT NULL,

    CardType NVARCHAR(20) NULL,

    CardLastFour CHAR(4) NULL,

    Amount DECIMAL(18,2) NOT NULL,

    PaymentStatus NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Payments_Status DEFAULT 'Pending',

    PaymentDate DATETIME2 NOT NULL
        CONSTRAINT DF_Payments_Date DEFAULT GETDATE(),

    FailureReason NVARCHAR(500) NULL,

    CONSTRAINT PK_Payments
        PRIMARY KEY (PaymentID),

    CONSTRAINT FK_Payments_Donation
        FOREIGN KEY (DonationID)
        REFERENCES Donations(DonationID),

    CONSTRAINT UQ_Payments_Reference
        UNIQUE (PaymentReference),

    CONSTRAINT CK_Payments_Amount
        CHECK (Amount > 0),

    CONSTRAINT CK_Payments_CardType
        CHECK
        (
            CardType IS NULL
            OR CardType IN
            (
                'Visa',
                'Mastercard',
                'Other'
            )
        ),

    CONSTRAINT CK_Payments_CardLastFour
        CHECK
        (
            CardLastFour IS NULL
            OR CardLastFour NOT LIKE '%[^0-9]%'
        ),

    CONSTRAINT CK_Payments_Status
        CHECK
        (
            PaymentStatus IN
            (
                'Pending',
                'Successful',
                'Failed',
                'Refunded'
            )
        )
);
GO


/* =========================================================
   12. QUERIES
   ========================================================= */

CREATE TABLE Queries
(
    QueryID INT IDENTITY(1,1) NOT NULL,

    UserID INT NOT NULL,

    Subject NVARCHAR(200) NOT NULL,

    Message NVARCHAR(2000) NOT NULL,

    Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Queries_Status DEFAULT 'Open',

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Queries_CreatedAt DEFAULT GETDATE(),

    UpdatedAt DATETIME2 NULL,

    CONSTRAINT PK_Queries
        PRIMARY KEY (QueryID),

    CONSTRAINT FK_Queries_User
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT CK_Queries_Status
        CHECK
        (
            Status IN
            (
                'Open',
                'In Progress',
                'Resolved',
                'Closed'
            )
        )
);
GO


/* =========================================================
   13. QUERY REPLIES
   ========================================================= */

CREATE TABLE QueryReplies
(
    ReplyID INT IDENTITY(1,1) NOT NULL,

    QueryID INT NOT NULL,

    RepliedBy INT NOT NULL,

    ReplyMessage NVARCHAR(2000) NOT NULL,

    RepliedAt DATETIME2 NOT NULL
        CONSTRAINT DF_QueryReplies_Date DEFAULT GETDATE(),

    CONSTRAINT PK_QueryReplies
        PRIMARY KEY (ReplyID),

    CONSTRAINT FK_QueryReplies_Query
        FOREIGN KEY (QueryID)
        REFERENCES Queries(QueryID),

    CONSTRAINT FK_QueryReplies_User
        FOREIGN KEY (RepliedBy)
        REFERENCES Users(UserID)
);
GO


/* =========================================================
   14. PARTNERS
   ========================================================= */

CREATE TABLE Partners
(
    PartnerID INT IDENTITY(1,1) NOT NULL,

    PartnerName NVARCHAR(150) NOT NULL,

    Description NVARCHAR(1000) NULL,

    LogoURL NVARCHAR(500) NULL,

    WebsiteURL NVARCHAR(500) NULL,

    IsActive BIT NOT NULL
        CONSTRAINT DF_Partners_IsActive DEFAULT 1,

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Partners_CreatedAt DEFAULT GETDATE(),

    UpdatedAt DATETIME2 NULL,

    CONSTRAINT PK_Partners
        PRIMARY KEY (PartnerID),

    CONSTRAINT UQ_Partners_Name
        UNIQUE (PartnerName)
);
GO


/* =========================================================
   15. GALLERY
   ========================================================= */

CREATE TABLE Gallery
(
    GalleryID INT IDENTITY(1,1) NOT NULL,

    NGOID INT NOT NULL,

    ProgramID INT NULL,

    ImageURL NVARCHAR(500) NOT NULL,

    Caption NVARCHAR(500) NULL,

    UploadedBy INT NOT NULL,

    UploadedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Gallery_UploadedAt DEFAULT GETDATE(),

    IsActive BIT NOT NULL
        CONSTRAINT DF_Gallery_IsActive DEFAULT 1,

    CONSTRAINT PK_Gallery
        PRIMARY KEY (GalleryID),

    CONSTRAINT FK_Gallery_NGO
        FOREIGN KEY (NGOID)
        REFERENCES NGOs(NGOID),

    /*
       If ProgramID is provided, the program must belong
       to the selected NGO.
    */

    CONSTRAINT FK_Gallery_Program_NGO
        FOREIGN KEY (ProgramID, NGOID)
        REFERENCES Programs
        (
            ProgramID,
            NGOID
        ),

    CONSTRAINT FK_Gallery_User
        FOREIGN KEY (UploadedBy)
        REFERENCES Users(UserID)
);
GO


/* =========================================================
   16. WEBSITE PAGES
   ========================================================= */

CREATE TABLE WebsitePages
(
    PageID INT IDENTITY(1,1) NOT NULL,

    PageTitle NVARCHAR(150) NOT NULL,

    PageSlug NVARCHAR(150) NOT NULL,

    Content NVARCHAR(MAX) NOT NULL,

    LastUpdated DATETIME2 NOT NULL
        CONSTRAINT DF_WebsitePages_LastUpdated DEFAULT GETDATE(),

    UpdatedBy INT NULL,

    IsPublished BIT NOT NULL
        CONSTRAINT DF_WebsitePages_IsPublished DEFAULT 1,

    CONSTRAINT PK_WebsitePages
        PRIMARY KEY (PageID),

    CONSTRAINT UQ_WebsitePages_PageSlug
        UNIQUE (PageSlug),

    CONSTRAINT FK_WebsitePages_User
        FOREIGN KEY (UpdatedBy)
        REFERENCES Users(UserID)
);
GO


/* =========================================================
   17. INDEXES
   ========================================================= */

/* NGO searches */
CREATE INDEX IX_NGOs_Status
ON NGOs(Status);
GO

/* NGO applications */
CREATE INDEX IX_NGOApplications_Status
ON NGOApplications(ApplicationStatus);
GO

CREATE INDEX IX_NGOApplications_Applicant
ON NGOApplications(ApplicantUserID);
GO

/* Programs */
CREATE INDEX IX_Programs_NGO
ON Programs(NGOID);
GO

CREATE INDEX IX_Programs_Cause
ON Programs(CauseID);
GO

CREATE INDEX IX_Programs_Status
ON Programs(Status);
GO

CREATE INDEX IX_Programs_StartDate
ON Programs(StartDate);
GO

/* Program interests */
CREATE INDEX IX_ProgramInterests_Program
ON ProgramInterests(ProgramID);
GO

/* Donations */
CREATE INDEX IX_Donations_User
ON Donations(UserID);
GO

CREATE INDEX IX_Donations_NGO
ON Donations(NGOID);
GO

CREATE INDEX IX_Donations_Cause
ON Donations(CauseID);
GO

CREATE INDEX IX_Donations_Program
ON Donations(ProgramID);
GO

CREATE INDEX IX_Donations_Status
ON Donations(DonationStatus);
GO

CREATE INDEX IX_Donations_Date
ON Donations(DonationDate);
GO

/* Payments */
CREATE INDEX IX_Payments_Donation
ON Payments(DonationID);
GO

CREATE INDEX IX_Payments_Status
ON Payments(PaymentStatus);
GO

/* Queries */
CREATE INDEX IX_Queries_User
ON Queries(UserID);
GO

CREATE INDEX IX_Queries_Status
ON Queries(Status);
GO

/* Query replies */
CREATE INDEX IX_QueryReplies_Query
ON QueryReplies(QueryID);
GO

/* Gallery */
CREATE INDEX IX_Gallery_NGO
ON Gallery(NGOID);
GO

CREATE INDEX IX_Gallery_Program
ON Gallery(ProgramID);
GO

/* Website pages */
CREATE INDEX IX_WebsitePages_Published
ON WebsitePages(IsPublished);
GO


USE GiveAID;
GO

/* =========================================================
   17. EVENTS
   ========================================================= */

CREATE TABLE Events
(
    EventID INT IDENTITY(1,1) NOT NULL,

    NGOID INT NOT NULL,

    ProgramID INT NULL,

    EventName NVARCHAR(150) NOT NULL,

    Description NVARCHAR(2000) NULL,

    Location NVARCHAR(500) NULL,

    EventDate DATE NOT NULL,

    StartTime TIME NULL,

    EndTime TIME NULL,

    ImageURL NVARCHAR(500) NULL,

    Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Events_Status DEFAULT 'Upcoming',

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Events_CreatedAt DEFAULT GETDATE(),

    CONSTRAINT PK_Events
        PRIMARY KEY (EventID),

    CONSTRAINT FK_Events_NGO
        FOREIGN KEY (NGOID)
        REFERENCES NGOs(NGOID),

    CONSTRAINT FK_Events_Program_NGO
        FOREIGN KEY (ProgramID, NGOID)
        REFERENCES Programs(ProgramID, NGOID),

    CONSTRAINT CK_Events_Status
        CHECK
        (
            Status IN
            (
                'Upcoming',
                'Ongoing',
                'Completed',
                'Cancelled'
            )
        ),

    CONSTRAINT CK_Events_Time
        CHECK
        (
            EndTime IS NULL
            OR StartTime IS NULL
            OR EndTime > StartTime
        )
);
GO


/* =========================================================
   18. INVITATIONS
   ========================================================= */

CREATE TABLE Invitations
(
    InvitationID INT IDENTITY(1,1) NOT NULL,

    UserID INT NOT NULL,

    ProgramID INT NULL,

    RecipientEmail NVARCHAR(150) NOT NULL,

    InvitationMessage NVARCHAR(1000) NULL,

    SentAt DATETIME2 NOT NULL
        CONSTRAINT DF_Invitations_SentAt DEFAULT GETDATE(),

    Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Invitations_Status DEFAULT 'Sent',

    CONSTRAINT PK_Invitations
        PRIMARY KEY (InvitationID),

    CONSTRAINT FK_Invitations_User
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT FK_Invitations_Program
        FOREIGN KEY (ProgramID)
        REFERENCES Programs(ProgramID),

    CONSTRAINT CK_Invitations_Status
        CHECK
        (
            Status IN
            (
                'Sent',
                'Delivered',
                'Failed'
            )
        )
);
GO


/* =========================================================
   19. NOTIFICATIONS
   ========================================================= */

CREATE TABLE Notifications
(
    NotificationID INT IDENTITY(1,1) NOT NULL,

    UserID INT NOT NULL,

    Title NVARCHAR(200) NOT NULL,

    Message NVARCHAR(1000) NOT NULL,

    NotificationType NVARCHAR(50) NOT NULL,

    IsRead BIT NOT NULL
        CONSTRAINT DF_Notifications_IsRead DEFAULT 0,

    CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Notifications_CreatedAt DEFAULT GETDATE(),

    ReadAt DATETIME2 NULL,

    CONSTRAINT PK_Notifications
        PRIMARY KEY (NotificationID),

    CONSTRAINT FK_Notifications_User
        FOREIGN KEY (UserID)
        REFERENCES Users(UserID),

    CONSTRAINT CK_Notifications_Type
        CHECK
        (
            NotificationType IN
            (
                'Donation',
                'Payment',
                'Program',
                'Event',
                'Query',
                'Account',
                'System'
            )
        )
);
GO


/* =========================================================
   INDEXES
   ========================================================= */

CREATE INDEX IX_Events_NGO
ON Events(NGOID);
GO

CREATE INDEX IX_Events_Program
ON Events(ProgramID);
GO

CREATE INDEX IX_Events_Date
ON Events(EventDate);
GO

CREATE INDEX IX_Events_Status
ON Events(Status);
GO

CREATE INDEX IX_Invitations_User
ON Invitations(UserID);
GO

CREATE INDEX IX_Invitations_Program
ON Invitations(ProgramID);
GO

CREATE INDEX IX_Invitations_RecipientEmail
ON Invitations(RecipientEmail);
GO

CREATE INDEX IX_Notifications_User
ON Notifications(UserID);
GO

CREATE INDEX IX_Notifications_Unread
ON Notifications(UserID, IsRead);
GO
