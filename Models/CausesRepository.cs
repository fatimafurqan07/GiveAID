using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class CausesRepository
    {
        private readonly string _connectionString;

        public CausesRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];

            _connectionString = setting != null &&
                                !string.IsNullOrWhiteSpace(setting.ConnectionString)
                ? setting.ConnectionString
                : @"Data Source=localhost\SQLEXPRESS;Initial Catalog=GiveAID;Integrated Security=True;TrustServerCertificate=True;";
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /* =========================================================
           PUBLIC CAUSE LISTING
           ========================================================= */

        public CauseListViewModel GetCausesList(string search = null, string category = null)
        {
            var model = new CauseListViewModel
            {
                SearchQuery = Clean(search),
                SelectedCategory = Clean(category)
            };

            using (var connection = GetConnection())
            {
                connection.Open();

                const string categoriesSql = @"
SELECT CauseName
FROM dbo.Causes
WHERE IsActive = 1
ORDER BY DisplayOrder, CauseName;";

                using (var command = new SqlCommand(categoriesSql, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.AvailableCategories.Add(reader.GetString(0));
                    }
                }

                var sql = @"
SELECT
    c.CauseID,
    c.CauseName,
    c.Slug,
    c.ShortDescription,
    c.Description,
    c.ImageURL,
    c.IconName,
    c.IsFeatured,
    c.IsActive,
    c.DisplayOrder,
    (
        SELECT COUNT(DISTINCT p.NGOID)
        FROM dbo.Programmes p
        INNER JOIN dbo.NGOs n ON n.NGOID = p.NGOID
        WHERE p.CauseID = c.CauseID
          AND n.IsActive = 1
          AND p.Status <> N'Cancelled'
    ) AS ActiveNGOsCount,
    (
        SELECT COUNT(1)
        FROM dbo.Programmes p
        WHERE p.CauseID = c.CauseID
          AND p.Status IN (N'Active', N'Upcoming')
    ) AS ActiveProgramsCount,
    (
        SELECT ISNULL(SUM(d.Amount), 0)
        FROM dbo.Donations d
        WHERE d.CauseID = c.CauseID
          AND d.DonationStatus = N'Completed'
    ) AS TotalRaised,
    (
        SELECT ISNULL(SUM(p.TargetAmount), 0)
        FROM dbo.Programmes p
        WHERE p.CauseID = c.CauseID
          AND p.Status <> N'Cancelled'
    ) AS TargetGoal
FROM dbo.Causes c
WHERE c.IsActive = 1";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    sql += @"
  AND
  (
      c.CauseName LIKE @Search OR
      c.ShortDescription LIKE @Search OR
      c.Description LIKE @Search
  )";

                    parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 250)
                    {
                        Value = "%" + model.SearchQuery + "%"
                    });
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedCategory) &&
                    !model.SelectedCategory.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " AND c.CauseName = @Category";

                    parameters.Add(new SqlParameter("@Category", SqlDbType.NVarChar, 120)
                    {
                        Value = model.SelectedCategory
                    });
                }

                sql += " ORDER BY c.DisplayOrder, c.CauseName;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddRange(parameters.ToArray());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Causes.Add(new CauseListItemViewModel
                            {
                                CauseID = reader.GetInt32(0),
                                CauseName = reader.GetString(1),
                                Slug = reader.GetString(2),
                                ShortDescription = Text(reader, 3),
                                Description = Text(reader, 4),
                                ImageURL = Text(reader, 5),
                                Icon = string.IsNullOrWhiteSpace(Text(reader, 6))
                                    ? CauseKey(reader.GetString(1))
                                    : Text(reader, 6),
                                IsFeatured = reader.GetBoolean(7),
                                IsActive = reader.GetBoolean(8),
                                DisplayOrder = reader.GetInt32(9),
                                ActiveNGOsCount = reader.GetInt32(10),
                                ActiveProgramsCount = reader.GetInt32(11),
                                TotalRaised = Convert.ToDecimal(reader.GetValue(12)),
                                TargetGoal = Convert.ToDecimal(reader.GetValue(13))
                            });
                        }
                    }
                }

                model.TotalNGOsCount = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.NGOs WHERE IsActive = 1;");

                model.TotalProgramsCount = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes WHERE Status = N'Active';");

                model.TotalFundsRaised = ScalarDecimal(
                    connection,
                    "SELECT ISNULL(SUM(Amount), 0) FROM dbo.Donations WHERE DonationStatus = N'Completed';");
            }

            return model;
        }

        /* =========================================================
           PUBLIC CAUSE DETAILS
           ========================================================= */

        public CauseDetailViewModel GetCauseById(int causeId)
        {
            using (var connection = GetConnection())
            {
                connection.Open();

                CauseDetailViewModel cause = null;

                const string sql = @"
SELECT
    CauseID,
    CauseName,
    Slug,
    ShortDescription,
    Description,
    ImageURL,
    IconName,
    IsFeatured,
    IsActive,
    CreatedAt
FROM dbo.Causes
WHERE CauseID = @CauseID
  AND IsActive = 1;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@CauseID", SqlDbType.Int).Value = causeId;

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cause = new CauseDetailViewModel
                            {
                                CauseID = reader.GetInt32(0),
                                CauseName = reader.GetString(1),
                                Slug = reader.GetString(2),
                                ShortDescription = Text(reader, 3),
                                Description = Text(reader, 4),
                                ImageURL = Text(reader, 5),
                                Icon = string.IsNullOrWhiteSpace(Text(reader, 6))
                                    ? CauseKey(reader.GetString(1))
                                    : Text(reader, 6),
                                IsFeatured = reader.GetBoolean(7),
                                IsActive = reader.GetBoolean(8),
                                CreatedAt = reader.GetDateTime(9)
                            };
                        }
                    }
                }

                if (cause == null)
                {
                    return null;
                }

                LoadNgos(connection, cause);
                LoadProgrammes(connection, cause);

                using (var command = new SqlCommand(@"
SELECT ISNULL(SUM(Amount), 0)
FROM dbo.Donations
WHERE CauseID = @CauseID
  AND DonationStatus = N'Completed';", connection))
                {
                    command.Parameters.Add("@CauseID", SqlDbType.Int).Value = causeId;
                    cause.TotalFundsRaised = Convert.ToDecimal(command.ExecuteScalar());
                }

                return cause;
            }
        }

        private static void LoadNgos(SqlConnection connection, CauseDetailViewModel cause)
        {
            const string sql = @"
SELECT
    n.NGOID,
    n.NGOName,
    n.Category,
    n.LogoURL,
    n.City,
    n.Description,
    COUNT(DISTINCT p.ProgrammeID) AS ProgramsCount,
    (
        SELECT ISNULL(SUM(d.Amount), 0)
        FROM dbo.Donations d
        WHERE d.NGOID = n.NGOID
          AND d.CauseID = @CauseID
          AND d.DonationStatus = N'Completed'
    ) AS TotalRaised
FROM dbo.NGOs n
INNER JOIN dbo.Programmes p ON p.NGOID = n.NGOID
WHERE p.CauseID = @CauseID
  AND n.IsActive = 1
  AND p.Status <> N'Cancelled'
GROUP BY
    n.NGOID,
    n.NGOName,
    n.Category,
    n.LogoURL,
    n.City,
    n.Description
ORDER BY n.NGOName;";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CauseID", SqlDbType.Int).Value = cause.CauseID;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cause.NGOs.Add(new CauseNgoItemViewModel
                        {
                            NGOID = reader.GetInt32(0),
                            NGOName = reader.GetString(1),
                            Category = Text(reader, 2),
                            LogoURL = Text(reader, 3),
                            City = Text(reader, 4, "Pakistan"),
                            Description = Text(reader, 5),
                            ProgramsCount = reader.GetInt32(6),
                            TotalRaised = Convert.ToDecimal(reader.GetValue(7)),
                            IsVerified = true
                        });
                    }
                }
            }
        }

        private static void LoadProgrammes(SqlConnection connection, CauseDetailViewModel cause)
        {
            const string sql = @"
SELECT
    p.ProgrammeID,
    p.NGOID,
    n.NGOName,
    p.CauseID,
    c.CauseName,
    p.ProgrammeName,
    p.Description,
    p.Location,
    p.StartDate,
    p.EndDate,
    p.TargetAmount,
    (
        SELECT ISNULL(SUM(d.Amount), 0)
        FROM dbo.Donations d
        WHERE d.ProgrammeID = p.ProgrammeID
          AND d.DonationStatus = N'Completed'
    ) AS CurrentAmount,
    p.Status,
    p.ImageURL,
    (
        SELECT COUNT(1)
        FROM dbo.ProgrammeInterests i
        WHERE i.ProgrammeID = p.ProgrammeID
    ) AS InterestedCount
FROM dbo.Programmes p
INNER JOIN dbo.NGOs n ON n.NGOID = p.NGOID
INNER JOIN dbo.Causes c ON c.CauseID = p.CauseID
WHERE p.CauseID = @CauseID
  AND n.IsActive = 1
  AND p.Status <> N'Cancelled'
ORDER BY
    CASE p.Status
        WHEN N'Active' THEN 1
        WHEN N'Upcoming' THEN 2
        ELSE 3
    END,
    p.StartDate DESC;";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CauseID", SqlDbType.Int).Value = cause.CauseID;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new NgoProgramDetailItemViewModel
                        {
                            ProgramID = reader.GetInt32(0),
                            NGOID = reader.GetInt32(1),
                            NGOName = reader.GetString(2),
                            CauseID = reader.GetInt32(3),
                            CauseName = reader.GetString(4),
                            ProgramName = reader.GetString(5),
                            Description = Text(reader, 6),
                            Location = Text(reader, 7),
                            StartDate = reader.GetDateTime(8),
                            EndDate = reader.IsDBNull(9)
                                ? (DateTime?)null
                                : reader.GetDateTime(9),
                            TargetAmount = Convert.ToDecimal(reader.GetValue(10)),
                            CurrentAmount = Convert.ToDecimal(reader.GetValue(11)),
                            Status = reader.GetString(12),
                            ImageURL = Text(reader, 13),
                            InterestedCount = reader.GetInt32(14)
                        };

                        cause.Programs.Add(item);
                        cause.TotalTargetGoal += item.TargetAmount;
                    }
                }
            }
        }

        /* =========================================================
           ADMIN CAUSE LISTING
           ========================================================= */

        public AdminCauseListViewModel GetAdminCauses(
            string search = "",
            string status = "all",
            string feature = "all")
        {
            var model = new AdminCauseListViewModel
            {
                SearchQuery = Clean(search),
                SelectedStatus = NormalizeFilter(status),
                SelectedFeature = NormalizeFilter(feature)
            };

            using (var connection = GetConnection())
            {
                connection.Open();

                model.TotalCauses = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Causes;");

                model.ActiveCauses = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Causes WHERE IsActive = 1;");

                model.InactiveCauses = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Causes WHERE IsActive = 0;");

                model.FeaturedCauses = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Causes WHERE IsFeatured = 1;");

                model.TotalProgrammes = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes;");

                var sql = @"
SELECT
    c.CauseID,
    c.CauseName,
    c.Slug,
    c.ShortDescription,
    c.ImageURL,
    c.IconName,
    c.IsFeatured,
    c.IsActive,
    c.DisplayOrder,
    c.CreatedAt,
    c.UpdatedAt,
    (SELECT COUNT(1) FROM dbo.Programmes p WHERE p.CauseID = c.CauseID) AS TotalProgrammes,
    (
        SELECT COUNT(1)
        FROM dbo.Programmes p
        WHERE p.CauseID = c.CauseID
          AND p.Status IN (N'Active', N'Upcoming')
    ) AS ActiveProgrammes,
    (
        SELECT COUNT(DISTINCT p.NGOID)
        FROM dbo.Programmes p
        WHERE p.CauseID = c.CauseID
    ) AS AssociatedNGOs,
    (
        SELECT ISNULL(SUM(p.TargetAmount), 0)
        FROM dbo.Programmes p
        WHERE p.CauseID = c.CauseID
    ) AS TargetAmount,
    (
        SELECT ISNULL(SUM(d.Amount), 0)
        FROM dbo.Donations d
        WHERE d.CauseID = c.CauseID
          AND d.DonationStatus = N'Completed'
    ) AS CompletedFunds
FROM dbo.Causes c
WHERE 1 = 1";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    sql += @"
  AND
  (
      c.CauseName LIKE @Search OR
      c.Slug LIKE @Search OR
      c.ShortDescription LIKE @Search OR
      c.Description LIKE @Search
  )";

                    parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 250)
                    {
                        Value = "%" + model.SearchQuery + "%"
                    });
                }

                if (model.SelectedStatus == "active")
                {
                    sql += " AND c.IsActive = 1";
                }
                else if (model.SelectedStatus == "inactive")
                {
                    sql += " AND c.IsActive = 0";
                }

                if (model.SelectedFeature == "featured")
                {
                    sql += " AND c.IsFeatured = 1";
                }
                else if (model.SelectedFeature == "standard")
                {
                    sql += " AND c.IsFeatured = 0";
                }

                sql += " ORDER BY c.DisplayOrder, c.CauseName;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddRange(parameters.ToArray());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Causes.Add(new AdminCauseListItemViewModel
                            {
                                CauseID = reader.GetInt32(0),
                                CauseName = reader.GetString(1),
                                Slug = reader.GetString(2),
                                ShortDescription = Text(reader, 3),
                                ImageURL = Text(reader, 4),
                                Icon = Text(reader, 5),
                                IsFeatured = reader.GetBoolean(6),
                                IsActive = reader.GetBoolean(7),
                                DisplayOrder = reader.GetInt32(8),
                                CreatedAt = reader.GetDateTime(9),
                                UpdatedAt = NullableDate(reader, 10),
                                TotalProgrammes = reader.GetInt32(11),
                                ActiveProgrammes = reader.GetInt32(12),
                                AssociatedNGOs = reader.GetInt32(13),
                                TargetAmount = Convert.ToDecimal(reader.GetValue(14)),
                                CompletedFunds = Convert.ToDecimal(reader.GetValue(15))
                            });
                        }
                    }
                }
            }

            return model;
        }

        public CauseAdminFormViewModel GetCauseForAdmin(int causeId)
        {
            const string sql = @"
SELECT
    CauseID,
    CauseName,
    Slug,
    ShortDescription,
    Description,
    ImageURL,
    IconName,
    IsFeatured,
    IsActive,
    DisplayOrder
FROM dbo.Causes
WHERE CauseID = @CauseID;";

            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CauseID", SqlDbType.Int).Value = causeId;
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new CauseAdminFormViewModel
                    {
                        CauseID = reader.GetInt32(0),
                        CauseName = reader.GetString(1),
                        Slug = reader.GetString(2),
                        ShortDescription = Text(reader, 3),
                        Description = Text(reader, 4),
                        ImageURL = Text(reader, 5),
                        Icon = Text(reader, 6),
                        IsFeatured = reader.GetBoolean(7),
                        IsActive = reader.GetBoolean(8),
                        DisplayOrder = reader.GetInt32(9)
                    };
                }
            }
        }

        public bool CreateCause(CauseAdminFormViewModel model, out string message)
        {
            message = string.Empty;

            if (model == null)
            {
                message = "Cause information is required.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                if (CauseIdentityExists(connection, model.CauseName, model.Slug, 0))
                {
                    message = "A cause with the same name or slug already exists.";
                    return false;
                }

                const string sql = @"
INSERT INTO dbo.Causes
(
    CauseName,
    Slug,
    ShortDescription,
    Description,
    ImageURL,
    IconName,
    IsFeatured,
    IsActive,
    DisplayOrder
)
VALUES
(
    @CauseName,
    @Slug,
    @ShortDescription,
    @Description,
    @ImageURL,
    @IconName,
    @IsFeatured,
    @IsActive,
    @DisplayOrder
);";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        AddCauseParameters(command, model);
                        command.ExecuteNonQuery();
                    }

                    message = "Cause created successfully.";
                    return true;
                }
                catch (SqlException exception)
                {
                    message = IsUniqueConstraintError(exception)
                        ? "A cause with the same name or slug already exists."
                        : "The cause could not be created: " + exception.Message;
                    return false;
                }
            }
        }

        public bool UpdateCause(CauseAdminFormViewModel model, out string message)
        {
            message = string.Empty;

            if (model == null || model.CauseID <= 0)
            {
                message = "A valid cause record is required.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                if (CauseIdentityExists(connection, model.CauseName, model.Slug, model.CauseID))
                {
                    message = "Another cause already uses the same name or slug.";
                    return false;
                }

                const string sql = @"
UPDATE dbo.Causes
SET
    CauseName = @CauseName,
    Slug = @Slug,
    ShortDescription = @ShortDescription,
    Description = @Description,
    ImageURL = @ImageURL,
    IconName = @IconName,
    IsFeatured = @IsFeatured,
    IsActive = @IsActive,
    DisplayOrder = @DisplayOrder,
    UpdatedAt = SYSUTCDATETIME()
WHERE CauseID = @CauseID;";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        AddCauseParameters(command, model);
                        command.Parameters.Add("@CauseID", SqlDbType.Int).Value = model.CauseID;

                        if (command.ExecuteNonQuery() == 0)
                        {
                            message = "The selected cause record was not found.";
                            return false;
                        }
                    }

                    message = "Cause updated successfully.";
                    return true;
                }
                catch (SqlException exception)
                {
                    message = IsUniqueConstraintError(exception)
                        ? "Another cause already uses the same name or slug."
                        : "The cause could not be updated: " + exception.Message;
                    return false;
                }
            }
        }

        public bool SetCauseActiveStatus(int causeId, bool makeActive, out string message)
        {
            message = string.Empty;

            if (causeId <= 0)
            {
                message = "A valid cause record is required.";
                return false;
            }

            const string sql = @"
UPDATE dbo.Causes
SET
    IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE CauseID = @CauseID;";

            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = makeActive;
                command.Parameters.Add("@CauseID", SqlDbType.Int).Value = causeId;
                connection.Open();

                if (command.ExecuteNonQuery() == 0)
                {
                    message = "The selected cause record was not found.";
                    return false;
                }
            }

            message = makeActive
                ? "Cause activated and made publicly visible."
                : "Cause deactivated and hidden from public cause pages.";

            return true;
        }

        /* =========================================================
           ADMIN PROGRAMME LISTING
           ========================================================= */

        public AdminProgrammeListViewModel GetAdminProgrammes(
            string search = "",
            string status = "all",
            int? causeId = null,
            int? ngoId = null)
        {
            var model = new AdminProgrammeListViewModel
            {
                SearchQuery = Clean(search),
                SelectedStatus = NormalizeFilter(status),
                SelectedCauseID = ValidNullableId(causeId),
                SelectedNGOID = ValidNullableId(ngoId)
            };

            using (var connection = GetConnection())
            {
                connection.Open();

                LoadAdminLookups(connection, model.AvailableCauses, model.AvailableNGOs, false);

                model.TotalProgrammes = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes;");

                model.ActiveProgrammes = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes WHERE Status = N'Active';");

                model.UpcomingProgrammes = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes WHERE Status = N'Upcoming';");

                model.CompletedProgrammes = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes WHERE Status = N'Completed';");

                model.CancelledProgrammes = ScalarInt(
                    connection,
                    "SELECT COUNT(1) FROM dbo.Programmes WHERE Status = N'Cancelled';");

                model.TotalTargetAmount = ScalarDecimal(
                    connection,
                    "SELECT ISNULL(SUM(TargetAmount), 0) FROM dbo.Programmes;");

                model.CompletedFunds = ScalarDecimal(
                    connection,
                    "SELECT ISNULL(SUM(Amount), 0) FROM dbo.Donations WHERE DonationStatus = N'Completed';");

                var sql = @"
SELECT
    p.ProgrammeID,
    p.NGOID,
    n.NGOName,
    p.CauseID,
    c.CauseName,
    p.ProgrammeName,
    p.Slug,
    p.ShortDescription,
    p.Location,
    p.StartDate,
    p.EndDate,
    p.TargetAmount,
    (
        SELECT ISNULL(SUM(d.Amount), 0)
        FROM dbo.Donations d
        WHERE d.ProgrammeID = p.ProgrammeID
          AND d.DonationStatus = N'Completed'
    ) AS CompletedFunds,
    p.Status,
    p.IsFeatured,
    p.ImageURL,
    p.CreatedAt,
    p.UpdatedAt,
    (
        SELECT COUNT(1)
        FROM dbo.ProgrammeInterests i
        WHERE i.ProgrammeID = p.ProgrammeID
    ) AS InterestedUsers
FROM dbo.Programmes p
INNER JOIN dbo.NGOs n ON n.NGOID = p.NGOID
INNER JOIN dbo.Causes c ON c.CauseID = p.CauseID
WHERE 1 = 1";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    sql += @"
  AND
  (
      p.ProgrammeName LIKE @Search OR
      p.Slug LIKE @Search OR
      p.ShortDescription LIKE @Search OR
      p.Description LIKE @Search OR
      p.Location LIKE @Search OR
      n.NGOName LIKE @Search OR
      c.CauseName LIKE @Search
  )";

                    parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 250)
                    {
                        Value = "%" + model.SearchQuery + "%"
                    });
                }

                if (IsValidProgrammeStatus(model.SelectedStatus))
                {
                    sql += " AND p.Status = @Status";
                    parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 20)
                    {
                        Value = NormaliseProgrammeStatus(model.SelectedStatus)
                    });
                }

                if (model.SelectedCauseID.HasValue)
                {
                    sql += " AND p.CauseID = @CauseID";
                    parameters.Add(new SqlParameter("@CauseID", SqlDbType.Int)
                    {
                        Value = model.SelectedCauseID.Value
                    });
                }

                if (model.SelectedNGOID.HasValue)
                {
                    sql += " AND p.NGOID = @NGOID";
                    parameters.Add(new SqlParameter("@NGOID", SqlDbType.Int)
                    {
                        Value = model.SelectedNGOID.Value
                    });
                }

                sql += @"
