using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class DashboardRepository
    {
        private readonly string _connectionString;

        public DashboardRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
                throw new ConfigurationErrorsException("GiveAIDConnection is missing from Web.config.");
            _connectionString = setting.ConnectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        public AdminDashboardViewModel GetAdminDashboardData()
        {
            var model = new AdminDashboardViewModel();
            using (var conn = GetConnection())
            {
                conn.Open();
                model.TotalUsers = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Users WHERE IsActive=1");
                model.TotalNGOs = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.NGOs WHERE IsActive=1");
                model.TotalPrograms = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Programmes WHERE Status IN (N'Active',N'Upcoming')");
                model.TotalCauses = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Causes WHERE IsActive=1");
                model.PendingApplicationsCount = 0;
                model.PendingDonationsCount = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Donations WHERE DonationStatus=N'Pending'");

                using (var cmd = new SqlCommand("SELECT COUNT(*),COALESCE(SUM(Amount),0) FROM dbo.Donations WHERE DonationStatus=N'Completed'", conn))
                using (var reader = cmd.ExecuteReader())
                    if (reader.Read()) { model.TotalDonationsCount = reader.GetInt32(0); model.TotalFundsRaised = reader.GetDecimal(1); }

                ReadChart(conn, @"SELECT FORMAT(DonationDate,'MMM yyyy'),FORMAT(DonationDate,'yyyyMM'),SUM(Amount)
                    FROM dbo.Donations WHERE DonationStatus=N'Completed'
                    GROUP BY FORMAT(DonationDate,'MMM yyyy'),FORMAT(DonationDate,'yyyyMM') ORDER BY 2",
                    model.MonthlyLabels, model.MonthlyAmounts, 0, 2);

                ReadChart(conn, @"SELECT c.CauseName,SUM(d.Amount) FROM dbo.Donations d
                    JOIN dbo.Causes c ON c.CauseID=d.CauseID WHERE d.DonationStatus=N'Completed'
                    GROUP BY c.CauseName ORDER BY 2 DESC", model.CauseLabels, model.CauseAmounts, 0, 1);

                using (var cmd = new SqlCommand("SELECT Status,COUNT(*) FROM dbo.Programmes GROUP BY Status", conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) { model.ProgramStatusLabels.Add(reader.GetString(0)); model.ProgramStatusCounts.Add(reader.GetInt32(1)); }

                foreach (var item in ReadDonations(conn, null))
                {
                    model.AllDonations.Add(item);
                    if (model.RecentDonations.Count < 10) model.RecentDonations.Add(item);
                }

                using (var cmd = new SqlCommand(@"SELECT TOP 5 u.UserID,u.FullName,u.Email,COALESCE(r.RoleName,N'User'),u.CreatedAt,u.IsActive
                    FROM dbo.Users u LEFT JOIN dbo.UserRoles ur ON ur.UserID=u.UserID LEFT JOIN dbo.Roles r ON r.RoleID=ur.RoleID
                    ORDER BY u.CreatedAt DESC", conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) model.RecentUsers.Add(new RecentUserItem { UserID = reader.GetInt32(0), FullName = reader.GetString(1), Email = reader.GetString(2), RoleName = reader.GetString(3), CreatedAt = reader.GetDateTime(4), IsActive = reader.GetBoolean(5) });
            }
            return model;
        }

        public UserDashboardViewModel GetUserDashboardData(int userId)
        {
            var model = new UserDashboardViewModel { UserID = userId };
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT FullName,Email,CreatedAt FROM dbo.Users WHERE UserID=@id", conn))
                { cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId; using (var r = cmd.ExecuteReader()) if (r.Read()) { model.FullName = r.GetString(0); model.Email = r.GetString(1); model.MemberSince = r.GetDateTime(2); } }
                using (var cmd = new SqlCommand("SELECT COUNT(*),COALESCE(SUM(Amount),0),COUNT(DISTINCT CauseID) FROM dbo.Donations WHERE UserID=@id AND DonationStatus=N'Completed'", conn))
                { cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId; using (var r = cmd.ExecuteReader()) if (r.Read()) { model.TotalDonationsCount = r.GetInt32(0); model.TotalDonated = r.GetDecimal(1); model.CausesSupportedCount = r.GetInt32(2); } }
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.ProgrammeInterests WHERE UserID=@id AND Status=N'Interested'", conn))
                { cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId; model.SavedProgramsCount = Convert.ToInt32(cmd.ExecuteScalar()); }

                const string donationSql = @"SELECT d.DonationID,COALESCE(pay.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)),COALESCE(n.NGOName,N'General Fund'),COALESCE(p.ProgrammeName,c.CauseName),c.CauseName,d.Amount,d.DonationDate,d.DonationStatus,COALESCE(pay.CardBrand,pay.PaymentMethod,N'Dummy Payment'),COALESCE(pay.CardLastFour,N'----') FROM dbo.Donations d JOIN dbo.Causes c ON c.CauseID=d.CauseID LEFT JOIN dbo.NGOs n ON n.NGOID=d.NGOID LEFT JOIN dbo.Programmes p ON p.ProgrammeID=d.ProgrammeID LEFT JOIN dbo.Payments pay ON pay.DonationID=d.DonationID WHERE d.UserID=@id ORDER BY d.DonationDate DESC";
                using (var cmd = new SqlCommand(donationSql, conn)) { cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId; using (var r = cmd.ExecuteReader()) while (r.Read()) model.Donations.Add(new UserDonationItem { DonationID = r.GetInt32(0), PaymentReference = r.GetString(1), NGOName = r.GetString(2), ProgramName = r.GetString(3), CauseName = r.GetString(4), Amount = r.GetDecimal(5), DonationDate = r.GetDateTime(6), Status = r.GetString(7), CardType = r.GetString(8), CardLastFour = r.GetString(9) }); }

                const string interestSql = @"SELECT p.ProgrammeID,p.ProgrammeName,n.NGOName,c.CauseName,p.TargetAmount,COALESCE((SELECT SUM(d.Amount) FROM dbo.Donations d WHERE d.ProgrammeID=p.ProgrammeID AND d.DonationStatus=N'Completed'),0),p.Status,pi.CreatedAt FROM dbo.ProgrammeInterests pi JOIN dbo.Programmes p ON p.ProgrammeID=pi.ProgrammeID JOIN dbo.NGOs n ON n.NGOID=p.NGOID JOIN dbo.Causes c ON c.CauseID=p.CauseID WHERE pi.UserID=@id AND pi.Status=N'Interested' ORDER BY pi.CreatedAt DESC";
                using (var cmd = new SqlCommand(interestSql, conn)) { cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId; using (var r = cmd.ExecuteReader()) while (r.Read()) model.SavedPrograms.Add(new UserInterestItem { ProgramID = r.GetInt32(0), ProgramName = r.GetString(1), NGOName = r.GetString(2), CauseName = r.GetString(3), TargetAmount = r.GetDecimal(4), CurrentAmount = r.GetDecimal(5), Status = r.GetString(6), InterestDate = r.GetDateTime(7) }); }
            }
            return model;
        }

        public UserProfileViewModel GetUserProfile(int userId)
        {
            if (userId <= 0)
                return null;

            const string sql = @"
                SELECT UserID, FullName, Email, Phone, Gender, Profession,
                       Address, City, Country, CreatedAt, LastLoginAt
                FROM dbo.Users
                WHERE UserID = @UserID AND IsActive = 1";

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new UserProfileViewModel
                    {
                        UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                        FullName = reader.GetString(reader.GetOrdinal("FullName")),
                        Email = reader.GetString(reader.GetOrdinal("Email")),
                        Phone = ReadNullableString(reader, "Phone"),
                        Gender = ReadNullableString(reader, "Gender"),
                        Profession = ReadNullableString(reader, "Profession"),
                        Address = ReadNullableString(reader, "Address"),
                        City = ReadNullableString(reader, "City"),
                        Country = ReadNullableString(reader, "Country"),
                        MemberSince = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        LastLoginAt = reader.IsDBNull(reader.GetOrdinal("LastLoginAt"))
                            ? (DateTime?)null
                            : reader.GetDateTime(reader.GetOrdinal("LastLoginAt"))
                    };
                }
            }
        }

        public bool UpdateUserProfile(UserProfileViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (model.UserID <= 0 || string.IsNullOrWhiteSpace(model.FullName))
                return false;

            string gender = NormalizeGender(model.Gender);

            const string sql = @"
                UPDATE dbo.Users
                SET FullName = @FullName,
                    Phone = @Phone,
                    Gender = @Gender,
                    Profession = @Profession,
                    Address = @Address,
                    City = @City,
                    Country = @Country,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE UserID = @UserID AND IsActive = 1";

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150).Value = model.FullName.Trim();
                cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = NullableDbValue(model.Phone);
                cmd.Parameters.Add("@Gender", SqlDbType.NVarChar, 20).Value = NullableDbValue(gender);
                cmd.Parameters.Add("@Profession", SqlDbType.NVarChar, 120).Value = NullableDbValue(model.Profession);
                cmd.Parameters.Add("@Address", SqlDbType.NVarChar, 500).Value = NullableDbValue(model.Address);
                cmd.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = NullableDbValue(model.City);
                cmd.Parameters.Add("@Country", SqlDbType.NVarChar, 100).Value = NullableDbValue(model.Country);
                cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = model.UserID;

                conn.Open();
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public UserDonationsViewModel GetUserDonations(int userId, string search, string status)
        {
            search = (search ?? string.Empty).Trim();
            status = string.IsNullOrWhiteSpace(status)
                ? "all"
                : status.Trim().ToLowerInvariant();

            if (status != "completed" && status != "pending" && status != "cancelled")
                status = "all";

            var model = new UserDonationsViewModel
            {
                Search = search,
                Status = status
            };

            if (userId <= 0)
                return model;

            using (var conn = GetConnection())
            {
                conn.Open();

                const string totalsSql = @"
                    SELECT COUNT(*) AS TotalRecords,
                           SUM(CASE WHEN DonationStatus=N'Completed' THEN 1 ELSE 0 END) AS CompletedRecords,
                           SUM(CASE WHEN DonationStatus=N'Pending' THEN 1 ELSE 0 END) AS PendingRecords,
                           COALESCE(SUM(CASE WHEN DonationStatus=N'Completed' THEN Amount ELSE 0 END),0) AS CompletedAmount
                    FROM dbo.Donations
                    WHERE UserID=@UserID";

                using (var cmd = new SqlCommand(totalsSql, conn))
                {
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TotalRecords = reader.GetInt32(0);
                            model.CompletedRecords = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            model.PendingRecords = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                            model.CompletedAmount = reader.GetDecimal(3);
                        }
                    }
                }

                const string recordsSql = @"
                    SELECT d.DonationID,
                           COALESCE(pay.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)),
                           COALESCE(n.NGOName,N'General Fund'),
                           COALESCE(p.ProgrammeName,c.CauseName),
                           c.CauseName,
                           d.Amount,
                           d.DonationDate,
                           d.DonationStatus,
                           COALESCE(pay.CardBrand,pay.PaymentMethod,N'Dummy Payment'),
                           COALESCE(pay.CardLastFour,N'----')
                    FROM dbo.Donations d
                    JOIN dbo.Causes c ON c.CauseID=d.CauseID
                    LEFT JOIN dbo.NGOs n ON n.NGOID=d.NGOID
                    LEFT JOIN dbo.Programmes p ON p.ProgrammeID=d.ProgrammeID
                    LEFT JOIN dbo.Payments pay ON pay.DonationID=d.DonationID
                    WHERE d.UserID=@UserID
                      AND (@Status=N'all' OR LOWER(d.DonationStatus)=@Status)
                      AND (@Search=N''
                           OR COALESCE(pay.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)) LIKE N'%'+@Search+N'%'
                           OR COALESCE(n.NGOName,N'General Fund') LIKE N'%'+@Search+N'%'
                           OR COALESCE(p.ProgrammeName,c.CauseName) LIKE N'%'+@Search+N'%'
                           OR c.CauseName LIKE N'%'+@Search+N'%')
                    ORDER BY d.DonationDate DESC,d.DonationID DESC";

                using (var cmd = new SqlCommand(recordsSql, conn))
                {
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 150).Value = search;
                    cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Donations.Add(new UserDonationItem
                            {
                                DonationID = reader.GetInt32(0),
                                PaymentReference = reader.GetString(1),
                                NGOName = reader.GetString(2),
                                ProgramName = reader.GetString(3),
                                CauseName = reader.GetString(4),
                                Amount = reader.GetDecimal(5),
                                DonationDate = reader.GetDateTime(6),
                                Status = reader.GetString(7),
                                CardType = reader.GetString(8),
                                CardLastFour = reader.GetString(9)
                            });
                        }
                    }
                }
            }

            return model;
        }

        public AdminDonationsViewModel GetAdminDonations(string search, string status)
        {
            search = (search ?? string.Empty).Trim();
            status = string.IsNullOrWhiteSpace(status)
                ? "all"
                : status.Trim().ToLowerInvariant();

            if (status != "pending" &&
                status != "completed" &&
                status != "cancelled" &&
                status != "failed")
            {
                status = "all";
            }

            var model = new AdminDonationsViewModel
            {
                Search = search,
                Status = status
            };

            using (var conn = GetConnection())
            {
                conn.Open();

                const string totalsSql = @"
                    SELECT COUNT(*) AS TotalRecords,
                           SUM(CASE WHEN DonationStatus=N'Pending' THEN 1 ELSE 0 END) AS PendingRecords,
                           SUM(CASE WHEN DonationStatus=N'Completed' THEN 1 ELSE 0 END) AS CompletedRecords,
                           SUM(CASE WHEN DonationStatus=N'Cancelled' THEN 1 ELSE 0 END) AS CancelledRecords,
                           SUM(CASE WHEN DonationStatus=N'Failed' THEN 1 ELSE 0 END) AS FailedRecords,
                           COALESCE(SUM(CASE WHEN DonationStatus=N'Pending' THEN Amount ELSE 0 END),0) AS PendingAmount,
                           COALESCE(SUM(CASE WHEN DonationStatus=N'Completed' THEN Amount ELSE 0 END),0) AS CompletedAmount
                    FROM dbo.Donations;";

                using (var cmd = new SqlCommand(totalsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.TotalRecords = reader.GetInt32(0);
                        model.PendingRecords = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        model.CompletedRecords = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        model.CancelledRecords = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        model.FailedRecords = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                        model.PendingAmount = reader.GetDecimal(5);
                        model.CompletedAmount = reader.GetDecimal(6);
                    }
                }

                const string recordsSql = @"
                    SELECT d.DonationID,
                           COALESCE(pay.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)),
                           d.UserID,
                           u.FullName,
                           u.Email,
                           d.NGOID,
                           COALESCE(n.NGOName,N'General Fund'),
                           d.CauseID,
                           c.CauseName,
                           d.ProgrammeID,
                           COALESCE(pr.ProgrammeName,N'General cause fund'),
                           d.Amount,
                           d.CurrencyCode,
                           d.DonorMessage,
                           d.IsAnonymous,
                           d.DonationStatus,
                           d.DonationDate,
                           d.CompletedAt,
                           COALESCE(pay.PaymentMethod,N'Dummy Payment'),
                           COALESCE(pay.PaymentStatus,N'Pending'),
                           pay.ProcessedAt,
                           d.AdminRemarks,
                           d.AdminReviewedAt,
                           d.ReviewedByUserID,
                           reviewer.FullName
                    FROM dbo.Donations d
                    JOIN dbo.Users u ON u.UserID=d.UserID
                    JOIN dbo.Causes c ON c.CauseID=d.CauseID
                    LEFT JOIN dbo.NGOs n ON n.NGOID=d.NGOID
                    LEFT JOIN dbo.Programmes pr ON pr.ProgrammeID=d.ProgrammeID
                    LEFT JOIN dbo.Payments pay ON pay.DonationID=d.DonationID
                    LEFT JOIN dbo.Users reviewer ON reviewer.UserID=d.ReviewedByUserID
                    WHERE (@Status=N'all' OR LOWER(d.DonationStatus)=@Status)
                      AND (@Search=N''
                           OR COALESCE(pay.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)) LIKE N'%'+@Search+N'%'
                           OR u.FullName LIKE N'%'+@Search+N'%'
                           OR u.Email LIKE N'%'+@Search+N'%'
                           OR COALESCE(n.NGOName,N'General Fund') LIKE N'%'+@Search+N'%'
                           OR c.CauseName LIKE N'%'+@Search+N'%'
                           OR COALESCE(pr.ProgrammeName,N'General cause fund') LIKE N'%'+@Search+N'%')
                    ORDER BY CASE WHEN d.DonationStatus=N'Pending' THEN 0 ELSE 1 END,
                             d.DonationDate DESC,
                             d.DonationID DESC;";

                using (var cmd = new SqlCommand(recordsSql, conn))
                {
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 150).Value = search;
                    cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Donations.Add(new AdminDonationItemViewModel
                            {
                                DonationID = reader.GetInt32(0),
                                PaymentReference = reader.GetString(1),
                                UserID = reader.GetInt32(2),
                                DonorName = reader.GetString(3),
                                DonorEmail = reader.GetString(4),
                                NGOID = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                NGOName = reader.GetString(6),
                                CauseID = reader.GetInt32(7),
                                CauseName = reader.GetString(8),
                                ProgramID = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                                ProgramName = reader.GetString(10),
                                Amount = reader.GetDecimal(11),
                                CurrencyCode = reader.GetString(12),
                                DonorMessage = reader.IsDBNull(13) ? null : reader.GetString(13),
                                IsAnonymous = reader.GetBoolean(14),
                                DonationStatus = reader.GetString(15),
                                DonationDate = reader.GetDateTime(16),
                                CompletedAt = reader.IsDBNull(17) ? (DateTime?)null : reader.GetDateTime(17),
                                PaymentMethod = reader.GetString(18),
                                PaymentStatus = reader.GetString(19),
                                PaymentProcessedAt = reader.IsDBNull(20) ? (DateTime?)null : reader.GetDateTime(20),
                                AdminRemarks = reader.IsDBNull(21) ? null : reader.GetString(21),
                                AdminReviewedAt = reader.IsDBNull(22) ? (DateTime?)null : reader.GetDateTime(22),
                                ReviewedByUserID = reader.IsDBNull(23) ? (int?)null : reader.GetInt32(23),
                                ReviewedByName = reader.IsDBNull(24) ? null : reader.GetString(24)
                            });
                        }
                    }
                }
            }

            return model;
        }

        public bool ReviewDonation(
            AdminDonationDecisionViewModel model,
            int reviewerAdminId,
            out string message)
        {
            message = "The donation could not be reviewed.";

            if (model == null || model.DonationID <= 0 || reviewerAdminId <= 0)
                return false;

            string decision = (model.Decision ?? string.Empty).Trim();

            if (decision.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                bool completed = SetDonationStatus(
                    model.DonationID,
                    "Completed",
                    "Successful",
                    reviewerAdminId,
                    string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks.Trim());

                message = completed
                    ? "The donation has been completed successfully."
                    : "Only a pending donation can be completed.";
                return completed;
            }

            if (decision.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.Remarks))
                {
                    message = "Please enter a cancellation reason.";
                    return false;
                }

                bool cancelled = SetDonationStatus(
                    model.DonationID,
                    "Cancelled",
                    "Failed",
                    reviewerAdminId,
                    model.Remarks.Trim());

                message = cancelled
                    ? "The donation has been cancelled and the reason was recorded."
                    : "Only a pending donation can be cancelled.";
                return cancelled;
            }

            message = "The selected review decision is not valid.";
            return false;
        }

        public UserInterestsViewModel GetUserInterests(int userId)
        {
            var model = new UserInterestsViewModel();
            if (userId <= 0) return model;

            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT p.ProgrammeID,p.ProgrammeName,n.NGOName,c.CauseName,
                           p.TargetAmount,
                           COALESCE((SELECT SUM(d.Amount) FROM dbo.Donations d
                                     WHERE d.ProgrammeID=p.ProgrammeID
                                       AND d.DonationStatus=N'Completed'),0),
                           p.Status,pi.CreatedAt
                    FROM dbo.ProgrammeInterests pi
                    JOIN dbo.Programmes p ON p.ProgrammeID=pi.ProgrammeID
                    JOIN dbo.NGOs n ON n.NGOID=p.NGOID
                    JOIN dbo.Causes c ON c.CauseID=p.CauseID
                    WHERE pi.UserID=@UserID AND pi.Status=N'Interested'
                    ORDER BY pi.CreatedAt DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Programs.Add(new UserInterestItem
                            {
                                ProgramID = reader.GetInt32(0),
                                ProgramName = reader.GetString(1),
                                NGOName = reader.GetString(2),
                                CauseName = reader.GetString(3),
                                TargetAmount = reader.GetDecimal(4),
                                CurrentAmount = reader.GetDecimal(5),
                                Status = reader.GetString(6),
                                InterestDate = reader.GetDateTime(7)
                            });
                        }
                    }
                }
            }
            return model;
        }

        public bool SaveProgramInterest(int userId, int programId)
        {
            if (userId <= 0 || programId <= 0) return false;
            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = @"
                    IF NOT EXISTS (SELECT 1 FROM dbo.Programmes
                                   WHERE ProgrammeID=@ProgrammeID
                                     AND Status IN (N'Active',N'Upcoming'))
                        SELECT CAST(0 AS bit);
                    ELSE IF EXISTS (SELECT 1 FROM dbo.ProgrammeInterests
                                    WHERE UserID=@UserID AND ProgrammeID=@ProgrammeID)
                    BEGIN
                        UPDATE dbo.ProgrammeInterests
                           SET Status=N'Interested',CreatedAt=SYSUTCDATETIME()
                         WHERE UserID=@UserID AND ProgrammeID=@ProgrammeID;
                        SELECT CAST(1 AS bit);
                    END
                    ELSE
                    BEGIN
                        INSERT dbo.ProgrammeInterests(UserID,ProgrammeID,Status,CreatedAt)
                        VALUES(@UserID,@ProgrammeID,N'Interested',SYSUTCDATETIME());
                        SELECT CAST(1 AS bit);
                    END";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@ProgrammeID", SqlDbType.Int).Value = programId;
                    return Convert.ToBoolean(cmd.ExecuteScalar());
                }
            }
        }

        public bool RemoveProgramInterest(int userId, int programId)
        {
            if (userId <= 0 || programId <= 0) return false;
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(@"
                DELETE FROM dbo.ProgrammeInterests
                 WHERE UserID=@UserID AND ProgrammeID=@ProgrammeID", conn))
            {
                cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@ProgrammeID", SqlDbType.Int).Value = programId;
                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public AdminUsersViewModel GetAdminUsers(string search, string status)
        {
            search = (search ?? string.Empty).Trim();
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
            var model = new AdminUsersViewModel { Search = search, Status = status };

            using (var conn = GetConnection())
            {
                conn.Open();
                model.TotalUsers = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Users");
                model.ActiveUsers = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Users WHERE IsActive=1");
                model.InactiveUsers = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Users WHERE IsActive=0");
                model.AdminUsers = ScalarInt(conn, @"SELECT COUNT(DISTINCT u.UserID) FROM dbo.Users u
                    JOIN dbo.UserRoles ur ON ur.UserID=u.UserID JOIN dbo.Roles r ON r.RoleID=ur.RoleID
                    WHERE r.RoleName=N'Admin'");

                const string sql = @"
                    SELECT u.UserID,u.FullName,u.Email,u.Phone,u.City,u.Country,u.IsActive,u.CreatedAt,u.LastLoginAt,
                           COALESCE(STUFF((SELECT ', '+r2.RoleName FROM dbo.UserRoles ur2
                               JOIN dbo.Roles r2 ON r2.RoleID=ur2.RoleID WHERE ur2.UserID=u.UserID
                               ORDER BY r2.RoleName FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,2,''),N'User') AS Roles
                    FROM dbo.Users u
                    WHERE (@Search=N'' OR u.FullName LIKE N'%'+@Search+N'%' OR u.Email LIKE N'%'+@Search+N'%'
                           OR COALESCE(u.City,N'') LIKE N'%'+@Search+N'%')
                      AND (@Status=N'all' OR (@Status=N'active' AND u.IsActive=1) OR (@Status=N'inactive' AND u.IsActive=0))
                    ORDER BY u.CreatedAt DESC,u.UserID DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 150).Value = search;
                    cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Users.Add(new AdminUserListItem
                            {
                                UserID = reader.GetInt32(0),
                                FullName = reader.GetString(1),
                                Email = reader.GetString(2),
                                Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
                                City = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Country = reader.IsDBNull(5) ? null : reader.GetString(5),
                                IsActive = reader.GetBoolean(6),
                                CreatedAt = reader.GetDateTime(7),
                                LastLoginAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                Roles = reader.GetString(9)
                            });
                        }
                    }
                }
            }
            return model;
        }

        public bool SetUserActiveStatus(int userId, bool makeActive, int currentAdminId, out string message)
        {
            if (userId <= 0) { message = "Invalid user account."; return false; }
            if (userId == currentAdminId) { message = "You cannot deactivate your own administrator account."; return false; }
            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE dbo.Users SET IsActive=@Active,UpdatedAt=SYSUTCDATETIME()
                    WHERE UserID=@UserID AND NOT EXISTS
                    (SELECT 1 FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleID=ur.RoleID
                     WHERE ur.UserID=dbo.Users.UserID AND r.RoleName=N'Admin')";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@Active", SqlDbType.Bit).Value = makeActive;
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0) { message = "Administrator accounts are protected or the user was not found."; return false; }
                }
            }
            message = makeActive ? "User account activated successfully." : "User account deactivated successfully.";
            return true;
        }

        public List<LookupItem> GetActiveNGOs() => ReadLookup("SELECT NGOID,NGOName FROM dbo.NGOs WHERE IsActive=1 ORDER BY NGOName");
        public List<LookupItem> GetActiveCauses() => ReadLookup("SELECT CauseID,CauseName FROM dbo.Causes WHERE IsActive=1 ORDER BY CauseName");

        public List<LookupItem> GetActivePrograms(int? ngoId = null)
        {
            var list = new List<LookupItem>();
            using (var conn = GetConnection()) { conn.Open(); string sql = "SELECT ProgrammeID,ProgrammeName,NGOID FROM dbo.Programmes WHERE Status IN (N'Active',N'Upcoming')" + (ngoId.HasValue ? " AND NGOID=@ngo" : "") + " ORDER BY ProgrammeName"; using (var cmd = new SqlCommand(sql, conn)) { if (ngoId.HasValue) cmd.Parameters.Add("@ngo", SqlDbType.Int).Value = ngoId.Value; using (var r = cmd.ExecuteReader()) while (r.Read()) list.Add(new LookupItem { ID = r.GetInt32(0), Name = r.GetString(1), SecondaryID = r.GetInt32(2) }); } }
            return list;
        }

        public int CreateDonation(CreateDonationModel model, int? authenticatedUserId, out string paymentReference)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (!authenticatedUserId.HasValue || authenticatedUserId.Value <= 0) throw new InvalidOperationException("Please log in before making a donation.");
            paymentReference = "GA-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            using (var conn = GetConnection())
            {
                conn.Open(); using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        const string ds = @"INSERT dbo.Donations(UserID,CauseID,NGOID,ProgrammeID,Amount,DonorMessage,DonationStatus) VALUES(@u,@c,@n,@p,@a,@m,N'Pending');SELECT CAST(SCOPE_IDENTITY() AS int)"; int id; using (var cmd = new SqlCommand(ds, conn, tx)) { cmd.Parameters.Add("@u", SqlDbType.Int).Value = authenticatedUserId.Value; cmd.Parameters.Add("@c", SqlDbType.Int).Value = model.CauseID; cmd.Parameters.Add("@n", SqlDbType.Int).Value = model.NGOID > 0 ? (object)model.NGOID : DBNull.Value; cmd.Parameters.Add("@p", SqlDbType.Int).Value = model.ProgramID.HasValue ? (object)model.ProgramID.Value : DBNull.Value; cmd.Parameters.Add("@a", SqlDbType.Decimal).Value = model.Amount; cmd.Parameters.Add("@m", SqlDbType.NVarChar, 500).Value = (object)model.Message ?? DBNull.Value; id = (int)cmd.ExecuteScalar(); }
                        const string ps = @"INSERT dbo.Payments(DonationID,PaymentReference,PaymentMethod,Amount,CurrencyCode,PaymentStatus) VALUES(@d,@r,N'Dummy Payment',@a,'PKR',N'Pending')"; using (var cmd = new SqlCommand(ps, conn, tx)) { cmd.Parameters.Add("@d", SqlDbType.Int).Value = id; cmd.Parameters.Add("@r", SqlDbType.NVarChar, 100).Value = paymentReference; cmd.Parameters.Add("@a", SqlDbType.Decimal).Value = model.Amount; cmd.ExecuteNonQuery(); }
                        tx.Commit(); return id;
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        public bool ApproveDonation(int donationId, int reviewerAdminId) => SetDonationStatus(donationId, "Completed", "Successful", reviewerAdminId, null);
        public bool DenyDonation(int donationId, int reviewerAdminId, string remarks = null) => SetDonationStatus(donationId, "Cancelled", "Failed", reviewerAdminId, remarks);

        public DonationDetailViewModel GetDonationById(int donationId)
        {
            using (var conn = GetConnection()) { conn.Open(); const string sql = @"SELECT d.DonationID,COALESCE(p.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)),d.UserID,u.FullName,u.Email,d.NGOID,COALESCE(n.NGOName,N'General Fund'),d.CauseID,c.CauseName,d.ProgrammeID,COALESCE(pr.ProgrammeName,c.CauseName),d.Amount,d.DonationStatus,COALESCE(p.PaymentStatus,N'Pending'),COALESCE(p.PaymentMethod,N'Dummy Payment'),d.DonationDate,d.DonorMessage,d.AdminRemarks,d.AdminReviewedAt FROM dbo.Donations d JOIN dbo.Users u ON u.UserID=d.UserID JOIN dbo.Causes c ON c.CauseID=d.CauseID LEFT JOIN dbo.NGOs n ON n.NGOID=d.NGOID LEFT JOIN dbo.Programmes pr ON pr.ProgrammeID=d.ProgrammeID LEFT JOIN dbo.Payments p ON p.DonationID=d.DonationID WHERE d.DonationID=@id"; using (var cmd = new SqlCommand(sql, conn)) { cmd.Parameters.Add("@id", SqlDbType.Int).Value = donationId; using (var r = cmd.ExecuteReader()) if (r.Read()) return new DonationDetailViewModel { DonationID = r.GetInt32(0), PaymentReference = r.GetString(1), UserID = r.GetInt32(2), DonorName = r.GetString(3), DonorEmail = r.GetString(4), NGOID = r.IsDBNull(5) ? 0 : r.GetInt32(5), NGOName = r.GetString(6), CauseID = r.GetInt32(7), CauseName = r.GetString(8), ProgramID = r.IsDBNull(9) ? (int?)null : r.GetInt32(9), ProgramName = r.GetString(10), Amount = r.GetDecimal(11), DonationStatus = r.GetString(12), AdminApprovalStatus = r.GetString(12), NGOApprovalStatus = "Not required", PaymentStatus = r.GetString(13), PaymentMethod = r.GetString(14), DonationDate = r.GetDateTime(15), Message = r.IsDBNull(16) ? null : r.GetString(16), AdminRemarks = r.IsDBNull(17) ? null : r.GetString(17), AdminReviewedAt = r.IsDBNull(18) ? (DateTime?)null : r.GetDateTime(18) }; } }
            return null;
        }

        private bool SetDonationStatus(int id, string donationStatus, string paymentStatus, int reviewerAdminId, string remarks)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        int rows;
                        const string donationSql = @"
                            UPDATE dbo.Donations
                            SET DonationStatus=@status,
                                CompletedAt=CASE WHEN @status=N'Completed' THEN SYSUTCDATETIME() ELSE NULL END,
                                AdminRemarks=@remarks,
                                AdminReviewedAt=SYSUTCDATETIME(),
                                ReviewedByUserID=@reviewer
                            WHERE DonationID=@id
                              AND DonationStatus=N'Pending';";

                        using (var cmd = new SqlCommand(donationSql, conn, tx))
                        {
                            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value = donationStatus;
                            cmd.Parameters.Add("@remarks", SqlDbType.NVarChar, 500).Value = (object)remarks ?? DBNull.Value;
                            cmd.Parameters.Add("@reviewer", SqlDbType.Int).Value = reviewerAdminId;
                            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                            rows = cmd.ExecuteNonQuery();
                        }

                        if (rows == 0)
                        {
                            tx.Rollback();
                            return false;
                        }

                        const string paymentSql = @"
                            UPDATE dbo.Payments
                            SET PaymentStatus=@status,
                                ProcessedAt=SYSUTCDATETIME()
                            WHERE DonationID=@id;";

                        using (var cmd = new SqlCommand(paymentSql, conn, tx))
                        {
                            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value = paymentStatus;
                            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }
        private static string ReadNullableString(SqlDataReader reader, string columnName) { int ordinal = reader.GetOrdinal(columnName); return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal); }
        private static object NullableDbValue(string value) { return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim(); }
        private static string NormalizeGender(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value == "Male" || value == "Female" || value == "Other" || value == "Prefer not to say"
                ? value
                : null;
        }
        private int ScalarInt(SqlConnection c, string sql) { using (var cmd = new SqlCommand(sql, c)) return Convert.ToInt32(cmd.ExecuteScalar()); }
        private void ReadChart(SqlConnection c, string sql, List<string> labels, List<decimal> values, int li, int vi) { using (var cmd = new SqlCommand(sql, c)) using (var r = cmd.ExecuteReader()) while (r.Read()) { labels.Add(r.GetString(li)); values.Add(r.GetDecimal(vi)); } }
        private List<LookupItem> ReadLookup(string sql) { var list = new List<LookupItem>(); using (var c = GetConnection()) { c.Open(); using (var cmd = new SqlCommand(sql, c)) using (var r = cmd.ExecuteReader()) while (r.Read()) list.Add(new LookupItem { ID = r.GetInt32(0), Name = r.GetString(1) }); } return list; }
        private List<RecentDonationItem> ReadDonations(SqlConnection c, int? userId) { var list = new List<RecentDonationItem>(); string sql = @"SELECT d.DonationID,COALESCE(pay.PaymentReference,N'GA-'+CONVERT(nvarchar(20),d.DonationID)),d.UserID,u.FullName,u.Email,d.NGOID,COALESCE(n.NGOName,N'General Fund'),d.ProgrammeID,COALESCE(p.ProgrammeName,c.CauseName),d.CauseID,c.CauseName,d.Amount,d.DonationDate,d.DonationStatus,d.DonorMessage FROM dbo.Donations d JOIN dbo.Users u ON u.UserID=d.UserID JOIN dbo.Causes c ON c.CauseID=d.CauseID LEFT JOIN dbo.NGOs n ON n.NGOID=d.NGOID LEFT JOIN dbo.Programmes p ON p.ProgrammeID=d.ProgrammeID LEFT JOIN dbo.Payments pay ON pay.DonationID=d.DonationID" + (userId.HasValue ? " WHERE d.UserID=@uid" : "") + " ORDER BY d.DonationDate DESC"; using (var cmd = new SqlCommand(sql, c)) { if (userId.HasValue) cmd.Parameters.Add("@uid", SqlDbType.Int).Value = userId.Value; using (var r = cmd.ExecuteReader()) while (r.Read()) { string status = r.GetString(13); list.Add(new RecentDonationItem { DonationID = r.GetInt32(0), PaymentReference = r.GetString(1), UserID = r.GetInt32(2), DonorName = r.GetString(3), DonorEmail = r.GetString(4), NGOID = r.IsDBNull(5) ? 0 : r.GetInt32(5), NGOName = r.GetString(6), ProgramID = r.IsDBNull(7) ? (int?)null : r.GetInt32(7), ProgramName = r.GetString(8), CauseID = r.GetInt32(9), CauseName = r.GetString(10), Amount = r.GetDecimal(11), DonationDate = r.GetDateTime(12), Status = status, AdminApprovalStatus = status, NGOApprovalStatus = "Not required", Message = r.IsDBNull(14) ? null : r.GetString(14) }); } } return list; }
    }

    public class AdminUsersViewModel
    {
        public string Search { get; set; }
        public string Status { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int AdminUsers { get; set; }
        public List<AdminUserListItem> Users { get; set; } = new List<AdminUserListItem>();
    }

    public class AdminUserListItem
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Roles { get; set; }
        public bool IsAdmin => (Roles ?? "").IndexOf("Admin", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}