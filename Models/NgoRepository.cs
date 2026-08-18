using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace GiveAID_Project.Models
{
    public class NgoRepository
    {
        private readonly string _connectionString;

        public NgoRepository()
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

        // =========================================================
        // 1. GET PUBLIC NGOS (LIST / SEARCH / FILTER)
        // =========================================================
        public NgoListViewModel GetPublicNgos(string search = null, string location = null, int? causeId = null, string category = null)
        {
            var result = new NgoListViewModel
            {
                SearchQuery = search?.Trim(),
                SelectedLocation = location?.Trim(),
                SelectedCauseId = causeId,
                SelectedCategory = category?.Trim()
            };

            using (var conn = GetConnection())
            {
                conn.Open();

                // 1. Get filter dropdown options (Locations & Causes)
                using (var cmd = new SqlCommand("SELECT DISTINCT City FROM NGOs WHERE Status = 'Active' AND City IS NOT NULL AND RTRIM(LTRIM(City)) <> '' ORDER BY City ASC", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.AvailableLocations.Add(reader.GetString(0));
                    }
                }

                using (var cmd = new SqlCommand("SELECT CauseID, CauseName FROM Causes WHERE IsActive = 1 ORDER BY CauseName ASC", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.AvailableCauses.Add(new LookupItem
                        {
                            ID = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }

                // 2. Query Active & Approved NGOs
                // Strictly respect visibility: Status = 'Active' AND NOT Rejected/Denied
                string sql = @"
                    SELECT n.NGOID, n.NGOName, n.Description, n.Address, n.City, n.Phone, n.Email, n.LogoURL, n.WebsiteURL, n.Status, n.CreatedAt,
                           (SELECT COUNT(DISTINCT p.ProgramID) FROM Programs p WHERE p.NGOID = n.NGOID AND p.Status IN ('Active', 'Upcoming', 'Completed')) AS ActiveProgramsCount,
                           (SELECT COUNT(DISTINCT p.CauseID) FROM Programs p WHERE p.NGOID = n.NGOID) AS CausesCount,
                           (SELECT ISNULL(SUM(d.Amount), 0) FROM Donations d WHERE d.NGOID = n.NGOID AND d.DonationStatus IN ('Approved', 'Completed')) AS TotalRaised
                    FROM NGOs n
                    WHERE n.Status = 'Active'
                      AND (NOT EXISTS (SELECT 1 FROM NGOApplications a WHERE a.NGOName = n.NGOName) 
                           OR EXISTS (SELECT 1 FROM NGOApplications a WHERE a.NGOName = n.NGOName AND a.ApplicationStatus = 'Approved'))";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    sql += @" AND (n.NGOName LIKE @Search OR n.Description LIKE @Search OR n.City LIKE @Search 
                                   OR EXISTS (SELECT 1 FROM Programs pr WHERE pr.NGOID = n.NGOID AND pr.ProgramName LIKE @Search)
                                   OR EXISTS (SELECT 1 FROM Programs pr INNER JOIN Causes c ON pr.CauseID = c.CauseID WHERE pr.NGOID = n.NGOID AND c.CauseName LIKE @Search))";
                    parameters.Add(new SqlParameter("@Search", $"%{search.Trim()}%"));
                }

                if (!string.IsNullOrWhiteSpace(location) && location != "all")
                {
                    sql += " AND n.City = @Location";
                    parameters.Add(new SqlParameter("@Location", location.Trim()));
                }

                if (causeId.HasValue && causeId.Value > 0)
                {
                    sql += " AND EXISTS (SELECT 1 FROM Programs pr WHERE pr.NGOID = n.NGOID AND pr.CauseID = @CauseID)";
                    parameters.Add(new SqlParameter("@CauseID", causeId.Value));
                }

                if (!string.IsNullOrWhiteSpace(category) && category != "all")
                {
                    sql += @" AND (n.Description LIKE @Category 
                                   OR EXISTS (SELECT 1 FROM Programs pr INNER JOIN Causes c ON pr.CauseID = c.CauseID WHERE pr.NGOID = n.NGOID AND (c.CauseName LIKE @Category OR pr.ProgramName LIKE @Category)))";
                    parameters.Add(new SqlParameter("@Category", $"%{category.Trim()}%"));
                }

                sql += " ORDER BY n.NGOName ASC";

                var ngoList = new List<NgoListItemViewModel>();

                using (var cmd = new SqlCommand(sql, conn))
                {
                    foreach (var p in parameters)
                    {
                        cmd.Parameters.Add(p);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var ngo = new NgoListItemViewModel
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Address = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                City = reader.IsDBNull(4) ? "Pakistan" : reader.GetString(4),
                                Phone = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Email = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                LogoURL = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                WebsiteURL = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Status = reader.GetString(9),
                                IsVerified = true,
                                CreatedAt = reader.GetDateTime(10),
                                ActiveProgramsCount = reader.GetInt32(11),
                                CausesSupportedCount = reader.GetInt32(12),
                                TotalFundsRaised = reader.GetDecimal(13)
                            };

                            // Extract primary category from description if present
                            if (ngo.Description.StartsWith("[Category:"))
                            {
                                int endIdx = ngo.Description.IndexOf(']');
                                if (endIdx > 10)
                                {
                                    ngo.PrimaryCategory = ngo.Description.Substring(10, endIdx - 10).Trim();
                                }
                            }

                            ngoList.Add(ngo);
                        }
                    }
                }

                // 3. For each NGO, fetch the distinct causes supported
                foreach (var ngo in ngoList)
                {
                    const string causeSql = @"
                        SELECT DISTINCT c.CauseID, c.CauseName 
                        FROM Causes c
                        INNER JOIN Programs p ON c.CauseID = p.CauseID
                        WHERE p.NGOID = @NGOID AND c.IsActive = 1
                        ORDER BY c.CauseName ASC";

                    using (var cmd = new SqlCommand(causeSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@NGOID", ngo.NGOID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ngo.CauseIdsList.Add(reader.GetInt32(0));
                                ngo.CausesList.Add(reader.GetString(1));
                            }
                        }
                    }

                    // Fallback to primary category if no programs linked yet
                    if (ngo.CausesList.Count == 0 && !string.IsNullOrWhiteSpace(ngo.PrimaryCategory))
                    {
                        ngo.CausesList.Add(ngo.PrimaryCategory);
                    }
                }

                result.NGOs = ngoList;

                // Overall platform stats for hero banner
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM NGOs WHERE Status = 'Active'", conn))
                {
                    result.TotalVerifiedNgos = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Programs WHERE Status = 'Active'", conn))
                {
                    result.TotalActivePrograms = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM Donations WHERE DonationStatus IN ('Approved', 'Completed')", conn))
                {
                    result.TotalImpactRaised = Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }

            return result;
        }

        // =========================================================
        // 2. GET NGO DETAILS BY ID
        // =========================================================
        public NgoDetailViewModel GetNgoById(int ngoId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Check active & approved status
                const string ngoSql = @"
                    SELECT n.NGOID, n.NGOName, n.Description, n.Address, n.City, n.Phone, n.Email, n.LogoURL, n.WebsiteURL, n.Status, n.CreatedAt
                    FROM NGOs n
                    WHERE n.NGOID = @NGOID 
                      AND n.Status = 'Active'
                      AND (NOT EXISTS (SELECT 1 FROM NGOApplications a WHERE a.NGOName = n.NGOName) 
                           OR EXISTS (SELECT 1 FROM NGOApplications a WHERE a.NGOName = n.NGOName AND a.ApplicationStatus = 'Approved'))";

                NgoDetailViewModel ngo = null;

                using (var cmd = new SqlCommand(ngoSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ngo = new NgoDetailViewModel
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Address = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                City = reader.IsDBNull(4) ? "Pakistan" : reader.GetString(4),
                                Phone = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Email = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                LogoURL = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                WebsiteURL = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Status = reader.GetString(9),
                                IsVerified = true,
                                CreatedAt = reader.GetDateTime(10)
                            };
                        }
                    }
                }

                if (ngo == null)
                {
                    return null;
                }

                // 2. Total funds and donors
                const string statsSql = @"
                    SELECT ISNULL(SUM(Amount), 0), COUNT(DISTINCT DonationID), COUNT(DISTINCT UserID)
                    FROM Donations
                    WHERE NGOID = @NGOID AND DonationStatus IN ('Approved', 'Completed')";

                using (var cmd = new SqlCommand(statsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ngo.TotalFundsRaised = reader.GetDecimal(0);
                            ngo.TotalDonationsCount = reader.GetInt32(1);
                            ngo.TotalDonorsCount = reader.GetInt32(2);
                        }
                    }
                }

                // 3. Causes supported by this NGO
                const string causesSql = @"
                    SELECT c.CauseID, c.CauseName, c.Description, c.ImageURL,
                           COUNT(DISTINCT p.ProgramID) AS ProgramsCount,
                           ISNULL(SUM(d.Amount), 0) AS TotalRaised
                    FROM Causes c
                    INNER JOIN Programs p ON c.CauseID = p.CauseID
                    LEFT JOIN Donations d ON d.ProgramID = p.ProgramID AND d.DonationStatus IN ('Approved', 'Completed')
                    WHERE p.NGOID = @NGOID AND c.IsActive = 1
                    GROUP BY c.CauseID, c.CauseName, c.Description, c.ImageURL
                    ORDER BY c.CauseName ASC";

                using (var cmd = new SqlCommand(causesSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var cause = new NgoCauseItemViewModel
                            {
                                CauseID = reader.GetInt32(0),
                                CauseName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                ImageURL = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                ProgramsCount = reader.GetInt32(4),
                                TotalRaised = reader.GetDecimal(5),
                                Icon = GetCauseIcon(reader.GetString(1))
                            };
                            ngo.Causes.Add(cause);
                        }
                    }
                }

                // 4. Programs run by this NGO
                const string programsSql = @"
                    SELECT p.ProgramID, p.NGOID, n.NGOName, p.CauseID, c.CauseName, p.ProgramName, p.Description, 
                           p.Location, p.StartDate, p.EndDate, p.TargetAmount, p.CurrentAmount, p.Status, p.ImageURL,
                           (SELECT COUNT(1) FROM ProgramInterests pi WHERE pi.ProgramID = p.ProgramID) AS InterestedCount
                    FROM Programs p
                    INNER JOIN NGOs n ON p.NGOID = n.NGOID
                    INNER JOIN Causes c ON p.CauseID = c.CauseID
                    WHERE p.NGOID = @NGOID
                    ORDER BY CASE WHEN p.Status = 'Active' THEN 1 WHEN p.Status = 'Upcoming' THEN 2 ELSE 3 END, p.StartDate DESC";

                using (var cmd = new SqlCommand(programsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOID", ngoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ngo.Programs.Add(new NgoProgramDetailItemViewModel
                            {
                                ProgramID = reader.GetInt32(0),
                                NGOID = reader.GetInt32(1),
                                NGOName = reader.GetString(2),
                                CauseID = reader.GetInt32(3),
                                CauseName = reader.GetString(4),
                                ProgramName = reader.GetString(5),
                                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Location = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                StartDate = reader.GetDateTime(8),
                                EndDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                                TargetAmount = reader.GetDecimal(10),
                                CurrentAmount = reader.GetDecimal(11),
                                Status = reader.GetString(12),
                                ImageURL = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                InterestedCount = reader.GetInt32(14)
                            });
                        }
                    }
                }

                return ngo;
            }
        }

        private static string GetCauseIcon(string causeName)
        {
            if (string.IsNullOrEmpty(causeName)) return "💚";
            var lower = causeName.ToLowerInvariant();
            if (lower.Contains("water")) return "💧";
            if (lower.Contains("education") || lower.Contains("literacy") || lower.Contains("school")) return "📚";
            if (lower.Contains("health") || lower.Contains("medical") || lower.Contains("clinic")) return "🩺";
            if (lower.Contains("reforest") || lower.Contains("climate") || lower.Contains("planet") || lower.Contains("tree")) return "🌲";
            if (lower.Contains("hunger") || lower.Contains("food") || lower.Contains("meal")) return "🍲";
            if (lower.Contains("disabilit") || lower.Contains("inclusion") || lower.Contains("wheelchair")) return "♿";
            if (lower.Contains("poverty") || lower.Contains("livelihood") || lower.Contains("artisan")) return "💼";
            if (lower.Contains("emergency") || lower.Contains("relief") || lower.Contains("disaster")) return "🚨";
            return "💚";
        }
    }
}