ORDER BY
    CASE p.Status
        WHEN N'Active' THEN 1
        WHEN N'Upcoming' THEN 2
        WHEN N'Completed' THEN 3
        ELSE 4
    END,
    p.StartDate DESC,
    p.ProgrammeName;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddRange(parameters.ToArray());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Programmes.Add(new AdminProgrammeListItemViewModel
                            {
                                ProgrammeID = reader.GetInt32(0),
                                NGOID = reader.GetInt32(1),
                                NGOName = reader.GetString(2),
                                CauseID = reader.GetInt32(3),
                                CauseName = reader.GetString(4),
                                ProgrammeName = reader.GetString(5),
                                Slug = reader.GetString(6),
                                ShortDescription = Text(reader, 7),
                                Location = Text(reader, 8),
                                StartDate = reader.GetDateTime(9),
                                EndDate = NullableDate(reader, 10),
                                TargetAmount = Convert.ToDecimal(reader.GetValue(11)),
                                CompletedFunds = Convert.ToDecimal(reader.GetValue(12)),
                                Status = reader.GetString(13),
                                IsFeatured = reader.GetBoolean(14),
                                ImageURL = Text(reader, 15),
                                CreatedAt = reader.GetDateTime(16),
                                UpdatedAt = NullableDate(reader, 17),
                                InterestedUsers = reader.GetInt32(18)
                            });
                        }
                    }
                }
            }

            return model;
        }

        public ProgrammeAdminFormViewModel GetProgrammeForAdmin(int programmeId)
        {
            const string sql = @"
SELECT
    ProgrammeID,
    NGOID,
    CauseID,
    ProgrammeName,
    Slug,
    ShortDescription,
    Description,
    Location,
    StartDate,
    EndDate,
    TargetAmount,
    ImageURL,
    Status,
    IsFeatured
FROM dbo.Programmes
WHERE ProgrammeID = @ProgrammeID;";

            using (var connection = GetConnection())
            {
                connection.Open();

                ProgrammeAdminFormViewModel model = null;

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@ProgrammeID", SqlDbType.Int).Value = programmeId;

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model = new ProgrammeAdminFormViewModel
                            {
                                ProgrammeID = reader.GetInt32(0),
                                NGOID = reader.GetInt32(1),
                                CauseID = reader.GetInt32(2),
                                ProgrammeName = reader.GetString(3),
                                Slug = reader.GetString(4),
                                ShortDescription = Text(reader, 5),
                                Description = Text(reader, 6),
                                Location = Text(reader, 7),
                                StartDate = reader.GetDateTime(8),
                                EndDate = NullableDate(reader, 9),
                                TargetAmount = Convert.ToDecimal(reader.GetValue(10)),
                                ImageURL = Text(reader, 11),
                                Status = reader.GetString(12),
                                IsFeatured = reader.GetBoolean(13)
                            };
                        }
                    }
                }

                if (model != null)
                {
                    LoadAdminLookups(
                        connection,
                        model.AvailableCauses,
                        model.AvailableNGOs,
                        true);
                }

                return model;
            }
        }

        public void PopulateProgrammeLookups(ProgrammeAdminFormViewModel model)
        {
            if (model == null)
            {
                return;
            }

            using (var connection = GetConnection())
            {
                connection.Open();
                LoadAdminLookups(connection, model.AvailableCauses, model.AvailableNGOs, true);
            }
        }

        public bool CreateProgramme(ProgrammeAdminFormViewModel model, out string message)
        {
            message = string.Empty;

            if (model == null)
            {
                message = "Programme information is required.";
                return false;
            }

            model.Status = NormaliseProgrammeStatus(model.Status);

            if (!IsValidProgrammeStatus(model.Status))
            {
                message = "Select a valid programme status.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                string relationshipMessage;
                if (!ValidateProgrammeRelationships(connection, model, out relationshipMessage))
                {
                    message = relationshipMessage;
                    return false;
                }

                if (ProgrammeIdentityExists(connection, model.ProgrammeName, model.Slug, 0))
                {
                    message = "A programme with the same name or slug already exists.";
                    return false;
                }

                const string sql = @"
INSERT INTO dbo.Programmes
(
    NGOID,
    CauseID,
    ProgrammeName,
    Slug,
    ShortDescription,
    Description,
    Location,
    StartDate,
    EndDate,
    TargetAmount,
    ImageURL,
    Status,
    IsFeatured
)
VALUES
(
    @NGOID,
    @CauseID,
    @ProgrammeName,
    @Slug,
    @ShortDescription,
    @Description,
    @Location,
    @StartDate,
    @EndDate,
    @TargetAmount,
    @ImageURL,
    @Status,
    @IsFeatured
);";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        AddProgrammeParameters(command, model);
                        command.ExecuteNonQuery();
                    }

                    message = "Programme created successfully.";
                    return true;
                }
                catch (SqlException exception)
                {
                    message = IsUniqueConstraintError(exception)
                        ? "A programme with the same slug already exists."
                        : "The programme could not be created: " + exception.Message;
                    return false;
                }
            }
        }

        public bool UpdateProgramme(ProgrammeAdminFormViewModel model, out string message)
        {
            message = string.Empty;

            if (model == null || model.ProgrammeID <= 0)
            {
                message = "A valid programme record is required.";
                return false;
            }

            model.Status = NormaliseProgrammeStatus(model.Status);

            if (!IsValidProgrammeStatus(model.Status))
            {
                message = "Select a valid programme status.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                string relationshipMessage;
                if (!ValidateProgrammeRelationships(connection, model, out relationshipMessage))
                {
                    message = relationshipMessage;
                    return false;
                }

                if (ProgrammeIdentityExists(
                    connection,
                    model.ProgrammeName,
                    model.Slug,
                    model.ProgrammeID))
                {
                    message = "Another programme already uses the same name or slug.";
                    return false;
                }

                const string sql = @"
UPDATE dbo.Programmes
SET
    NGOID = @NGOID,
    CauseID = @CauseID,
    ProgrammeName = @ProgrammeName,
    Slug = @Slug,
    ShortDescription = @ShortDescription,
    Description = @Description,
    Location = @Location,
    StartDate = @StartDate,
    EndDate = @EndDate,
    TargetAmount = @TargetAmount,
    ImageURL = @ImageURL,
    Status = @Status,
    IsFeatured = @IsFeatured,
    UpdatedAt = SYSUTCDATETIME()
WHERE ProgrammeID = @ProgrammeID;";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        AddProgrammeParameters(command, model);
                        command.Parameters.Add("@ProgrammeID", SqlDbType.Int).Value = model.ProgrammeID;

                        if (command.ExecuteNonQuery() == 0)
                        {
                            message = "The selected programme record was not found.";
                            return false;
                        }
                    }

                    message = "Programme updated successfully.";
                    return true;
                }
                catch (SqlException exception)
                {
                    message = IsUniqueConstraintError(exception)
                        ? "Another programme already uses the same slug."
                        : "The programme could not be updated: " + exception.Message;
                    return false;
                }
            }
        }

        public bool SetProgrammeStatus(int programmeId, string status, out string message)
        {
            message = string.Empty;
            status = NormaliseProgrammeStatus(status);

            if (programmeId <= 0)
            {
                message = "A valid programme record is required.";
                return false;
            }

            if (!IsValidProgrammeStatus(status))
            {
                message = "Select a valid programme status.";
                return false;
            }

            const string sql = @"
UPDATE dbo.Programmes
SET
    Status = @Status,
    UpdatedAt = SYSUTCDATETIME()
WHERE ProgrammeID = @ProgrammeID;";

            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status;
                command.Parameters.Add("@ProgrammeID", SqlDbType.Int).Value = programmeId;
                connection.Open();

                if (command.ExecuteNonQuery() == 0)
                {
                    message = "The selected programme record was not found.";
                    return false;
                }
            }

            message = "Programme status changed to " + status + ".";
            return true;
        }

        /* =========================================================
           ADMIN DATA HELPERS
           ========================================================= */

        private static void LoadAdminLookups(
            SqlConnection connection,
            List<CauseAdminLookupItem> causes,
            List<CauseAdminLookupItem> ngos,
            bool includeInactive)
        {
            causes.Clear();
            ngos.Clear();

            var causeSql = @"
SELECT CauseID, CauseName, Slug, IsActive
FROM dbo.Causes" +
                (includeInactive ? string.Empty : " WHERE IsActive = 1") +
                " ORDER BY DisplayOrder, CauseName;";

            using (var command = new SqlCommand(causeSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    causes.Add(new CauseAdminLookupItem
                    {
                        ID = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        SecondaryText = reader.GetString(2),
                        IsActive = reader.GetBoolean(3)
                    });
                }
            }

            var ngoSql = @"
SELECT NGOID, NGOName, City, IsActive
FROM dbo.NGOs" +
                (includeInactive ? string.Empty : " WHERE IsActive = 1") +
                " ORDER BY NGOName;";

            using (var command = new SqlCommand(ngoSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    ngos.Add(new CauseAdminLookupItem
                    {
                        ID = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        SecondaryText = Text(reader, 2),
                        IsActive = reader.GetBoolean(3)
                    });
                }
            }
        }

        private static bool ValidateProgrammeRelationships(
            SqlConnection connection,
            ProgrammeAdminFormViewModel model,
            out string message)
        {
            message = string.Empty;

            using (var command = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.NGOs WHERE NGOID = @NGOID;",
                connection))
            {
                command.Parameters.Add("@NGOID", SqlDbType.Int).Value = model.NGOID;

                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    message = "The selected NGO record does not exist.";
                    return false;
                }
            }

            using (var command = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Causes WHERE CauseID = @CauseID;",
                connection))
            {
                command.Parameters.Add("@CauseID", SqlDbType.Int).Value = model.CauseID;

                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    message = "The selected cause record does not exist.";
                    return false;
                }
            }

            return true;
        }

        private static bool CauseIdentityExists(
            SqlConnection connection,
            string causeName,
            string slug,
            int excludingCauseId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.Causes
WHERE (CauseName = @CauseName OR Slug = @Slug)
  AND CauseID <> @CauseID;";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CauseName", SqlDbType.NVarChar, 120).Value = Clean(causeName);
                command.Parameters.Add("@Slug", SqlDbType.NVarChar, 140).Value = Clean(slug);
                command.Parameters.Add("@CauseID", SqlDbType.Int).Value = excludingCauseId;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static bool ProgrammeIdentityExists(
            SqlConnection connection,
            string programmeName,
            string slug,
            int excludingProgrammeId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.Programmes
WHERE (ProgrammeName = @ProgrammeName OR Slug = @Slug)
  AND ProgrammeID <> @ProgrammeID;";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ProgrammeName", SqlDbType.NVarChar, 180).Value = Clean(programmeName);
                command.Parameters.Add("@Slug", SqlDbType.NVarChar, 200).Value = Clean(slug);
                command.Parameters.Add("@ProgrammeID", SqlDbType.Int).Value = excludingProgrammeId;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void AddCauseParameters(
            SqlCommand command,
            CauseAdminFormViewModel model)
        {
            command.Parameters.Add("@CauseName", SqlDbType.NVarChar, 120).Value = Clean(model.CauseName);
            command.Parameters.Add("@Slug", SqlDbType.NVarChar, 140).Value = Clean(model.Slug).ToLowerInvariant();
            command.Parameters.Add("@ShortDescription", SqlDbType.NVarChar, 300).Value = DbText(model.ShortDescription);
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 2000).Value = DbText(model.Description);
            command.Parameters.Add("@ImageURL", SqlDbType.NVarChar, 500).Value = DbText(model.ImageURL);
            command.Parameters.Add("@IconName", SqlDbType.NVarChar, 100).Value = DbText(model.Icon);
            command.Parameters.Add("@IsFeatured", SqlDbType.Bit).Value = model.IsFeatured;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = model.IsActive;
            command.Parameters.Add("@DisplayOrder", SqlDbType.Int).Value = model.DisplayOrder;
        }

        private static void AddProgrammeParameters(
            SqlCommand command,
            ProgrammeAdminFormViewModel model)
        {
            command.Parameters.Add("@NGOID", SqlDbType.Int).Value = model.NGOID;
            command.Parameters.Add("@CauseID", SqlDbType.Int).Value = model.CauseID;
            command.Parameters.Add("@ProgrammeName", SqlDbType.NVarChar, 180).Value = Clean(model.ProgrammeName);
            command.Parameters.Add("@Slug", SqlDbType.NVarChar, 200).Value = Clean(model.Slug).ToLowerInvariant();
            command.Parameters.Add("@ShortDescription", SqlDbType.NVarChar, 350).Value = DbText(model.ShortDescription);
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 3000).Value = DbText(model.Description);
            command.Parameters.Add("@Location", SqlDbType.NVarChar, 500).Value = DbText(model.Location);
            command.Parameters.Add("@StartDate", SqlDbType.Date).Value = model.StartDate.Date;
            command.Parameters.Add("@EndDate", SqlDbType.Date).Value = model.EndDate.HasValue
                ? (object)model.EndDate.Value.Date
                : DBNull.Value;
            command.Parameters.Add("@TargetAmount", SqlDbType.Decimal).Value = model.TargetAmount;
            command.Parameters["@TargetAmount"].Precision = 18;
            command.Parameters["@TargetAmount"].Scale = 2;
            command.Parameters.Add("@ImageURL", SqlDbType.NVarChar, 500).Value = DbText(model.ImageURL);
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = model.Status;
            command.Parameters.Add("@IsFeatured", SqlDbType.Bit).Value = model.IsFeatured;
        }

        private static bool IsUniqueConstraintError(SqlException exception)
        {
            return exception != null &&
                   (exception.Number == 2601 || exception.Number == 2627);
        }

        private static string NormalizeFilter(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "all"
                : value.Trim().ToLowerInvariant();
        }

        private static int? ValidNullableId(int? value)
        {
            return value.HasValue && value.Value > 0
                ? value
                : null;
        }

        private static string NormaliseProgrammeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "Upcoming";
            }

            status = status.Trim();

            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                return "Active";
            }

            if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                return "Completed";
            }

            if (status.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return "Cancelled";
            }

            return "Upcoming";
        }

        private static bool IsValidProgrammeStatus(string status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   (
                       status.Equals("Upcoming", StringComparison.OrdinalIgnoreCase) ||
                       status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                       status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                       status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                   );
        }

        private static object DbText(string value)
        {
            var cleanValue = Clean(value);
            return cleanValue == null ? (object)DBNull.Value : cleanValue;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Text(
            SqlDataReader reader,
            int ordinal,
            string fallback = "")
        {
            return reader.IsDBNull(ordinal)
                ? fallback
                : reader.GetString(ordinal);
        }

        private static DateTime? NullableDate(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (DateTime?)null
                : reader.GetDateTime(ordinal);
        }

        private static int ScalarInt(SqlConnection connection, string sql)
        {
            using (var command = new SqlCommand(sql, connection))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static decimal ScalarDecimal(SqlConnection connection, string sql)
        {
            using (var command = new SqlCommand(sql, connection))
            {
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        private static string CauseKey(string name)
        {
            var value = (name ?? string.Empty).ToLowerInvariant();

            if (value.Contains("water")) return "water";
            if (value.Contains("health")) return "health";
            if (value.Contains("education")) return "education";
            if (value.Contains("child")) return "children";
            if (value.Contains("women")) return "women";
            if (value.Contains("elder")) return "elderly";

            return "community";
        }
    }
}
