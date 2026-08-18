using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class AccountRepository
    {
        private readonly string _connectionString;

        public AccountRepository()
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

        public AccountRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public bool EmailExists(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            using (var conn = GetConnection())
            {
                conn.Open();
                const string query = "SELECT COUNT(1) FROM Users WHERE LOWER(Email) = LOWER(@Email)";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email.Trim());
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public bool NGONameExists(string ngoName)
        {
            if (string.IsNullOrWhiteSpace(ngoName))
                return false;

            using (var conn = GetConnection())
            {
                conn.Open();
                const string query = "SELECT COUNT(1) FROM NGOs WHERE LOWER(NGOName) = LOWER(@NGOName)";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@NGOName", ngoName.Trim());
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public int GetOrCreateRoleId(SqlConnection conn, SqlTransaction trans, string roleName = "User")
        {
            const string findQuery = "SELECT RoleID FROM Roles WHERE LOWER(RoleName) = LOWER(@RoleName)";
            using (var cmd = new SqlCommand(findQuery, conn, trans))
            {
                cmd.Parameters.AddWithValue("@RoleName", roleName.Trim());
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
            }

            // If not found, insert default role into Roles table
            const string insertQuery = "INSERT INTO Roles (RoleName) VALUES (@RoleName); SELECT SCOPE_IDENTITY();";
            using (var cmd = new SqlCommand(insertQuery, conn, trans))
            {
                cmd.Parameters.AddWithValue("@RoleName", roleName.Trim());
                var newId = cmd.ExecuteScalar();
                return Convert.ToInt32(newId);
            }
        }

        public UserAccount CreateUser(RegisterModel model, string defaultRole = "User")
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            string passwordHash = PasswordSecurity.HashPassword(model.Password);

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert into Users table
                        const string insertUserQuery = @"
                            INSERT INTO Users (FullName, Email, PasswordHash, Phone, Address, City, IsActive, IsBanned, CreatedAt)
                            VALUES (@FullName, @Email, @PasswordHash, @Phone, @Address, @City, 1, 0, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newUserId;
                        using (var cmd = new SqlCommand(insertUserQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@FullName", model.FullName.Trim());
                            cmd.Parameters.AddWithValue("@Email", model.Email.Trim().ToLowerInvariant());
                            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                            cmd.Parameters.AddWithValue("@Phone", (object)model.Phone?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Address", (object)model.Address?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@City", (object)model.City?.Trim() ?? DBNull.Value);

                            newUserId = (int)cmd.ExecuteScalar();
                        }

                        // 2. Resolve RoleID dynamically from existing Roles table
                        int roleId = GetOrCreateRoleId(conn, trans, defaultRole);

                        // 3. Assign role in UserRoles table
                        const string insertUserRoleQuery = @"
                            INSERT INTO UserRoles (UserID, RoleID, AssignedAt)
                            VALUES (@UserID, @RoleID, GETDATE());";

                        using (var cmd = new SqlCommand(insertUserRoleQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", newUserId);
                            cmd.Parameters.AddWithValue("@RoleID", roleId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();

                        return new UserAccount
                        {
                            UserID = newUserId,
                            FullName = model.FullName.Trim(),
                            Email = model.Email.Trim().ToLowerInvariant(),
                            PasswordHash = passwordHash,
                            Phone = model.Phone?.Trim(),
                            Address = model.Address?.Trim(),
                            City = model.City?.Trim(),
                            IsActive = true,
                            IsBanned = false,
                            CreatedAt = DateTime.UtcNow,
                            Roles = new List<string> { defaultRole }
                        };
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public UserAccount CreateNGOApplication(NGORegisterModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            string passwordHash = PasswordSecurity.HashPassword(model.Password);

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert into Users table with IsActive = 0 (Requires Admin Approval)
                        const string insertUserQuery = @"
                            INSERT INTO Users (FullName, Email, PasswordHash, Phone, Address, City, IsActive, IsBanned, CreatedAt)
                            VALUES (@FullName, @Email, @PasswordHash, @Phone, @Address, @City, 0, 0, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newUserId;
                        using (var cmd = new SqlCommand(insertUserQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@FullName", model.NGOName.Trim());
                            cmd.Parameters.AddWithValue("@Email", model.Email.Trim().ToLowerInvariant());
                            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                            cmd.Parameters.AddWithValue("@Phone", (object)model.Phone?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Address", (object)model.Address?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@City", (object)model.City?.Trim() ?? DBNull.Value);

                            newUserId = (int)cmd.ExecuteScalar();
                        }

                        // 2. Resolve RoleID for 'NGO'
                        int roleId = GetOrCreateRoleId(conn, trans, "NGO");

                        // 3. Assign role in UserRoles table
                        const string insertUserRoleQuery = @"
                            INSERT INTO UserRoles (UserID, RoleID, AssignedAt)
                            VALUES (@UserID, @RoleID, GETDATE());";

                        using (var cmd = new SqlCommand(insertUserRoleQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserID", newUserId);
                            cmd.Parameters.AddWithValue("@RoleID", roleId);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Combine Category & Reg Number into description if needed
                        string fullDescription = model.Description?.Trim();
                        if (!string.IsNullOrWhiteSpace(model.Category))
                        {
                            fullDescription = $"[Category: {model.Category.Trim()}] " + fullDescription;
                        }
                        if (!string.IsNullOrWhiteSpace(model.RegistrationNumber))
                        {
                            fullDescription += $" [Reg No: {model.RegistrationNumber.Trim()}]";
                        }

                        // 5. Insert into NGOs table with Status = 'Pending'
                        const string insertNgoQuery = @"
                            INSERT INTO NGOs (NGOName, Description, Address, City, Phone, Email, WebsiteURL, Status, CreatedAt)
                            VALUES (@NGOName, @Description, @Address, @City, @Phone, @Email, @WebsiteURL, 'Pending', GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newNgoId;
                        using (var cmd = new SqlCommand(insertNgoQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@NGOName", model.NGOName.Trim());
                            cmd.Parameters.AddWithValue("@Description", (object)fullDescription ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Address", (object)model.Address?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@City", (object)model.City?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Phone", (object)model.Phone?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Email", (object)model.Email?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@WebsiteURL", (object)model.WebsiteURL?.Trim() ?? DBNull.Value);

                            newNgoId = (int)cmd.ExecuteScalar();
                        }

                        // 6. Insert into NGOAccounts table linking User and NGO
                        const string insertNgoAccountQuery = @"
                            INSERT INTO NGOAccounts (NGOID, UserID, CreatedAt)
                            VALUES (@NGOID, @UserID, GETDATE());";

                        using (var cmd = new SqlCommand(insertNgoAccountQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@NGOID", newNgoId);
                            cmd.Parameters.AddWithValue("@UserID", newUserId);
                            cmd.ExecuteNonQuery();
                        }

                        // 7. Insert into NGOApplications table with ApplicationStatus = 'Pending'
                        const string insertAppQuery = @"
                            INSERT INTO NGOApplications (ApplicantUserID, NGOName, Description, Address, City, Phone, Email, ApplicationStatus, SubmittedAt)
                            VALUES (@ApplicantUserID, @NGOName, @Description, @Address, @City, @Phone, @Email, 'Pending', GETDATE());";

                        using (var cmd = new SqlCommand(insertAppQuery, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@ApplicantUserID", newUserId);
                            cmd.Parameters.AddWithValue("@NGOName", model.NGOName.Trim());
                            cmd.Parameters.AddWithValue("@Description", (object)fullDescription ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Address", (object)model.Address?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@City", (object)model.City?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Phone", (object)model.Phone?.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Email", (object)model.Email?.Trim() ?? DBNull.Value);

                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();

                        return new UserAccount
                        {
                            UserID = newUserId,
                            FullName = model.NGOName.Trim(),
                            Email = model.Email.Trim().ToLowerInvariant(),
                            PasswordHash = passwordHash,
                            Phone = model.Phone?.Trim(),
                            Address = model.Address?.Trim(),
                            City = model.City?.Trim(),
                            IsActive = false,
                            IsBanned = false,
                            CreatedAt = DateTime.UtcNow,
                            Roles = new List<string> { "NGO" }
                        };
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public NGOAccountStatusInfo GetNGOAccountStatus(int userId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                const string query = @"
                    SELECT TOP 1 
                           ISNULL(n.NGOID, 0) AS NGOID, 
                           ISNULL(n.NGOName, '') AS NGOName, 
                           ISNULL(n.Status, 'Pending') AS NGOStatus,
                           ISNULL(a.ApplicationStatus, 'Pending') AS ApplicationStatus,
                           u.IsActive AS UserIsActive,
                           a.AdminRemarks
                    FROM Users u
                    LEFT JOIN NGOAccounts na ON u.UserID = na.UserID
                    LEFT JOIN NGOs n ON na.NGOID = n.NGOID
                    LEFT JOIN NGOApplications a ON u.UserID = a.ApplicantUserID
                    WHERE u.UserID = @UserID
                    ORDER BY a.SubmittedAt DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new NGOAccountStatusInfo
                            {
                                NGOID = reader.GetInt32(0),
                                NGOName = reader.GetString(1),
                                NGOStatus = reader.GetString(2),
                                ApplicationStatus = reader.GetString(3),
                                UserIsActive = reader.GetBoolean(4),
                                AdminRemarks = reader.IsDBNull(5) ? null : reader.GetString(5)
                            };
                        }
                    }
                }
            }

            return null;
        }

        public UserAccount GetUserByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            UserAccount user = null;

            using (var conn = GetConnection())
            {
                conn.Open();
                const string query = @"
                    SELECT UserID, FullName, Email, PasswordHash, Phone, Address, City, 
                           ProfileImageURL, IsActive, IsBanned, CreatedAt, LastLoginAt
                    FROM Users
                    WHERE LOWER(Email) = LOWER(@Email)";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email.Trim());
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new UserAccount
                            {
                                UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                                FullName = reader.GetString(reader.GetOrdinal("FullName")),
                                Email = reader.GetString(reader.GetOrdinal("Email")),
                                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                                Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? null : reader.GetString(reader.GetOrdinal("Phone")),
                                Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? null : reader.GetString(reader.GetOrdinal("Address")),
                                City = reader.IsDBNull(reader.GetOrdinal("City")) ? null : reader.GetString(reader.GetOrdinal("City")),
                                ProfileImageURL = reader.IsDBNull(reader.GetOrdinal("ProfileImageURL")) ? null : reader.GetString(reader.GetOrdinal("ProfileImageURL")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                IsBanned = reader.GetBoolean(reader.GetOrdinal("IsBanned")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                LastLoginAt = reader.IsDBNull(reader.GetOrdinal("LastLoginAt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("LastLoginAt"))
                            };
                        }
                    }
                }

                if (user != null)
                {
                    user.Roles = GetUserRolesInternal(conn, user.UserID);
                }
            }

            return user;
        }

        public List<string> GetUserRoles(int userId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                return GetUserRolesInternal(conn, userId);
            }
        }

        private List<string> GetUserRolesInternal(SqlConnection conn, int userId)
        {
            var roles = new List<string>();
            const string query = @"
                SELECT r.RoleName
                FROM UserRoles ur
                INNER JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE ur.UserID = @UserID";

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        roles.Add(reader.GetString(0));
                    }
                }
            }

            return roles;
        }

        public void UpdateLastLogin(int userId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                const string query = "UPDATE Users SET LastLoginAt = GETDATE() WHERE UserID = @UserID";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
