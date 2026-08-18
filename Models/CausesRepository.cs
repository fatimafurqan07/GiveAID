using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class CausesRepository
    {
        private readonly string _connectionString;

        public CausesRepository()
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
        // 1. GET ALL ACTIVE CAUSES (LIST / SEARCH)
        // =========================================================
        public CauseListViewModel GetCausesList(string search = null, string category = null)
        {
            var result = new CauseListViewModel
            {
                SearchQuery = search?.Trim(),
                SelectedCategory = category?.Trim()
            };

            using (var conn = GetConnection())
            {
                conn.Open();

                string sql = @"
                    SELECT c.CauseID, c.CauseName, c.Description, c.ImageURL, c.IsActive,
                           COUNT(DISTINCT CASE WHEN n.Status = 'Active' THEN p.NGOID END) AS ActiveNGOsCount,
                           COUNT(DISTINCT CASE WHEN p.Status IN ('Active', 'Upcoming') THEN p.ProgramID END) AS ActiveProgramsCount,
                           ISNULL(SUM(CASE WHEN d.DonationStatus IN ('Approved', 'Completed') THEN d.Amount END), 0) AS TotalRaised,
                           ISNULL(SUM(p.TargetAmount), 0) AS TargetGoal
                    FROM Causes c
                    LEFT JOIN Programs p ON c.CauseID = p.CauseID
                    LEFT JOIN NGOs n ON p.NGOID = n.NGOID AND n.Status = 'Active'
                    LEFT JOIN Donations d ON c.CauseID = d.CauseID
                    WHERE c.IsActive = 1";

                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    sql += " AND (c.CauseName LIKE @Search OR c.Description LIKE @Search)";
                    parameters.Add(new SqlParameter("@Search", $"%{search.Trim()}%"));
                }

                if (!string.IsNullOrWhiteSpace(category) && category != "all")
                {
                    sql += " AND c.CauseName LIKE @Category";
                    parameters.Add(new SqlParameter("@Category", $"%{category.Trim()}%"));
                }

                sql += @" GROUP BY c.CauseID, c.CauseName, c.Description, c.ImageURL, c.IsActive
                          ORDER BY c.CauseName ASC";

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
                            var cause = new CauseListItemViewModel
                            {
                                CauseID = reader.GetInt32(0),
                                CauseName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                ImageURL = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                IsActive = reader.GetBoolean(4),
                                ActiveNGOsCount = reader.GetInt32(5),
                                ActiveProgramsCount = reader.GetInt32(6),
                                TotalRaised = reader.GetDecimal(7),
                                TargetGoal = reader.GetDecimal(8),
                                Icon = GetCauseIcon(reader.GetString(1))
                            };
                            result.Causes.Add(cause);
                        }
                    }
                }

                // Global totals
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM NGOs WHERE Status = 'Active'", conn))
                {
                    result.TotalNGOsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Programs WHERE Status = 'Active'", conn))
                {
                    result.TotalProgramsCount = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM Donations WHERE DonationStatus IN ('Approved', 'Completed')", conn))
                {
                    result.TotalFundsRaised = Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }

            return result;
        }

        // =========================================================
        // 2. GET CAUSE DETAILS BY ID
        // =========================================================
        public CauseDetailViewModel GetCauseById(int causeId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                const string causeSql = @"
                    SELECT CauseID, CauseName, Description, ImageURL, IsActive, CreatedAt
                    FROM Causes
                    WHERE CauseID = @CauseID AND IsActive = 1";

                CauseDetailViewModel cause = null;

                using (var cmd = new SqlCommand(causeSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CauseID", causeId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cause = new CauseDetailViewModel
                            {
                                CauseID = reader.GetInt32(0),
                                CauseName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                ImageURL = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                IsActive = reader.GetBoolean(4),
                                CreatedAt = reader.GetDateTime(5),
                                Icon = GetCauseIcon(reader.GetString(1))
                            };
                        }
                    }
                }

                if (cause == null)
                {
                    return null;
                }

                // 2. Verified NGOs working on this Cause (via Programs)
                const string ngosSql = @"
                    SELECT n.NGOID, n.NGOName, n.LogoURL, n.City, n.Description,
                           COUNT(DISTINCT p.ProgramID) AS ProgramsCount,
                           ISNULL(SUM(d.Amount), 0) AS TotalRaised
                    FROM NGOs n
                    INNER JOIN Programs p ON n.NGOID = p.NGOID
                    LEFT JOIN Donations d ON d.NGOID = n.NGOID AND d.CauseID = @CauseID AND d.DonationStatus IN ('Approved', 'Completed')
                    WHERE p.CauseID = @CauseID 
                      AND n.Status = 'Active'
                    GROUP BY n.NGOID, n.NGOName, n.LogoURL, n.City, n.Description
                    ORDER BY n.NGOName ASC";

                using (var cmd = new SqlCommand(ngosSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CauseID", causeId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cause.NGOs.Add(new CauseNgoItemViewModel
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                LogoURL = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                City = reader.IsDBNull(3) ? "Pakistan" : reader.GetString(3),
                                Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                IsVerified = true,
                                ProgramsCount = reader.GetInt32(5),
                                TotalRaised = reader.GetDecimal(6)
                            });
                        }
                    }
                }

                // 3. Programs associated with this Cause
                const string programsSql = @"
                    SELECT p.ProgramID, p.NGOID, n.NGOName, p.CauseID, c.CauseName, p.ProgramName, p.Description,
                           p.Location, p.StartDate, p.EndDate, p.TargetAmount, p.CurrentAmount, p.Status, p.ImageURL,
                           (SELECT COUNT(1) FROM ProgramInterests pi WHERE pi.ProgramID = p.ProgramID) AS InterestedCount
                    FROM Programs p
                    INNER JOIN NGOs n ON p.NGOID = n.NGOID
                    INNER JOIN Causes c ON p.CauseID = c.CauseID
                    WHERE p.CauseID = @CauseID AND n.Status = 'Active'
                    ORDER BY CASE WHEN p.Status = 'Active' THEN 1 WHEN p.Status = 'Upcoming' THEN 2 ELSE 3 END, p.StartDate DESC";

                using (var cmd = new SqlCommand(programsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CauseID", causeId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var prog = new NgoProgramDetailItemViewModel
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
                            };
                            cause.Programs.Add(prog);
                            cause.TotalTargetGoal += prog.TargetAmount;
                        }
                    }
                }

                // 4. Total funds raised for this Cause
                const string totalRaisedSql = @"
                    SELECT ISNULL(SUM(Amount), 0)
                    FROM Donations
                    WHERE CauseID = @CauseID AND DonationStatus IN ('Approved', 'Completed')";

                using (var cmd = new SqlCommand(totalRaisedSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CauseID", causeId);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        cause.TotalFundsRaised = Convert.ToDecimal(obj);
                    }
                }

                return cause;
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
