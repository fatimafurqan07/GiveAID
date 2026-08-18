using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class DashboardRepository
    {
        private readonly string _connectionString;

        public DashboardRepository()
        {
            var connSetting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];
            if (connSetting != null && !string.IsNullOrEmpty(connSetting.ConnectionString))
            {
                _connectionString = connSetting.ConnectionString;
            }
            else
            {
                _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GiveAID;Integrated Security=True;MultipleActiveResultSets=True;";
            }
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // ==========================================
        // 1. ADMIN DASHBOARD DATA
        // ==========================================
        public AdminDashboardViewModel GetAdminDashboardData()
        {
            var model = new AdminDashboardViewModel();

            using (var conn = GetConnection())
            {
                conn.Open();

                // 1. Metrics
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Users", conn))
                {
                    model.TotalUsers = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM NGOs WHERE Status = 'Active'", conn))
                {
                    model.TotalNGOs = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Programs WHERE Status = 'Active'", conn))
                {
                    model.TotalPrograms = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Causes WHERE IsActive = 1", conn))
                {
                    model.TotalCauses = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1), ISNULL(SUM(Amount), 0) FROM Donations WHERE DonationStatus IN ('Approved', 'Completed')", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TotalDonationsCount = reader.GetInt32(0);
                            model.TotalFundsRaised = reader.GetDecimal(1);
                        }
                    }
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM NGOApplications WHERE ApplicationStatus = 'Pending'", conn))
                {
                    model.PendingApplicationsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Donations WHERE DonationStatus = 'Pending' OR AdminApprovalStatus = 'Pending'", conn))
                {
                    model.PendingDonationsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 2. Monthly Donations Trend
                const string monthlySql = @"
                    SELECT FORMAT(DonationDate, 'MMM yyyy') AS MonthLabel, 
                           FORMAT(DonationDate, 'yyyyMM') AS MonthKey, 
                           SUM(Amount) AS TotalAmount
                    FROM Donations
                    WHERE DonationStatus IN ('Approved', 'Completed')
                    GROUP BY FORMAT(DonationDate, 'MMM yyyy'), FORMAT(DonationDate, 'yyyyMM')
                    ORDER BY MonthKey ASC";

                using (var cmd = new SqlCommand(monthlySql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.MonthlyLabels.Add(reader.GetString(0));
                        model.MonthlyAmounts.Add(reader.GetDecimal(2));
                    }
                }

                // 3. Cause Distribution
                const string causeSql = @"
                    SELECT c.CauseName, ISNULL(SUM(d.Amount), 0) AS TotalAmount
                    FROM Causes c
                    INNER JOIN Donations d ON c.CauseID = d.CauseID
                    WHERE d.DonationStatus IN ('Approved', 'Completed')
                    GROUP BY c.CauseName
                    ORDER BY TotalAmount DESC";

                using (var cmd = new SqlCommand(causeSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.CauseLabels.Add(reader.GetString(0));
                        model.CauseAmounts.Add(reader.GetDecimal(1));
                    }
                }

                // 4. Program Statuses
                const string progStatusSql = @"
                    SELECT Status, COUNT(1) AS StatusCount
                    FROM Programs
                    GROUP BY Status";

                using (var cmd = new SqlCommand(progStatusSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.ProgramStatusLabels.Add(reader.GetString(0));
                        model.ProgramStatusCounts.Add(reader.GetInt32(1));
                    }
                }

                // 5. All & Recent Donations
                const string allDonationsSql = @"
                    SELECT d.DonationID, ISNULL(p.PaymentReference, 'GA-REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
                           d.UserID, u.FullName AS DonorName, u.Email AS DonorEmail, 
                           d.NGOID, n.NGOName,
                           d.ProgramID, ISNULL(pr.ProgramName, 'General Cause Fund') AS ProgramName, 
                           d.CauseID, c.CauseName,
                           d.Amount, d.DonationDate, d.DonationStatus, d.AdminApprovalStatus, d.NGOApprovalStatus,
                           d.AdminRemarks, d.AdminReviewedAt
                    FROM Donations d
                    INNER JOIN Users u ON d.UserID = u.UserID
                    INNER JOIN NGOs n ON d.NGOID = n.NGOID
                    INNER JOIN Causes c ON d.CauseID = c.CauseID
                    LEFT JOIN Programs pr ON d.ProgramID = pr.ProgramID
                    LEFT JOIN Payments p ON d.DonationID = p.DonationID
                    ORDER BY d.DonationDate DESC";

                using (var cmd = new SqlCommand(allDonationsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new RecentDonationItem
                        {
                            DonationID = reader.GetInt32(0),
                            PaymentReference = reader.GetString(1),
                            UserID = reader.GetInt32(2),
                            DonorName = reader.GetString(3),
                            DonorEmail = reader.GetString(4),
                            NGOID = reader.GetInt32(5),
                            NGOName = reader.GetString(6),
                            ProgramID = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                            ProgramName = reader.GetString(8),
                            CauseID = reader.GetInt32(9),
                            CauseName = reader.GetString(10),
                            Amount = reader.GetDecimal(11),
                            DonationDate = reader.GetDateTime(12),
                            Status = reader.GetString(13),
                            AdminApprovalStatus = reader.GetString(14),
                            NGOApprovalStatus = reader.GetString(15),
                            AdminRemarks = reader.IsDBNull(16) ? null : reader.GetString(16),
                            AdminReviewedAt = reader.IsDBNull(17) ? (DateTime?)null : reader.GetDateTime(17)
                        };

                        model.AllDonations.Add(item);

                        if (model.RecentDonations.Count < 10)
                        {
                            model.RecentDonations.Add(item);
                        }
                    }
                }

                // 6. Recent Applications & All Applications
                const string allAppsSql = @"
                    SELECT a.ApplicationID, a.ApplicantUserID, ISNULL(na.NGOID, 0) AS NGOID,
                           a.NGOName, u.FullName AS ApplicantName, a.Email, a.Phone, a.City, a.Address,
                           a.Description, ISNULL(n.WebsiteURL, '') AS WebsiteURL,
                           a.ApplicationStatus, u.IsActive, a.SubmittedAt, a.ReviewedAt, a.AdminRemarks
                    FROM NGOApplications a
                    INNER JOIN Users u ON a.ApplicantUserID = u.UserID
                    LEFT JOIN NGOAccounts na ON u.UserID = na.UserID
                    LEFT JOIN NGOs n ON na.NGOID = n.NGOID
                    ORDER BY a.SubmittedAt DESC";

                using (var cmd = new SqlCommand(allAppsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new AdminNgoApplicationItem
                        {
                            ApplicationID = reader.GetInt32(0),
                            ApplicantUserID = reader.GetInt32(1),
                            NGOID = reader.GetInt32(2),
                            NGOName = reader.GetString(3),
                            ApplicantName = reader.GetString(4),
                            Email = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Phone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            City = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                            Address = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                            Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                            WebsiteURL = reader.IsDBNull(10) ? "" : reader.GetString(10),
                            ApplicationStatus = reader.GetString(11),
                            IsActive = reader.GetBoolean(12),
                            SubmittedAt = reader.GetDateTime(13),
                            ReviewedAt = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                            AdminRemarks = reader.IsDBNull(15) ? null : reader.GetString(15)
                        };

                        model.AllNgoApplications.Add(item);

                        if (model.RecentApplications.Count < 10)
                        {
                            model.RecentApplications.Add(new RecentApplicationItem
                            {
                                ApplicationID = item.ApplicationID,
                                NGOName = item.NGOName,
                                ApplicantName = item.ApplicantName,
                                ApplicantEmail = item.Email,
                                City = item.City,
                                Status = item.ApplicationStatus,
                                IsActive = item.IsActive,
                                SubmittedAt = item.SubmittedAt
                            });
                        }
                    }
                }

                // 7. Recent Users
                const string recentUsersSql = @"
                    SELECT TOP 5 u.UserID, u.FullName, u.Email, ISNULL(r.RoleName, 'User') AS RoleName,
                           u.CreatedAt, u.IsActive
                    FROM Users u
                    LEFT JOIN UserRoles ur ON u.UserID = ur.UserID
                    LEFT JOIN Roles r ON ur.RoleID = r.RoleID
                    ORDER BY u.CreatedAt DESC";

                using (var cmd = new SqlCommand(recentUsersSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.RecentUsers.Add(new RecentUserItem
                        {
                            UserID = reader.GetInt32(0),
                            FullName = reader.GetString(1),
                            Email = reader.GetString(2),
                            RoleName = reader.GetString(3),
                            CreatedAt = reader.GetDateTime(4),
                            IsActive = reader.GetBoolean(5)
                        });
                    }
                }
            }

            return model;
        }

        public List<AdminNgoApplicationItem> GetAllNgoApplications()
        {
            var list = new List<AdminNgoApplicationItem>();

            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT a.ApplicationID, a.ApplicantUserID, ISNULL(na.NGOID, 0) AS NGOID,
                           a.NGOName, u.FullName AS ApplicantName, a.Email, a.Phone, a.City, a.Address,
                           a.Description, ISNULL(n.WebsiteURL, '') AS WebsiteURL,
                           a.ApplicationStatus, u.IsActive, a.SubmittedAt, a.ReviewedAt, a.AdminRemarks
                    FROM NGOApplications a
                    INNER JOIN Users u ON a.ApplicantUserID = u.UserID
                    LEFT JOIN NGOAccounts na ON u.UserID = na.UserID
                    LEFT JOIN NGOs n ON na.NGOID = n.NGOID
                    ORDER BY a.SubmittedAt DESC";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AdminNgoApplicationItem
                        {
                            ApplicationID = reader.GetInt32(0),
                            ApplicantUserID = reader.GetInt32(1),
                            NGOID = reader.GetInt32(2),
                            NGOName = reader.GetString(3),
                            ApplicantName = reader.GetString(4),
                            Email = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Phone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            City = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                            Address = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                            Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                            WebsiteURL = reader.IsDBNull(10) ? "" : reader.GetString(10),
                            ApplicationStatus = reader.GetString(11),
                            IsActive = reader.GetBoolean(12),
                            SubmittedAt = reader.GetDateTime(13),
                            ReviewedAt = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                            AdminRemarks = reader.IsDBNull(15) ? null : reader.GetString(15)
                        });
                    }
                }
            }

            return list;
        }

        public bool ApproveNgoApplication(int applicationId, int reviewerUserId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        int applicantUserId = 0;
                        const string getApplicantSql = "SELECT ApplicantUserID FROM NGOApplications WHERE ApplicationID = @AppID";
                        using (var cmd = new SqlCommand(getApplicantSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@AppID", applicationId);
                            var obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value)
                            {
                                applicantUserId = Convert.ToInt32(obj);
                            }
                        }

                        if (applicantUserId == 0)
                        {
                            trans.Rollback();
                            return false;
                        }

                        const string updateAppSql = @"
                            UPDATE NGOApplications 
                            SET ApplicationStatus = 'Approved', ReviewedAt = GETDATE(), ReviewedBy = @ReviewerID 
                            WHERE ApplicationID = @AppID";

                        using (var cmd = new SqlCommand(updateAppSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@AppID", applicationId);
                            cmd.Parameters.AddWithValue("@ReviewerID", reviewerUserId > 0 ? (object)reviewerUserId : DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        const string updateUserSql = "UPDATE Users SET IsActive = 1 WHERE UserID = @UserID";
                        using (var cmd = new SqlCommand(updateUserSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", applicantUserId);
                            cmd.ExecuteNonQuery();
                        }

                        const string updateNgoSql = @"
                            UPDATE NGOs 
                            SET Status = 'Active', UpdatedAt = GETDATE() 
                            WHERE NGOID IN (SELECT NGOID FROM NGOAccounts WHERE UserID = @UserID)";

                        using (var cmd = new SqlCommand(updateNgoSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", applicantUserId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool DenyNgoApplication(int applicationId, int reviewerUserId, string remarks = null)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        int applicantUserId = 0;
                        const string getApplicantSql = "SELECT ApplicantUserID FROM NGOApplications WHERE ApplicationID = @AppID";
                        using (var cmd = new SqlCommand(getApplicantSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@AppID", applicationId);
                            var obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value)
                            {
                                applicantUserId = Convert.ToInt32(obj);
                            }
                        }

                        if (applicantUserId == 0)
                        {
                            trans.Rollback();
                            return false;
                        }

                        const string updateAppSql = @"
                            UPDATE NGOApplications 
                            SET ApplicationStatus = 'Rejected', ReviewedAt = GETDATE(), ReviewedBy = @ReviewerID, AdminRemarks = @Remarks 
                            WHERE ApplicationID = @AppID";

                        using (var cmd = new SqlCommand(updateAppSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@AppID", applicationId);
                            cmd.Parameters.AddWithValue("@ReviewerID", reviewerUserId > 0 ? (object)reviewerUserId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", (object)remarks?.Trim() ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        const string updateUserSql = "UPDATE Users SET IsActive = 0 WHERE UserID = @UserID";
                        using (var cmd = new SqlCommand(updateUserSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", applicantUserId);
                            cmd.ExecuteNonQuery();
                        }

                        const string updateNgoSql = @"
                            UPDATE NGOs 
                            SET Status = 'Inactive', UpdatedAt = GETDATE() 
                            WHERE NGOID IN (SELECT NGOID FROM NGOAccounts WHERE UserID = @UserID)";

                        using (var cmd = new SqlCommand(updateNgoSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", applicantUserId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool SetNgoActiveStatus(int applicationId, bool isActive)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        int applicantUserId = 0;
                        const string getApplicantSql = "SELECT ApplicantUserID FROM NGOApplications WHERE ApplicationID = @AppID";
                        using (var cmd = new SqlCommand(getApplicantSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@AppID", applicationId);
                            var obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value)
                            {
                                applicantUserId = Convert.ToInt32(obj);
                            }
                        }

                        if (applicantUserId == 0)
                        {
                            trans.Rollback();
                            return false;
                        }

                        const string updateUserSql = "UPDATE Users SET IsActive = @IsActive WHERE UserID = @UserID";
                        using (var cmd = new SqlCommand(updateUserSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                            cmd.Parameters.AddWithValue("@UserID", applicantUserId);
                            cmd.ExecuteNonQuery();
                        }

                        string ngoStatus = isActive ? "Active" : "Inactive";
                        const string updateNgoSql = @"
                            UPDATE NGOs 
                            SET Status = @Status, UpdatedAt = GETDATE() 
                            WHERE NGOID IN (SELECT NGOID FROM NGOAccounts WHERE UserID = @UserID)";

                        using (var cmd = new SqlCommand(updateNgoSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@Status", ngoStatus);
                            cmd.Parameters.AddWithValue("@UserID", applicantUserId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        // ==========================================
        // 2. NGO DASHBOARD DATA
        // ==========================================
        public NgoDashboardViewModel GetNgoDashboardData(int userId)
        {
            var model = new NgoDashboardViewModel();

            using (var conn = GetConnection())
            {
                conn.Open();

                // 1. Resolve NGOID for user
                int ngoId = 0;
                const string getNgoSql = @"
                    SELECT TOP 1 n.NGOID, n.NGOName, n.Email, n.Phone, n.City, n.Status
                    FROM NGOs n
                    INNER JOIN NGOAccounts na ON n.NGOID = na.NGOID
                    WHERE na.UserID = @UserID";

                using (var cmd = new SqlCommand(getNgoSql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ngoId = reader.GetInt32(0);
                            model.NGOID = ngoId;
                            model.NGOName = reader.GetString(1);
                            model.Email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            model.Phone = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            model.City = reader.IsDBNull(4) ? "" : reader.GetString(4);
                            model.Status = reader.GetString(5);
                        }
                    }
                }

                // If not found in NGOAccounts, fallback to first active NGO or user match
                if (ngoId == 0)
                {
                    const string fallbackNgoSql = "SELECT TOP 1 NGOID, NGOName, Email, Phone, City, Status FROM NGOs WHERE Status = 'Active' ORDER BY NGOID ASC";
                    using (var cmd = new SqlCommand(fallbackNgoSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ngoId = reader.GetInt32(0);
                            model.NGOID = ngoId;
                            model.NGOName = reader.GetString(1);
                            model.Email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            model.Phone = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            model.City = reader.IsDBNull(4) ? "" : reader.GetString(4);
                            model.Status = reader.GetString(5);
                        }
                    }
                }

                if (ngoId == 0)
                    return model;

                // 2. NGO Metrics
                using (var cmd = new SqlCommand("SELECT COUNT(1), ISNULL(SUM(Amount), 0), COUNT(DISTINCT UserID) FROM Donations WHERE NGOID = @NGOID AND DonationStatus IN ('Approved', 'Completed')", conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TotalDonationsCount = reader.GetInt32(0);
                            model.TotalRaised = reader.GetDecimal(1);
                            model.TotalDonorsCount = reader.GetInt32(2);
                        }
                    }
                }

                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Donations WHERE NGOID = @NGOID AND (DonationStatus = 'Pending' OR AdminApprovalStatus = 'Pending')", conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    model.PendingDonationsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Programs WHERE NGOID = @NGOID", conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    model.TotalProgramsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM ProgramInterests pi INNER JOIN Programs pr ON pi.ProgramID = pr.ProgramID WHERE pr.NGOID = @NGOID", conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    model.TotalInterestedUsersCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 3. NGO Programs
                const string progSql = @"
                    SELECT pr.ProgramID, pr.ProgramName, c.CauseName, pr.Location, pr.TargetAmount, pr.CurrentAmount, pr.Status,
                           (SELECT COUNT(1) FROM ProgramInterests pi WHERE pi.ProgramID = pr.ProgramID) AS InterestedCount
                    FROM Programs pr
                    INNER JOIN Causes c ON pr.CauseID = c.CauseID
                    WHERE pr.NGOID = @NGOID
                    ORDER BY pr.CreatedAt DESC";

                using (var cmd = new SqlCommand(progSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new NgoProgramItem
                            {
                                ProgramID = reader.GetInt32(0),
                                ProgramName = reader.GetString(1),
                                CauseName = reader.GetString(2),
                                Location = reader.IsDBNull(3) ? "Worldwide" : reader.GetString(3),
                                TargetAmount = reader.GetDecimal(4),
                                CurrentAmount = reader.GetDecimal(5),
                                Status = reader.GetString(6),
                                InterestedCount = reader.GetInt32(7)
                            };
                            model.Programs.Add(item);

                            model.ProgramNames.Add(item.ProgramName.Length > 20 ? item.ProgramName.Substring(0, 18) + "..." : item.ProgramName);
                            model.ProgramRaised.Add(item.CurrentAmount);
                            model.ProgramTargets.Add(item.TargetAmount);
                        }
                    }
                }

                // 4. Monthly Inflow
                const string monthlySql = @"
                    SELECT FORMAT(DonationDate, 'MMM yyyy') AS MonthLabel, 
                           FORMAT(DonationDate, 'yyyyMM') AS MonthKey, 
                           SUM(Amount) AS TotalAmount
                    FROM Donations
                    WHERE NGOID = @NGOID AND DonationStatus IN ('Approved', 'Completed')
                    GROUP BY FORMAT(DonationDate, 'MMM yyyy'), FORMAT(DonationDate, 'yyyyMM')
                    ORDER BY MonthKey ASC";

                using (var cmd = new SqlCommand(monthlySql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.MonthlyLabels.Add(reader.GetString(0));
                            model.MonthlyAmounts.Add(reader.GetDecimal(2));
                        }
                    }
                }

                // 5. All & Recent Donations Received for this NGO
                const string recentDonationsSql = @"
                    SELECT d.DonationID, ISNULL(p.PaymentReference, 'GA-REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
                           d.UserID, u.FullName AS DonorName, u.Email AS DonorEmail, 
                           d.NGOID, n.NGOName,
                           d.ProgramID, ISNULL(pr.ProgramName, 'General Cause Fund') AS ProgramName, 
                           d.CauseID, c.CauseName,
                           d.Amount, d.DonationDate, d.DonationStatus, d.AdminApprovalStatus, d.NGOApprovalStatus,
                           d.AdminRemarks, d.AdminReviewedAt
                    FROM Donations d
                    INNER JOIN Users u ON d.UserID = u.UserID
                    INNER JOIN NGOs n ON d.NGOID = n.NGOID
                    INNER JOIN Causes c ON d.CauseID = c.CauseID
                    LEFT JOIN Programs pr ON d.ProgramID = pr.ProgramID
                    LEFT JOIN Payments p ON d.DonationID = p.DonationID
                    WHERE d.NGOID = @NGOID
                    ORDER BY d.DonationDate DESC";

                using (var cmd = new SqlCommand(recentDonationsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new RecentDonationItem
                            {
                                DonationID = reader.GetInt32(0),
                                PaymentReference = reader.GetString(1),
                                UserID = reader.GetInt32(2),
                                DonorName = reader.GetString(3),
                                DonorEmail = reader.GetString(4),
                                NGOID = reader.GetInt32(5),
                                NGOName = reader.GetString(6),
                                ProgramID = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                ProgramName = reader.GetString(8),
                                CauseID = reader.GetInt32(9),
                                CauseName = reader.GetString(10),
                                Amount = reader.GetDecimal(11),
                                DonationDate = reader.GetDateTime(12),
                                Status = reader.GetString(13),
                                AdminApprovalStatus = reader.GetString(14),
                                NGOApprovalStatus = reader.GetString(15),
                                AdminRemarks = reader.IsDBNull(16) ? null : reader.GetString(16),
                                AdminReviewedAt = reader.IsDBNull(17) ? (DateTime?)null : reader.GetDateTime(17)
                            };

                            model.AllDonations.Add(item);

                            if (model.RecentDonations.Count < 10)
                            {
                                model.RecentDonations.Add(item);
                            }
                        }
                    }
                }

                // 6. Top Supporters
                const string supportersSql = @"
                    SELECT TOP 5 u.FullName, u.Email, SUM(d.Amount) AS TotalDonated, COUNT(d.DonationID) AS DonationsCount, MAX(d.DonationDate) AS LastDonationDate
                    FROM Donations d
                    INNER JOIN Users u ON d.UserID = u.UserID
                    WHERE d.NGOID = @NGOID AND d.DonationStatus IN ('Approved', 'Completed')
                    GROUP BY u.FullName, u.Email
                    ORDER BY TotalDonated DESC";

                using (var cmd = new SqlCommand(supportersSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.TopSupporters.Add(new NgoSupporterItem
                            {
                                SupporterName = reader.GetString(0),
                                SupporterEmail = reader.GetString(1),
                                TotalDonated = reader.GetDecimal(2),
                                DonationsCount = reader.GetInt32(3),
                                LastDonationDate = reader.GetDateTime(4)
                            });
                        }
                    }
                }
            }

            return model;
        }

        // ==========================================
        // 3. USER DASHBOARD DATA
        // ==========================================
        public UserDashboardViewModel GetUserDashboardData(int userId)
        {
            var model = new UserDashboardViewModel { UserID = userId };

            using (var conn = GetConnection())
            {
                conn.Open();

                // 1. User Info
                using (var cmd = new SqlCommand("SELECT FullName, Email, CreatedAt FROM Users WHERE UserID = @UserID", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.FullName = reader.GetString(0);
                            model.Email = reader.GetString(1);
                            model.MemberSince = reader.GetDateTime(2);
                        }
                    }
                }

                // 2. Metrics
                using (var cmd = new SqlCommand("SELECT COUNT(1), ISNULL(SUM(Amount), 0), COUNT(DISTINCT CauseID) FROM Donations WHERE UserID = @UserID AND DonationStatus IN ('Approved', 'Completed')", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TotalDonationsCount = reader.GetInt32(0);
                            model.TotalDonated = reader.GetDecimal(1);
                            model.CausesSupportedCount = reader.GetInt32(2);
                        }
                    }
                }

                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM ProgramInterests WHERE UserID = @UserID AND Status = 'Interested'", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    model.SavedProgramsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 3. Giving by Cause
                const string causeSql = @"
                    SELECT c.CauseName, SUM(d.Amount) AS TotalAmount
                    FROM Donations d
                    INNER JOIN Causes c ON d.CauseID = c.CauseID
                    WHERE d.UserID = @UserID AND d.DonationStatus IN ('Approved', 'Completed')
                    GROUP BY c.CauseName
                    ORDER BY TotalAmount DESC";

                using (var cmd = new SqlCommand(causeSql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.CauseLabels.Add(reader.GetString(0));
                            model.CauseAmounts.Add(reader.GetDecimal(1));
                        }
                    }
                }

                // 4. Monthly Giving History
                const string monthlySql = @"
                    SELECT FORMAT(DonationDate, 'MMM yyyy') AS MonthLabel, 
                           FORMAT(DonationDate, 'yyyyMM') AS MonthKey, 
                           SUM(Amount) AS TotalAmount
                    FROM Donations
                    WHERE UserID = @UserID AND DonationStatus IN ('Approved', 'Completed')
                    GROUP BY FORMAT(DonationDate, 'MMM yyyy'), FORMAT(DonationDate, 'yyyyMM')
                    ORDER BY MonthKey ASC";

                using (var cmd = new SqlCommand(monthlySql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.MonthlyLabels.Add(reader.GetString(0));
                            model.MonthlyAmounts.Add(reader.GetDecimal(2));
                        }
                    }
                }

                // 5. User Donations
                const string donationsSql = @"
                    SELECT d.DonationID, ISNULL(p.PaymentReference, 'GA-REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
                           n.NGOName, ISNULL(pr.ProgramName, c.CauseName) AS ProgramName, c.CauseName,
                           d.Amount, d.DonationDate, d.DonationStatus,
                           ISNULL(p.CardType, 'Card') AS CardType, ISNULL(p.CardLastFour, '••••') AS CardLastFour
                    FROM Donations d
                    INNER JOIN NGOs n ON d.NGOID = n.NGOID
                    INNER JOIN Causes c ON d.CauseID = c.CauseID
                    LEFT JOIN Programs pr ON d.ProgramID = pr.ProgramID
                    LEFT JOIN Payments p ON d.DonationID = p.DonationID
                    WHERE d.UserID = @UserID
                    ORDER BY d.DonationDate DESC";

                using (var cmd = new SqlCommand(donationsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
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

                // 6. Saved Programs of Interest
                const string interestsSql = @"
                    SELECT pr.ProgramID, pr.ProgramName, n.NGOName, c.CauseName,
                           pr.TargetAmount, pr.CurrentAmount, pr.Status, pi.InterestDate
                    FROM ProgramInterests pi
                    INNER JOIN Programs pr ON pi.ProgramID = pr.ProgramID
                    INNER JOIN NGOs n ON pr.NGOID = n.NGOID
                    INNER JOIN Causes c ON pr.CauseID = c.CauseID
                    WHERE pi.UserID = @UserID AND pi.Status = 'Interested'
                    ORDER BY pi.InterestDate DESC";

                using (var cmd = new SqlCommand(interestsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.SavedPrograms.Add(new UserInterestItem
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

        // ==========================================
        // 4. DONATION WORKFLOW CRUD & LOOKUPS
        // ==========================================

        public List<LookupItem> GetActiveNGOs()
        {
            var list = new List<LookupItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = "SELECT NGOID, NGOName FROM NGOs WHERE Status = 'Active' ORDER BY NGOName ASC";
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new LookupItem
                        {
                            ID = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return list;
        }

        public List<LookupItem> GetActiveCauses()
        {
            var list = new List<LookupItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = "SELECT CauseID, CauseName FROM Causes WHERE IsActive = 1 ORDER BY CauseName ASC";
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new LookupItem
                        {
                            ID = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return list;
        }

        public List<LookupItem> GetActivePrograms(int? ngoId = null)
        {
            var list = new List<LookupItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                string sql = "SELECT ProgramID, ProgramName, NGOID FROM Programs WHERE Status IN ('Active', 'Upcoming')";
                if (ngoId.HasValue && ngoId.Value > 0)
                {
                    sql += " AND NGOID = @NGOID";
                }
                sql += " ORDER BY ProgramName ASC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (ngoId.HasValue && ngoId.Value > 0)
                    {
                        cmd.Parameters.AddWithValue("@NGOID", ngoId.Value);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new LookupItem
                            {
                                ID = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                SecondaryID = reader.GetInt32(2)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public int CreateDonation(CreateDonationModel model, int? authenticatedUserId, out string paymentReference)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            paymentReference = "GA-DON-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Resolve donor user ID
                        int donorUserId = 0;
                        if (authenticatedUserId.HasValue && authenticatedUserId.Value > 0)
                        {
                            donorUserId = authenticatedUserId.Value;
                        }
                        else
                        {
                            // Look up by email
                            const string findUserSql = "SELECT UserID FROM Users WHERE LOWER(Email) = LOWER(@Email)";
                            using (var cmd = new SqlCommand(findUserSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@Email", model.DonorEmail.Trim().ToLowerInvariant());
                                var obj = cmd.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value)
                                {
                                    donorUserId = Convert.ToInt32(obj);
                                }
                            }

                            // If not found, create lightweight donor user record
                            if (donorUserId == 0)
                            {
                                const string insertUserSql = @"
                                    INSERT INTO Users (FullName, Email, PasswordHash, IsActive, IsBanned, CreatedAt)
                                    VALUES (@FullName, @Email, @PasswordHash, 1, 0, GETDATE());
                                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                using (var cmd = new SqlCommand(insertUserSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@FullName", model.DonorName.Trim());
                                    cmd.Parameters.AddWithValue("@Email", model.DonorEmail.Trim().ToLowerInvariant());
                                    cmd.Parameters.AddWithValue("@PasswordHash", PasswordSecurity.HashPassword(Guid.NewGuid().ToString("N").Substring(0, 10)));
                                    donorUserId = (int)cmd.ExecuteScalar();
                                }

                                // Assign 'User' role
                                int roleId = 0;
                                const string findRoleSql = "SELECT RoleID FROM Roles WHERE LOWER(RoleName) = 'user'";
                                using (var cmd = new SqlCommand(findRoleSql, conn, trans))
                                {
                                    var rObj = cmd.ExecuteScalar();
                                    if (rObj != null && rObj != DBNull.Value)
                                    {
                                        roleId = Convert.ToInt32(rObj);
                                    }
                                }

                                if (roleId == 0)
                                {
                                    const string createRoleSql = "INSERT INTO Roles (RoleName) VALUES ('User'); SELECT SCOPE_IDENTITY();";
                                    using (var cmd = new SqlCommand(createRoleSql, conn, trans))
                                    {
                                        roleId = Convert.ToInt32(cmd.ExecuteScalar());
                                    }
                                }

                                const string insertUserRoleSql = "INSERT INTO UserRoles (UserID, RoleID, AssignedAt) VALUES (@UserID, @RoleID, GETDATE());";
                                using (var cmd = new SqlCommand(insertUserRoleSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@UserID", donorUserId);
                                    cmd.Parameters.AddWithValue("@RoleID", roleId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // 2. Validate NGO and Cause
                        if (model.ProgramID.HasValue && model.ProgramID.Value > 0)
                        {
                            const string progMatchSql = "SELECT NGOID, CauseID FROM Programs WHERE ProgramID = @ProgramID";
                            using (var cmd = new SqlCommand(progMatchSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@ProgramID", model.ProgramID.Value);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        model.NGOID = reader.GetInt32(0);
                                        model.CauseID = reader.GetInt32(1);
                                    }
                                }
                            }
                        }

                        // 3. Insert into Donations table
                        const string insertDonationSql = @"
                            INSERT INTO Donations (UserID, NGOID, CauseID, ProgramID, Amount, AdminApprovalStatus, NGOApprovalStatus, DonationStatus, DonationDate, AdminRemarks)
                            VALUES (@UserID, @NGOID, @CauseID, @ProgramID, @Amount, 'Pending', 'Pending', 'Pending', GETDATE(), @Remarks);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int donationId;
                        using (var cmd = new SqlCommand(insertDonationSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", donorUserId);
                            cmd.Parameters.AddWithValue("@NGOID", model.NGOID);
                            cmd.Parameters.AddWithValue("@CauseID", model.CauseID);
                            cmd.Parameters.AddWithValue("@ProgramID", model.ProgramID.HasValue && model.ProgramID.Value > 0 ? (object)model.ProgramID.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Amount", model.Amount);
                            cmd.Parameters.AddWithValue("@Remarks", (object)model.Message?.Trim() ?? DBNull.Value);

                            donationId = (int)cmd.ExecuteScalar();
                        }

                        // 4. Insert into Payments table (placeholder record)
                        const string insertPaymentSql = @"
                            INSERT INTO Payments (DonationID, PaymentReference, CardType, Amount, PaymentStatus, PaymentDate)
                            VALUES (@DonationID, @PaymentReference, @CardType, @Amount, 'Pending', GETDATE());";

                        using (var cmd = new SqlCommand(insertPaymentSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@DonationID", donationId);
                            cmd.Parameters.AddWithValue("@PaymentReference", paymentReference);
                            cmd.Parameters.AddWithValue("@CardType", (object)model.PaymentRail?.Trim() ?? "Raast");
                            cmd.Parameters.AddWithValue("@Amount", model.Amount);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return donationId;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool ApproveDonation(int donationId, int reviewerAdminId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Get donation program & amount
                        int? programId = null;
                        decimal amount = 0;
                        const string getDonationSql = "SELECT ProgramID, Amount FROM Donations WHERE DonationID = @DonationID";
                        using (var cmd = new SqlCommand(getDonationSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@DonationID", donationId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    programId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                                    amount = reader.GetDecimal(1);
                                }
                                else
                                {
                                    trans.Rollback();
                                    return false;
                                }
                            }
                        }

                        // 2. Update Donation Status to 'Approved'
                        const string updateDonationSql = @"
                            UPDATE Donations 
                            SET DonationStatus = 'Approved', AdminApprovalStatus = 'Approved', AdminReviewedAt = GETDATE()
                            WHERE DonationID = @DonationID";

                        using (var cmd = new SqlCommand(updateDonationSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@DonationID", donationId);
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Update Program CurrentAmount if program is attached
                        if (programId.HasValue && programId.Value > 0)
                        {
                            const string updateProgramSql = "UPDATE Programs SET CurrentAmount = CurrentAmount + @Amount, UpdatedAt = GETDATE() WHERE ProgramID = @ProgramID";
                            using (var cmd = new SqlCommand(updateProgramSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@Amount", amount);
                                cmd.Parameters.AddWithValue("@ProgramID", programId.Value);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 4. Update Payment Status to 'Successful' placeholder
                        const string updatePaymentSql = "UPDATE Payments SET PaymentStatus = 'Successful' WHERE DonationID = @DonationID";
                        using (var cmd = new SqlCommand(updatePaymentSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@DonationID", donationId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool DenyDonation(int donationId, int reviewerAdminId, string remarks = null)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Update Donation Status to 'Denied'
                        const string updateDonationSql = @"
                            UPDATE Donations 
                            SET DonationStatus = 'Denied', AdminApprovalStatus = 'Denied', AdminReviewedAt = GETDATE(), AdminRemarks = @Remarks
                            WHERE DonationID = @DonationID";

                        using (var cmd = new SqlCommand(updateDonationSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@DonationID", donationId);
                            cmd.Parameters.AddWithValue("@Remarks", (object)remarks?.Trim() ?? "Denied by Administrator");
                            int rows = cmd.ExecuteNonQuery();
                            if (rows == 0)
                            {
                                trans.Rollback();
                                return false;
                            }
                        }

                        // 2. Update Payment Status to 'Failed'
                        const string updatePaymentSql = "UPDATE Payments SET PaymentStatus = 'Failed', FailureReason = @Reason WHERE DonationID = @DonationID";
                        using (var cmd = new SqlCommand(updatePaymentSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@DonationID", donationId);
                            cmd.Parameters.AddWithValue("@Reason", (object)remarks?.Trim() ?? "Donation denied by Platform Administrator");
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public DonationDetailViewModel GetDonationById(int donationId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT d.DonationID, ISNULL(p.PaymentReference, 'GA-REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
                           d.UserID, u.FullName AS DonorName, u.Email AS DonorEmail,
                           d.NGOID, n.NGOName,
                           d.CauseID, c.CauseName,
                           d.ProgramID, ISNULL(pr.ProgramName, 'General Fund') AS ProgramName,
                           d.Amount, d.DonationStatus, d.AdminApprovalStatus, d.NGOApprovalStatus,
                           ISNULL(p.PaymentStatus, 'Pending') AS PaymentStatus,
                           ISNULL(p.CardType, 'Raast') AS PaymentMethod,
                           d.DonationDate, d.AdminReviewedAt, d.AdminRemarks
                    FROM Donations d
                    INNER JOIN Users u ON d.UserID = u.UserID
                    INNER JOIN NGOs n ON d.NGOID = n.NGOID
                    INNER JOIN Causes c ON d.CauseID = c.CauseID
                    LEFT JOIN Programs pr ON d.ProgramID = pr.ProgramID
                    LEFT JOIN Payments p ON d.DonationID = p.DonationID
                    WHERE d.DonationID = @DonationID";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DonationID", donationId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new DonationDetailViewModel
                            {
                                DonationID = reader.GetInt32(0),
                                PaymentReference = reader.GetString(1),
                                UserID = reader.GetInt32(2),
                                DonorName = reader.GetString(3),
                                DonorEmail = reader.GetString(4),
                                NGOID = reader.GetInt32(5),
                                NGOName = reader.GetString(6),
                                CauseID = reader.GetInt32(7),
                                CauseName = reader.GetString(8),
                                ProgramID = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                                ProgramName = reader.GetString(10),
                                Amount = reader.GetDecimal(11),
                                DonationStatus = reader.GetString(12),
                                AdminApprovalStatus = reader.GetString(13),
                                NGOApprovalStatus = reader.GetString(14),
                                PaymentStatus = reader.GetString(15),
                                PaymentMethod = reader.GetString(16),
                                DonationDate = reader.GetDateTime(17),
                                AdminReviewedAt = reader.IsDBNull(18) ? (DateTime?)null : reader.GetDateTime(18),
                                AdminRemarks = reader.IsDBNull(19) ? null : reader.GetString(19),
                                Message = reader.IsDBNull(19) ? null : reader.GetString(19)
                            };
                        }
                    }
                }
            }

            return null;
        }
    }
}
