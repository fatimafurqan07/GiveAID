
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class NgoRepository
    {
        private readonly string _connectionString;

        public NgoRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];
            _connectionString = setting != null && !string.IsNullOrWhiteSpace(setting.ConnectionString)
                ? setting.ConnectionString
                : @"Data Source=localhost\SQLEXPRESS;Initial Catalog=GiveAID;Integrated Security=True;TrustServerCertificate=True;";
        }

        private SqlConnection GetConnection() { return new SqlConnection(_connectionString); }

        public NgoListViewModel GetPublicNgos(string search = null, string location = null, int? causeId = null, string category = null)
        {
            var model = new NgoListViewModel
            {
                SearchQuery = Clean(search),
                SelectedLocation = Clean(location),
                SelectedCauseId = causeId,
                SelectedCategory = Clean(category)
            };

            using (var connection = GetConnection())
            {
                connection.Open();
                LoadFilters(connection, model);

                var sql = @"
SELECT n.NGOID, n.NGOName, n.RegistrationNumber, n.Category, n.Description,
       n.Address, n.City, n.Country, n.Phone, n.Email, n.LogoURL, n.WebsiteURL, n.CreatedAt,
       (SELECT COUNT(1) FROM dbo.Programmes p
        WHERE p.NGOID = n.NGOID AND p.Status = N'Active') AS ActiveProgrammes,
       (SELECT COUNT(DISTINCT p.CauseID) FROM dbo.Programmes p
        INNER JOIN dbo.Causes c ON c.CauseID = p.CauseID
        WHERE p.NGOID = n.NGOID AND c.IsActive = 1 AND p.Status <> N'Cancelled') AS CauseCount,
       (SELECT ISNULL(SUM(d.Amount), 0) FROM dbo.Donations d
        WHERE d.NGOID = n.NGOID AND d.DonationStatus = N'Completed') AS TotalRaised
FROM dbo.NGOs n
WHERE n.IsActive = 1";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    sql += @" AND (n.NGOName LIKE @Search OR n.Description LIKE @Search OR n.City LIKE @Search
                             OR n.Category LIKE @Search
                             OR EXISTS (SELECT 1 FROM dbo.Programmes p WHERE p.NGOID = n.NGOID AND p.ProgrammeName LIKE @Search)
                             OR EXISTS (SELECT 1 FROM dbo.Programmes p INNER JOIN dbo.Causes c ON c.CauseID = p.CauseID
                                        WHERE p.NGOID = n.NGOID AND c.CauseName LIKE @Search))";
                    parameters.Add(new SqlParameter("@Search", "%" + model.SearchQuery + "%"));
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedLocation) && !model.SelectedLocation.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " AND n.City = @Location";
                    parameters.Add(new SqlParameter("@Location", model.SelectedLocation));
                }

                if (causeId.HasValue && causeId.Value > 0)
                {
                    sql += " AND EXISTS (SELECT 1 FROM dbo.Programmes p WHERE p.NGOID = n.NGOID AND p.CauseID = @CauseID AND p.Status <> N'Cancelled')";
                    parameters.Add(new SqlParameter("@CauseID", causeId.Value));
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedCategory) && !model.SelectedCategory.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    sql += @" AND (n.Category = @Category OR EXISTS
                             (SELECT 1 FROM dbo.Programmes p INNER JOIN dbo.Causes c ON c.CauseID = p.CauseID
                              WHERE p.NGOID = n.NGOID AND c.CauseName = @Category))";
                    parameters.Add(new SqlParameter("@Category", model.SelectedCategory));
                }

                sql += " ORDER BY n.NGOName;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddRange(parameters.ToArray());
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.NGOs.Add(new NgoListItemViewModel
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                RegistrationNumber = Text(reader, 2),
                                Category = Text(reader, 3),
                                Description = Text(reader, 4),
                                Address = Text(reader, 5),
                                City = Text(reader, 6, "Pakistan"),
                                Country = Text(reader, 7, "Pakistan"),
                                Phone = Text(reader, 8),
                                Email = Text(reader, 9),
                                LogoURL = Text(reader, 10),
                                WebsiteURL = Text(reader, 11),
                                CreatedAt = reader.GetDateTime(12),
                                ActiveProgramsCount = reader.GetInt32(13),
                                CausesSupportedCount = reader.GetInt32(14),
                                TotalFundsRaised = Convert.ToDecimal(reader.GetValue(15)),
                                Status = "Active"
                            });
                        }
                    }
                }

                foreach (var ngo in model.NGOs) LoadNgoCauses(connection, ngo);

                model.TotalActiveNgos = ScalarInt(connection, "SELECT COUNT(1) FROM dbo.NGOs WHERE IsActive = 1;");
                model.TotalActivePrograms = ScalarInt(connection, "SELECT COUNT(1) FROM dbo.Programmes WHERE Status = N'Active';");
                model.TotalImpactRaised = ScalarDecimal(connection, "SELECT ISNULL(SUM(Amount), 0) FROM dbo.Donations WHERE DonationStatus = N'Completed';");
            }

            return model;
        }

        public NgoDetailViewModel GetNgoById(int ngoId)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                NgoDetailViewModel ngo = null;

                const string ngoSql = @"
