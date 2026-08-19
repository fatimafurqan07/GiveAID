/*
    GiveAID Database
    Step 03: Insert essential and professional demonstration data.

    The script is idempotent: it can be executed again without creating
    duplicate roles, accounts, NGOs, causes, programmes, partners or pages.

    Demonstration administrator:
      Email:    admin@giveaid.local
      Password: Admin@GiveAID2026!

    Change the demonstration password before a real deployment.
*/

USE [GiveAID];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ============================================================
       1. SYSTEM ROLES
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = N'Admin')
    BEGIN
        INSERT INTO dbo.Roles (RoleName, Description)
        VALUES (N'Admin', N'Manages users, content, NGOs, programmes, donations and support queries.');
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = N'User')
    BEGIN
        INSERT INTO dbo.Roles (RoleName, Description)
        VALUES (N'User', N'Registered donor, supporter or programme participant.');
    END;

    /* ============================================================
       2. DEFAULT ADMINISTRATOR
       Password is stored as a salted PBKDF2 hash, never as plain text.
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'admin@giveaid.local')
    BEGIN
        INSERT INTO dbo.Users
        (
            FullName,
            Email,
            PasswordHash,
            Phone,
            City,
            Country,
            IsActive
        )
        VALUES
        (
            N'GiveAID Administrator',
            N'admin@giveaid.local',
            N'NAfLSmBnUcRt2ZvHZhpsCQ==:uAMT4AwKFBuUKweSsCCNuRrG6bEtnbqbdfmZteb4H7o=',
            N'+92 300 0000000',
            N'Karachi',
            N'Pakistan',
            1
        );
    END;

    DECLARE @AdminUserID INT =
    (
        SELECT UserID FROM dbo.Users WHERE Email = N'admin@giveaid.local'
    );

    DECLARE @AdminRoleID INT =
    (
        SELECT RoleID FROM dbo.Roles WHERE RoleName = N'Admin'
    );

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserRoles
        WHERE UserID = @AdminUserID AND RoleID = @AdminRoleID
    )
    BEGIN
        INSERT INTO dbo.UserRoles (UserID, RoleID)
        VALUES (@AdminUserID, @AdminRoleID);
    END;

    /* ============================================================
       3. ASSOCIATED NGOs
       Fictional demonstration organisations for academic use.
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.NGOs WHERE NGOName = N'HopeBridge Foundation')
    BEGIN
        INSERT INTO dbo.NGOs
        (
            NGOName, RegistrationNumber, Category, Description,
            Email, Phone, Address, City, Country, WebsiteURL,
            ContactPerson, IsActive
        )
        VALUES
        (
            N'HopeBridge Foundation', N'DEMO-NGO-001', N'Community Welfare',
            N'A demonstration organisation supporting families through food security, shelter and community resilience programmes.',
            N'contact@hopebridge.example', N'+92 300 1111111',
            N'Community Centre Road', N'Karachi', N'Pakistan',
            N'https://example.org/hopebridge', N'Ayesha Rahman', 1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.NGOs WHERE NGOName = N'Bright Futures Initiative')
    BEGIN
        INSERT INTO dbo.NGOs
        (
            NGOName, RegistrationNumber, Category, Description,
            Email, Phone, Address, City, Country, WebsiteURL,
            ContactPerson, IsActive
        )
        VALUES
        (
            N'Bright Futures Initiative', N'DEMO-NGO-002', N'Education',
            N'A demonstration organisation focused on learning resources, scholarships and digital skills for young people.',
            N'hello@brightfutures.example', N'+92 300 2222222',
            N'Education Avenue', N'Islamabad', N'Pakistan',
            N'https://example.org/brightfutures', N'Omar Siddiqui', 1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.NGOs WHERE NGOName = N'Community Health Alliance')
    BEGIN
        INSERT INTO dbo.NGOs
        (
            NGOName, RegistrationNumber, Category, Description,
            Email, Phone, Address, City, Country, WebsiteURL,
            ContactPerson, IsActive
        )
        VALUES
        (
            N'Community Health Alliance', N'DEMO-NGO-003', N'Healthcare',
            N'A demonstration organisation delivering preventive health awareness and community medical outreach.',
            N'care@communityhealth.example', N'+92 300 3333333',
            N'Health Services Lane', N'Lahore', N'Pakistan',
            N'https://example.org/communityhealth', N'Sara Khan', 1
        );
    END;

    /* ============================================================
       4. DONATION CAUSES
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Causes WHERE Slug = N'education')
    BEGIN
        INSERT INTO dbo.Causes
        (CauseName, Slug, ShortDescription, Description, IconName, IsFeatured, DisplayOrder)
        VALUES
        (N'Education', N'education', N'Help learners access books, technology and safe learning spaces.',
         N'Support education programmes that improve access to learning resources and skills development.',
         N'book-open', 1, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Causes WHERE Slug = N'healthcare')
    BEGIN
        INSERT INTO dbo.Causes
        (CauseName, Slug, ShortDescription, Description, IconName, IsFeatured, DisplayOrder)
        VALUES
        (N'Healthcare', N'healthcare', N'Support essential healthcare and preventive health initiatives.',
         N'Help community health programmes provide awareness, screenings and essential medical support.',
         N'heart-pulse', 1, 2);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Causes WHERE Slug = N'children')
    BEGIN
        INSERT INTO dbo.Causes
        (CauseName, Slug, ShortDescription, Description, IconName, IsFeatured, DisplayOrder)
        VALUES
        (N'Children', N'children', N'Create safer and healthier opportunities for children.',
         N'Support child welfare initiatives centred on care, nutrition, education and protection.',
         N'child', 1, 3);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Causes WHERE Slug = N'clean-water')
    BEGIN
        INSERT INTO dbo.Causes
        (CauseName, Slug, ShortDescription, Description, IconName, IsFeatured, DisplayOrder)
        VALUES
        (N'Clean Water', N'clean-water', N'Improve access to safe drinking water and sanitation.',
         N'Support practical water and sanitation projects in underserved communities.',
         N'droplets', 1, 4);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Causes WHERE Slug = N'women-empowerment')
    BEGIN
        INSERT INTO dbo.Causes
        (CauseName, Slug, ShortDescription, Description, IconName, IsFeatured, DisplayOrder)
        VALUES
        (N'Women Empowerment', N'women-empowerment', N'Advance skills, opportunity and economic participation for women.',
         N'Support training and livelihood initiatives designed with women and their communities.',
         N'users', 0, 5);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Causes WHERE Slug = N'elderly-care')
    BEGIN
        INSERT INTO dbo.Causes
        (CauseName, Slug, ShortDescription, Description, IconName, IsFeatured, DisplayOrder)
        VALUES
        (N'Elderly Care', N'elderly-care', N'Promote dignity, care and connection for older people.',
         N'Support community programmes addressing the wellbeing and inclusion of senior citizens.',
         N'hand-heart', 0, 6);
    END;

    /* ============================================================
       5. SAMPLE PROGRAMMES
       ============================================================ */
    DECLARE @EducationCauseID INT = (SELECT CauseID FROM dbo.Causes WHERE Slug = N'education');
    DECLARE @HealthcareCauseID INT = (SELECT CauseID FROM dbo.Causes WHERE Slug = N'healthcare');
    DECLARE @WaterCauseID INT = (SELECT CauseID FROM dbo.Causes WHERE Slug = N'clean-water');

    DECLARE @BrightFuturesNGOID INT = (SELECT NGOID FROM dbo.NGOs WHERE NGOName = N'Bright Futures Initiative');
    DECLARE @HealthAllianceNGOID INT = (SELECT NGOID FROM dbo.NGOs WHERE NGOName = N'Community Health Alliance');
    DECLARE @HopeBridgeNGOID INT = (SELECT NGOID FROM dbo.NGOs WHERE NGOName = N'HopeBridge Foundation');

    IF NOT EXISTS (SELECT 1 FROM dbo.Programmes WHERE Slug = N'digital-learning-lab')
    BEGIN
        INSERT INTO dbo.Programmes
        (
            NGOID, CauseID, ProgrammeName, Slug, ShortDescription,
            Description, Location, StartDate, EndDate, TargetAmount,
            Status, IsFeatured
        )
        VALUES
        (
            @BrightFuturesNGOID, @EducationCauseID,
            N'Digital Learning Lab', N'digital-learning-lab',
            N'A practical learning space offering supervised access to computers and digital skills.',
            N'This academic demonstration programme supports foundational digital literacy and guided learning activities.',
            N'Islamabad', CAST(DATEADD(DAY, 30, GETDATE()) AS DATE),
            CAST(DATEADD(DAY, 120, GETDATE()) AS DATE), 750000,
            N'Upcoming', 1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Programmes WHERE Slug = N'community-health-day')
    BEGIN
        INSERT INTO dbo.Programmes
        (
            NGOID, CauseID, ProgrammeName, Slug, ShortDescription,
            Description, Location, StartDate, EndDate, TargetAmount,
            Status, IsFeatured
        )
        VALUES
        (
            @HealthAllianceNGOID, @HealthcareCauseID,
            N'Community Health Day', N'community-health-day',
            N'Preventive health awareness and basic screening activities for local families.',
            N'This academic demonstration programme illustrates community health outreach and volunteer participation.',
            N'Lahore', CAST(DATEADD(DAY, 45, GETDATE()) AS DATE),
            CAST(DATEADD(DAY, 45, GETDATE()) AS DATE), 500000,
            N'Upcoming', 1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Programmes WHERE Slug = N'safe-water-community-project')
    BEGIN
        INSERT INTO dbo.Programmes
        (
            NGOID, CauseID, ProgrammeName, Slug, ShortDescription,
            Description, Location, StartDate, EndDate, TargetAmount,
            Status, IsFeatured
        )
        VALUES
        (
            @HopeBridgeNGOID, @WaterCauseID,
            N'Safe Water Community Project', N'safe-water-community-project',
            N'A community-led project promoting safer drinking-water access and hygiene awareness.',
            N'This academic demonstration programme represents a transparent, measurable community water initiative.',
            N'Karachi', CAST(DATEADD(DAY, 15, GETDATE()) AS DATE),
            CAST(DATEADD(DAY, 180, GETDATE()) AS DATE), 900000,
            N'Upcoming', 1
        );
    END;

    /* ============================================================
       6. DEMONSTRATION PARTNERS
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Partners WHERE PartnerName = N'Community Impact Network')
    BEGIN
        INSERT INTO dbo.Partners (PartnerName, Description, WebsiteURL, DisplayOrder)
        VALUES
        (N'Community Impact Network', N'Fictional academic partner supporting community coordination.',
         N'https://example.org/community-impact', 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Partners WHERE PartnerName = N'Learning Access Alliance')
    BEGIN
        INSERT INTO dbo.Partners (PartnerName, Description, WebsiteURL, DisplayOrder)
        VALUES
        (N'Learning Access Alliance', N'Fictional academic partner supporting education initiatives.',
         N'https://example.org/learning-access', 2);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Partners WHERE PartnerName = N'Health Outreach Collective')
    BEGIN
        INSERT INTO dbo.Partners (PartnerName, Description, WebsiteURL, DisplayOrder)
        VALUES
        (N'Health Outreach Collective', N'Fictional academic partner supporting community health outreach.',
         N'https://example.org/health-outreach', 3);
    END;

    /* ============================================================
       7. EDITABLE WEBSITE CONTENT
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.WebsitePages WHERE PageSlug = N'about-us')
    BEGIN
        INSERT INTO dbo.WebsitePages
        (PageTitle, PageSlug, Content, MetaTitle, MetaDescription, UpdatedBy)
        VALUES
        (N'About GiveAID', N'about-us',
         N'GiveAID is an academic demonstration platform designed to connect supporters with transparent community programmes.',
         N'About GiveAID', N'Learn about the purpose and values of the GiveAID demonstration platform.', @AdminUserID);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.WebsitePages WHERE PageSlug = N'our-mission')
    BEGIN
        INSERT INTO dbo.WebsitePages
        (PageTitle, PageSlug, Content, MetaTitle, MetaDescription, UpdatedBy)
        VALUES
        (N'Our Mission', N'our-mission',
         N'Our mission is to make community support easier to understand through clear causes, responsible data handling and accessible programme information.',
         N'Our Mission - GiveAID', N'Read the mission of the GiveAID demonstration platform.', @AdminUserID);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.WebsitePages WHERE PageSlug = N'help-centre')
    BEGIN
        INSERT INTO dbo.WebsitePages
        (PageTitle, PageSlug, Content, MetaTitle, MetaDescription, UpdatedBy)
        VALUES
        (N'Help Centre', N'help-centre',
         N'Registered users can submit questions from their dashboard. An administrator can review and reply to each query.',
         N'Help Centre - GiveAID', N'Find help with GiveAID accounts, donations and programmes.', @AdminUserID);
    END;

    COMMIT TRANSACTION;
    PRINT 'GiveAID seed data inserted or verified successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
