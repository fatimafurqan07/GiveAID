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

                // 5. Recent Donations
                const string recentDonationsSql = @"
                    SELECT TOP 8 d.DonationID, ISNULL(p.PaymentReference, 'REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
                           u.FullName AS DonorName, u.Email AS DonorEmail, n.NGOName,
                           ISNULL(pr.ProgramName, c.CauseName) AS ProgramName, c.CauseName,
                           d.Amount, d.DonationDate, d.DonationStatus
                    FROM Donations d
                    INNER JOIN Users u ON d.UserID = u.UserID
                    INNER JOIN NGOs n ON d.NGOID = n.NGOID
                    INNER JOIN Causes c ON d.CauseID = c.CauseID
                    LEFT JOIN Programs pr ON d.ProgramID = pr.ProgramID
                    LEFT JOIN Payments p ON d.DonationID = p.DonationID
                    ORDER BY d.DonationDate DESC";

                using (var cmd = new SqlCommand(recentDonationsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.RecentDonations.Add(new RecentDonationItem
                        {
                            DonationID = reader.GetInt32(0),
                            PaymentReference = reader.GetString(1),
                            DonorName = reader.GetString(2),
                            DonorEmail = reader.GetString(3),
                            NGOName = reader.GetString(4),
                            ProgramName = reader.GetString(5),
                            CauseName = reader.GetString(6),
                            Amount = reader.GetDecimal(7),
                            DonationDate = reader.GetDateTime(8),
                            Status = reader.GetString(9)
                        });
                    }
                }

                // 6. Recent Applications
                const string recentAppsSql = @"
                    SELECT TOP 5 a.ApplicationID, a.NGOName, u.FullName AS ApplicantName, a.Email AS ApplicantEmail,
                           a.City, a.ApplicationStatus, a.SubmittedAt
                    FROM NGOApplications a
                    INNER JOIN Users u ON a.ApplicantUserID = u.UserID
                    ORDER BY a.SubmittedAt DESC";

                using (var cmd = new SqlCommand(recentAppsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.RecentApplications.Add(new RecentApplicationItem
                        {
                            ApplicationID = reader.GetInt32(0),
                            NGOName = reader.GetString(1),
                            ApplicantName = reader.GetString(2),
                            ApplicantEmail = reader.GetString(3),
                            City = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                            Status = reader.GetString(5),
                            SubmittedAt = reader.GetDateTime(6)
                        });
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

                            // Add to chart
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

                // 5. Recent Donations Received
                const string recentDonationsSql = @"
                    SELECT TOP 8 d.DonationID, ISNULL(p.PaymentReference, 'REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
                           u.FullName AS DonorName, u.Email AS DonorEmail, n.NGOName,
                           ISNULL(pr.ProgramName, c.CauseName) AS ProgramName, c.CauseName,
                           d.Amount, d.DonationDate, d.DonationStatus
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
                            model.RecentDonations.Add(new RecentDonationItem
                            {
                                DonationID = reader.GetInt32(0),
                                PaymentReference = reader.GetString(1),
                                DonorName = reader.GetString(2),
                                DonorEmail = reader.GetString(3),
                                NGOName = reader.GetString(4),
                                ProgramName = reader.GetString(5),
                                CauseName = reader.GetString(6),
                                Amount = reader.GetDecimal(7),
                                DonationDate = reader.GetDateTime(8),
                                Status = reader.GetString(9)
                            });
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
                    SELECT d.DonationID, ISNULL(p.PaymentReference, 'REF-' + CAST(d.DonationID AS VARCHAR)) AS PaymentRef,
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
    }
}