SELECT NGOID, NGOName, RegistrationNumber, Category, Description, Address, City, Country,
       Phone, Email, LogoURL, WebsiteURL, CreatedAt
FROM dbo.NGOs
WHERE NGOID = @NGOID AND IsActive = 1;";

                using (var command = new SqlCommand(ngoSql, connection))
                {
                    command.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ngo = new NgoDetailViewModel
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                RegistrationNumber = Text(reader, 2),
                                Category = Text(reader, 3),
                                Description = Text(reader, 4),
                                Address = Text(reader, 5),
                                City = Text(reader, 6, "Pakistan"),
                                Country = Text(reader, 7, "Pakistan"),
                                Phone = Text(reader, 8),
                                Email = Text(reader, 9),
                                LogoURL = Text(reader, 10),
                                WebsiteURL = Text(reader, 11),
                                CreatedAt = reader.GetDateTime(12),
                                Status = "Active"
                            };
                        }
                    }
                }

                if (ngo == null) return null;

                const string statsSql = @"
SELECT ISNULL(SUM(Amount),0), COUNT(DISTINCT DonationID), COUNT(DISTINCT UserID)
FROM dbo.Donations WHERE NGOID = @NGOID AND DonationStatus = N'Completed';";
                using (var command = new SqlCommand(statsSql, connection))
                {
                    command.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ngo.TotalFundsRaised = Convert.ToDecimal(reader.GetValue(0));
                            ngo.TotalDonationsCount = reader.GetInt32(1);
                            ngo.TotalDonorsCount = reader.GetInt32(2);
                        }
                    }
                }

                LoadDetailCauses(connection, ngo);
                LoadDetailProgrammes(connection, ngo);
                return ngo;
            }
        }

        // =========================================================
        // ADMIN NGO MANAGEMENT
        // =========================================================
        public AdminNgoListViewModel GetAdminNgos(string search = null, string status = "all", string category = null)
        {
            var model = new AdminNgoListViewModel
            {
                SearchQuery = Clean(search),
                SelectedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant(),
                SelectedCategory = Clean(category)
            };

            using (var connection = GetConnection())
            {
                connection.Open();

                using (var command = new SqlCommand(@"
SELECT DISTINCT Category
FROM dbo.NGOs
WHERE NULLIF(LTRIM(RTRIM(Category)), N'') IS NOT NULL
ORDER BY Category;", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.AvailableCategories.Add(reader.GetString(0));
                    }
                }

                var sql = @"
SELECT n.NGOID, n.NGOName, n.RegistrationNumber, n.Category, n.City, n.Country,
       n.Email, n.Phone, n.ContactPerson, n.IsActive, n.CreatedAt, n.UpdatedAt,
       (SELECT COUNT(1) FROM dbo.Programmes p WHERE p.NGOID = n.NGOID) AS ProgrammesCount,
       (SELECT COUNT(1) FROM dbo.Programmes p
        WHERE p.NGOID = n.NGOID AND p.Status = N'Active') AS ActiveProgrammesCount,
       (SELECT ISNULL(SUM(d.Amount), 0) FROM dbo.Donations d
        WHERE d.NGOID = n.NGOID AND d.DonationStatus = N'Completed') AS CompletedFunds
FROM dbo.NGOs n
WHERE 1 = 1";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(model.SearchQuery))
                {
                    sql += @" AND
                    (
                        n.NGOName LIKE @Search OR
                        n.RegistrationNumber LIKE @Search OR
                        n.Category LIKE @Search OR
                        n.City LIKE @Search OR
                        n.Email LIKE @Search OR
                        n.ContactPerson LIKE @Search
                    )";
                    parameters.Add(new SqlParameter("@Search", "%" + model.SearchQuery + "%"));
                }

                if (model.SelectedStatus == "active")
                {
                    sql += " AND n.IsActive = 1";
                }
                else if (model.SelectedStatus == "inactive")
                {
                    sql += " AND n.IsActive = 0";
                }

                if (!string.IsNullOrWhiteSpace(model.SelectedCategory) &&
                    !model.SelectedCategory.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " AND n.Category = @Category";
                    parameters.Add(new SqlParameter("@Category", model.SelectedCategory));
                }

                sql += " ORDER BY n.IsActive DESC, n.NGOName;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddRange(parameters.ToArray());

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.NGOs.Add(new AdminNgoListItemViewModel
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                RegistrationNumber = Text(reader, 2),
                                Category = Text(reader, 3),
                                City = Text(reader, 4),
                                Country = Text(reader, 5, "Pakistan"),
                                Email = Text(reader, 6),
                                Phone = Text(reader, 7),
                                ContactPerson = Text(reader, 8),
                                IsActive = reader.GetBoolean(9),
                                CreatedAt = reader.GetDateTime(10),
                                UpdatedAt = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                                ProgrammesCount = reader.GetInt32(12),
                                ActiveProgrammesCount = reader.GetInt32(13),
                                CompletedFunds = Convert.ToDecimal(reader.GetValue(14))
                            });
                        }
                    }
                }

                using (var command = new SqlCommand(@"
SELECT COUNT(1),
       ISNULL(SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END), 0),
       ISNULL(SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END), 0)
