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
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString)) throw new ConfigurationErrorsException("GiveAIDConnection is missing.");
            _connectionString = setting.ConnectionString;
        }
        public AccountRepository(string connectionString) { _connectionString = string.IsNullOrWhiteSpace(connectionString) ? throw new ArgumentNullException(nameof(connectionString)) : connectionString; }
        private SqlConnection CreateConnection() { return new SqlConnection(_connectionString); }

        public bool EmailExists(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            using (var connection = CreateConnection())
            using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.Users WHERE Email=@Email;", connection))
            { command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = NormalizeEmail(email); connection.Open(); return Convert.ToInt32(command.ExecuteScalar()) > 0; }
        }

        public UserAccount CreateUser(RegisterModel model, string defaultRole = "User")
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        const string userSql = @"INSERT INTO dbo.Users(FullName,Email,PasswordHash,Phone,Address,City,IsActive)
VALUES(@FullName,@Email,@PasswordHash,@Phone,@Address,@City,1); SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        int userId;
                        var hash = PasswordSecurity.HashPassword(model.Password);
                        using (var command = new SqlCommand(userSql, connection, transaction))
                        {
                            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 150).Value = model.FullName.Trim();
                            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = NormalizeEmail(model.Email);
                            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = hash;
                            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = DbValue(model.Phone);
                            command.Parameters.Add("@Address", SqlDbType.NVarChar, 500).Value = DbValue(model.Address);
                            command.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = DbValue(model.City);
                            userId = (int)command.ExecuteScalar();
                        }
                        int roleId;
                        using (var command = new SqlCommand("SELECT RoleID FROM dbo.Roles WHERE RoleName=@RoleName;", connection, transaction))
                        { command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 50).Value = defaultRole; var value = command.ExecuteScalar(); if (value == null) throw new InvalidOperationException("Required role is missing: " + defaultRole); roleId = Convert.ToInt32(value); }
                        using (var command = new SqlCommand("INSERT INTO dbo.UserRoles(UserID,RoleID) VALUES(@UserID,@RoleID);", connection, transaction))
                        { command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId; command.Parameters.Add("@RoleID", SqlDbType.Int).Value = roleId; command.ExecuteNonQuery(); }
                        transaction.Commit();
                        return new UserAccount { UserID = userId, FullName = model.FullName.Trim(), Email = NormalizeEmail(model.Email), PasswordHash = hash, Phone = Clean(model.Phone), Address = Clean(model.Address), City = Clean(model.City), IsActive = true, CreatedAt = DateTime.UtcNow, Roles = new List<string> { defaultRole } };
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public UserAccount GetUserByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            const string sql = @"SELECT UserID,FullName,Email,PasswordHash,Phone,Gender,Profession,Address,City,Country,ProfileImageURL,IsActive,CreatedAt,UpdatedAt,LastLoginAt FROM dbo.Users WHERE Email=@Email;";
            using (var connection = CreateConnection()) using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = NormalizeEmail(email); connection.Open(); UserAccount user;
                using (var reader = command.ExecuteReader()) { if (!reader.Read()) return null; user = new UserAccount { UserID = reader.GetInt32(0), FullName = reader.GetString(1), Email = reader.GetString(2), PasswordHash = reader.GetString(3), Phone = Text(reader, 4), Gender = Text(reader, 5), Profession = Text(reader, 6), Address = Text(reader, 7), City = Text(reader, 8), Country = Text(reader, 9), ProfileImageURL = Text(reader, 10), IsActive = reader.GetBoolean(11), CreatedAt = reader.GetDateTime(12), UpdatedAt = Date(reader, 13), LastLoginAt = Date(reader, 14) }; }
                user.Roles = GetUserRolesInternal(connection, user.UserID); return user;
            }
        }

        public List<string> GetUserRoles(int userId) { using (var connection = CreateConnection()) { connection.Open(); return GetUserRolesInternal(connection, userId); } }
        public void UpdateLastLogin(int userId) { ExecuteUserUpdate("UPDATE dbo.Users SET LastLoginAt=SYSUTCDATETIME() WHERE UserID=@UserID;", userId, null); }
        public void UpdatePasswordHash(int userId, string hash) { ExecuteUserUpdate("UPDATE dbo.Users SET PasswordHash=@Hash,UpdatedAt=SYSUTCDATETIME() WHERE UserID=@UserID;", userId, hash); }
        private void ExecuteUserUpdate(string sql, int userId, string hash) { using (var c = CreateConnection()) using (var cmd = new SqlCommand(sql, c)) { cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId; if (hash != null) cmd.Parameters.Add("@Hash", SqlDbType.NVarChar, 500).Value = hash; c.Open(); cmd.ExecuteNonQuery(); } }
        private static List<string> GetUserRolesInternal(SqlConnection connection, int userId) { var roles = new List<string>(); using (var cmd = new SqlCommand("SELECT r.RoleName FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleID=ur.RoleID WHERE ur.UserID=@UserID ORDER BY r.RoleName;", connection)) { cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId; using (var reader = cmd.ExecuteReader()) while (reader.Read()) roles.Add(reader.GetString(0)); } return roles; }
        private static string NormalizeEmail(string value) { return value.Trim().ToLowerInvariant(); }
        private static string Clean(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
        private static object DbValue(string value) { return (object)Clean(value) ?? DBNull.Value; }
        private static string Text(SqlDataReader r, int i) { return r.IsDBNull(i) ? null : r.GetString(i); }
        private static DateTime? Date(SqlDataReader r, int i) { return r.IsDBNull(i) ? (DateTime?)null : r.GetDateTime(i); }
    }
}