FROM dbo.NGOs;", connection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.TotalNGOs = reader.GetInt32(0);
                        model.ActiveNGOs = Convert.ToInt32(reader.GetValue(1));
                        model.InactiveNGOs = Convert.ToInt32(reader.GetValue(2));
                    }
                }

                model.TotalProgrammes = ScalarInt(connection, "SELECT COUNT(1) FROM dbo.Programmes;");
            }

            return model;
        }

        public NgoAdminFormViewModel GetNgoForAdmin(int ngoId)
        {
            using (var connection = GetConnection())
            {
                connection.Open();

                const string sql = @"
SELECT NGOID, NGOName, RegistrationNumber, Category, Description, Address,
       City, Country, Phone, Email, LogoURL, WebsiteURL, ContactPerson, IsActive
FROM dbo.NGOs
WHERE NGOID = @NGOID;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@NGOID", ngoId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) return null;

                        return new NgoAdminFormViewModel
                        {
                            NGOID = reader.GetInt32(0),
                            NGOName = reader.GetString(1),
                            RegistrationNumber = Text(reader, 2),
                            Category = Text(reader, 3),
                            Description = Text(reader, 4),
                            Address = Text(reader, 5),
                            City = Text(reader, 6),
                            Country = Text(reader, 7, "Pakistan"),
                            Phone = Text(reader, 8),
                            Email = Text(reader, 9),
                            LogoURL = Text(reader, 10),
                            WebsiteURL = Text(reader, 11),
                            ContactPerson = Text(reader, 12),
                            IsActive = reader.GetBoolean(13)
                        };
                    }
                }
            }
        }

        public bool CreateNgo(NgoAdminFormViewModel model, out string message)
        {
            if (model == null)
            {
                message = "NGO information is required.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                if (NgoIdentityExists(connection, model.NGOName, model.RegistrationNumber, model.Email, null, out message))
                {
                    return false;
                }

                const string sql = @"
INSERT INTO dbo.NGOs
(
    NGOName, RegistrationNumber, Category, Description, Email, Phone,
    Address, City, Country, WebsiteURL, LogoURL, ContactPerson,
    IsActive, CreatedAt, UpdatedAt
)
VALUES
(
    @NGOName, @RegistrationNumber, @Category, @Description, @Email, @Phone,
    @Address, @City, @Country, @WebsiteURL, @LogoURL, @ContactPerson,
    @IsActive, SYSUTCDATETIME(), NULL
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        AddNgoParameters(command, model);
                        model.NGOID = Convert.ToInt32(command.ExecuteScalar());
                    }

                    message = "NGO has been added successfully.";
                    return true;
                }
                catch (SqlException exception)
                {
                    message = exception.Number == 2601 || exception.Number == 2627
                        ? "An NGO with the same name already exists."
                        : "The NGO could not be added to the database.";
                    return false;
                }
            }
        }

        public bool UpdateNgo(NgoAdminFormViewModel model, out string message)
        {
            if (model == null || model.NGOID <= 0)
            {
                message = "A valid NGO record is required.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                if (!NgoExists(connection, model.NGOID))
                {
                    message = "The selected NGO record was not found.";
                    return false;
                }

                if (NgoIdentityExists(connection, model.NGOName, model.RegistrationNumber, model.Email, model.NGOID, out message))
                {
                    return false;
                }

                const string sql = @"
UPDATE dbo.NGOs
SET NGOName = @NGOName,
    RegistrationNumber = @RegistrationNumber,
    Category = @Category,
    Description = @Description,
    Email = @Email,
    Phone = @Phone,
    Address = @Address,
    City = @City,
    Country = @Country,
    WebsiteURL = @WebsiteURL,
    LogoURL = @LogoURL,
    ContactPerson = @ContactPerson,
    IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE NGOID = @NGOID;";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        AddNgoParameters(command, model);
                        command.Parameters.AddWithValue("@NGOID", model.NGOID);

                        if (command.ExecuteNonQuery() != 1)
                        {
                            message = "The NGO record was not updated.";
                            return false;
                        }
                    }

                    message = "NGO information has been updated successfully.";
                    return true;
                }
                catch (SqlException exception)
                {
                    message = exception.Number == 2601 || exception.Number == 2627
                        ? "An NGO with the same name already exists."
                        : "The NGO could not be updated in the database.";
                    return false;
                }
            }
        }

        public bool SetNgoActiveStatus(int ngoId, bool makeActive, out string message)
        {
            if (ngoId <= 0)
            {
                message = "A valid NGO record is required.";
                return false;
            }

            using (var connection = GetConnection())
            {
                connection.Open();

                const string sql = @"
UPDATE dbo.NGOs
SET IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE NGOID = @NGOID;";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@IsActive", makeActive);
                    command.Parameters.AddWithValue("@NGOID", ngoId);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        message = "The selected NGO record was not found.";
                        return false;
                    }
                }

                message = makeActive
                    ? "NGO has been activated and is visible on the public website."
                    : "NGO has been deactivated and removed from the public directory.";
                return true;
            }
        }

        private static bool NgoExists(SqlConnection connection, int ngoId)
        {
            using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.NGOs WHERE NGOID = @NGOID;", connection))
            {
                command.Parameters.AddWithValue("@NGOID", ngoId);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static bool NgoIdentityExists(
            SqlConnection connection,
            string ngoName,
            string registrationNumber,
            string email,
            int? excludedNgoId,
            out string message)
        {
            const string sql = @"
SELECT TOP (1)
       CASE
           WHEN LOWER(LTRIM(RTRIM(NGOName))) = LOWER(LTRIM(RTRIM(@NGOName))) THEN N'Name'
           WHEN NULLIF(LTRIM(RTRIM(@RegistrationNumber)), N'') IS NOT NULL
                AND LOWER(LTRIM(RTRIM(RegistrationNumber))) = LOWER(LTRIM(RTRIM(@RegistrationNumber))) THEN N'Registration'
           WHEN NULLIF(LTRIM(RTRIM(@Email)), N'') IS NOT NULL
                AND LOWER(LTRIM(RTRIM(Email))) = LOWER(LTRIM(RTRIM(@Email))) THEN N'Email'
       END
FROM dbo.NGOs
WHERE (@ExcludedNGOID IS NULL OR NGOID <> @ExcludedNGOID)
  AND
  (
      LOWER(LTRIM(RTRIM(NGOName))) = LOWER(LTRIM(RTRIM(@NGOName)))
      OR
      (
          NULLIF(LTRIM(RTRIM(@RegistrationNumber)), N'') IS NOT NULL
          AND LOWER(LTRIM(RTRIM(RegistrationNumber))) = LOWER(LTRIM(RTRIM(@RegistrationNumber)))
      )
      OR
      (
          NULLIF(LTRIM(RTRIM(@Email)), N'') IS NOT NULL
          AND LOWER(LTRIM(RTRIM(Email))) = LOWER(LTRIM(RTRIM(@Email)))
      )
  );";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NGOName", Clean(ngoName) ?? string.Empty);
                command.Parameters.AddWithValue("@RegistrationNumber", Clean(registrationNumber) ?? string.Empty);
                command.Parameters.AddWithValue("@Email", Clean(email) ?? string.Empty);
                command.Parameters.AddWithValue("@ExcludedNGOID", (object)excludedNgoId ?? DBNull.Value);

                var duplicateType = Convert.ToString(command.ExecuteScalar());

                if (string.IsNullOrWhiteSpace(duplicateType))
                {
                    message = null;
                    return false;
                }

                message = duplicateType == "Registration"
                    ? "This registration number is already assigned to another NGO."
                    : duplicateType == "Email"
                        ? "This email address is already assigned to another NGO."
                        : "An NGO with this name already exists.";
                return true;
            }
        }

        private static void AddNgoParameters(SqlCommand command, NgoAdminFormViewModel model)
        {
            command.Parameters.AddWithValue("@NGOName", Clean(model.NGOName) ?? string.Empty);
            command.Parameters.AddWithValue("@RegistrationNumber", DbText(model.RegistrationNumber));
            command.Parameters.AddWithValue("@Category", DbText(model.Category));
            command.Parameters.AddWithValue("@Description", DbText(model.Description));
            command.Parameters.AddWithValue("@Email", DbText(model.Email));
            command.Parameters.AddWithValue("@Phone", DbText(model.Phone));
            command.Parameters.AddWithValue("@Address", DbText(model.Address));
            command.Parameters.AddWithValue("@City", DbText(model.City));
            command.Parameters.AddWithValue("@Country", DbText(string.IsNullOrWhiteSpace(model.Country) ? "Pakistan" : model.Country));
            command.Parameters.AddWithValue("@WebsiteURL", DbText(model.WebsiteURL));
            command.Parameters.AddWithValue("@LogoURL", DbText(model.LogoURL));
            command.Parameters.AddWithValue("@ContactPerson", DbText(model.ContactPerson));
            command.Parameters.AddWithValue("@IsActive", model.IsActive);
        }

        private static object DbText(string value)
        {
            var cleaned = Clean(value);
            return string.IsNullOrWhiteSpace(cleaned) ? (object)DBNull.Value : cleaned;
        }

        private static void LoadFilters(SqlConnection connection, NgoListViewModel model)
        {
            using (var command = new SqlCommand("SELECT DISTINCT City FROM dbo.NGOs WHERE IsActive=1 AND NULLIF(LTRIM(RTRIM(City)),N'') IS NOT NULL ORDER BY City;", connection))
            using (var reader = command.ExecuteReader()) while (reader.Read()) model.AvailableLocations.Add(reader.GetString(0));

            using (var command = new SqlCommand("SELECT DISTINCT Category FROM dbo.NGOs WHERE IsActive=1 AND NULLIF(LTRIM(RTRIM(Category)),N'') IS NOT NULL ORDER BY Category;", connection))
            using (var reader = command.ExecuteReader()) while (reader.Read()) model.AvailableCategories.Add(reader.GetString(0));

            using (var command = new SqlCommand("SELECT CauseID, CauseName FROM dbo.Causes WHERE IsActive=1 ORDER BY DisplayOrder, CauseName;", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) model.AvailableCauses.Add(new LookupItem { ID = reader.GetInt32(0), Name = reader.GetString(1) });
        }

        private static void LoadNgoCauses(SqlConnection connection, NgoListItemViewModel ngo)
        {
            const string sql = @"SELECT DISTINCT c.CauseID,c.CauseName FROM dbo.Causes c
INNER JOIN dbo.Programmes p ON p.CauseID=c.CauseID
WHERE p.NGOID=@NGOID AND c.IsActive=1 AND p.Status<>N'Cancelled' ORDER BY c.CauseName;";
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NGOID", ngo.NGOID);
                using (var reader = command.ExecuteReader()) while (reader.Read()) { ngo.CauseIdsList.Add(reader.GetInt32(0)); ngo.CausesList.Add(reader.GetString(1)); }
            }
            if (ngo.CausesList.Count == 0 && !string.IsNullOrWhiteSpace(ngo.Category)) ngo.CausesList.Add(ngo.Category);
        }

        private static void LoadDetailCauses(SqlConnection connection, NgoDetailViewModel ngo)
        {
            const string sql = @"
SELECT c.CauseID,c.CauseName,c.Description,c.ImageURL,COUNT(DISTINCT p.ProgrammeID),ISNULL(SUM(CASE WHEN d.DonationStatus=N'Completed' THEN d.Amount ELSE 0 END),0)
FROM dbo.Causes c INNER JOIN dbo.Programmes p ON p.CauseID=c.CauseID
LEFT JOIN dbo.Donations d ON d.ProgrammeID=p.ProgrammeID
WHERE p.NGOID=@NGOID AND c.IsActive=1 AND p.Status<>N'Cancelled'
GROUP BY c.CauseID,c.CauseName,c.Description,c.ImageURL ORDER BY c.CauseName;";
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NGOID", ngo.NGOID);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) ngo.Causes.Add(new NgoCauseItemViewModel { CauseID = reader.GetInt32(0), CauseName = reader.GetString(1), Description = Text(reader, 2), ImageURL = Text(reader, 3), ProgramsCount = reader.GetInt32(4), TotalRaised = Convert.ToDecimal(reader.GetValue(5)), Icon = CauseKey(reader.GetString(1)) });
            }
        }

        private static void LoadDetailProgrammes(SqlConnection connection, NgoDetailViewModel ngo)
        {
            const string sql = @"
SELECT p.ProgrammeID,p.NGOID,n.NGOName,p.CauseID,c.CauseName,p.ProgrammeName,p.Description,p.Location,p.StartDate,p.EndDate,p.TargetAmount,
       (SELECT ISNULL(SUM(d.Amount),0) FROM dbo.Donations d WHERE d.ProgrammeID=p.ProgrammeID AND d.DonationStatus=N'Completed'),
       p.Status,p.ImageURL,(SELECT COUNT(1) FROM dbo.ProgrammeInterests i WHERE i.ProgrammeID=p.ProgrammeID)
FROM dbo.Programmes p INNER JOIN dbo.NGOs n ON n.NGOID=p.NGOID INNER JOIN dbo.Causes c ON c.CauseID=p.CauseID
WHERE p.NGOID=@NGOID AND p.Status<>N'Cancelled'
ORDER BY CASE p.Status WHEN N'Active' THEN 1 WHEN N'Upcoming' THEN 2 ELSE 3 END,p.StartDate DESC;";
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NGOID", ngo.NGOID);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) ngo.Programs.Add(new NgoProgramDetailItemViewModel { ProgramID = reader.GetInt32(0), NGOID = reader.GetInt32(1), NGOName = reader.GetString(2), CauseID = reader.GetInt32(3), CauseName = reader.GetString(4), ProgramName = reader.GetString(5), Description = Text(reader, 6), Location = Text(reader, 7), StartDate = reader.GetDateTime(8), EndDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9), TargetAmount = Convert.ToDecimal(reader.GetValue(10)), CurrentAmount = Convert.ToDecimal(reader.GetValue(11)), Status = reader.GetString(12), ImageURL = Text(reader, 13), InterestedCount = reader.GetInt32(14) });
            }
        }

        private static string Clean(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
        private static string Text(SqlDataReader reader, int ordinal, string fallback = "") { return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal); }
        private static int ScalarInt(SqlConnection c, string sql) { using (var cmd = new SqlCommand(sql, c)) return Convert.ToInt32(cmd.ExecuteScalar()); }
        private static decimal ScalarDecimal(SqlConnection c, string sql) { using (var cmd = new SqlCommand(sql, c)) return Convert.ToDecimal(cmd.ExecuteScalar()); }
        private static string CauseKey(string name) { var x = (name ?? "").ToLowerInvariant(); if (x.Contains("water")) return "water"; if (x.Contains("health")) return "health"; if (x.Contains("education")) return "education"; if (x.Contains("child")) return "children"; if (x.Contains("women")) return "women"; return "community"; }
    }
}
