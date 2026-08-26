using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public enum ContactSaveResult
    {
        Saved,
        RecentDuplicate
    }

    public class ContactRepository
    {
        private readonly string _connectionString;

        public ContactRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];

            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    "GiveAIDConnection is missing from Web.config.");
            }

            _connectionString = setting.ConnectionString;
        }

        /* =====================================================
           PUBLIC CONTACT FORM
           ===================================================== */

        // Kept for backward compatibility with the existing HomeController.
        public ContactSaveResult Save(ContactViewModel model)
        {
            return Save(model, null);
        }

        // Saves the signed-in UserID when one is available.
        public ContactSaveResult Save(ContactViewModel model, int? userId)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                const string duplicateSql = @"
SELECT COUNT(1)
FROM dbo.ContactMessages
WHERE Email = @Email
  AND Subject = @Subject
  AND Message = @Message
  AND CreatedAt >= DATEADD(MINUTE, -2, SYSUTCDATETIME());";

                using (var duplicateCommand = new SqlCommand(duplicateSql, connection))
                {
                    AddText(duplicateCommand, "@Email", 256, model.Email);
                    AddText(duplicateCommand, "@Subject", 200, model.Subject);
                    AddText(duplicateCommand, "@Message", 2000, model.Message);

                    if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
                    {
                        return ContactSaveResult.RecentDuplicate;
                    }
                }

                const string insertSql = @"
INSERT INTO dbo.ContactMessages
    (UserID, FullName, Email, Phone, Subject, Message, Status)
VALUES
    (@UserID, @FullName, @Email, @Phone, @Subject, @Message, N'New');";

                using (var command = new SqlCommand(insertSql, connection))
                {
                    AddNullableInt(command, "@UserID", userId);
                    AddText(command, "@FullName", 150, model.FullName);
                    AddText(command, "@Email", 256, model.Email);
                    AddNullableText(command, "@Phone", 30, model.Phone);
                    AddText(command, "@Subject", 200, model.Subject);
                    AddText(command, "@Message", 2000, model.Message);
                    command.ExecuteNonQuery();
                }
            }

            return ContactSaveResult.Saved;
        }

        /* =====================================================
           ADMIN MESSAGE LIST AND FILTERS
           ===================================================== */

        public AdminContactMessagesViewModel GetAdminMessages(
            string search = "",
            string status = "all")
        {
            search = (search ?? string.Empty).Trim();
            status = NormaliseFilterStatus(status);

            var model = new AdminContactMessagesViewModel
            {
                SearchQuery = search,
                SelectedStatus = status
            };

            const string sql = @"
SELECT
    COUNT(1) AS TotalMessages,
    SUM(CASE WHEN Status = N'New' THEN 1 ELSE 0 END) AS NewMessages,
    SUM(CASE WHEN Status = N'Read' THEN 1 ELSE 0 END) AS ReadMessages,
    SUM(CASE WHEN Status = N'Replied' THEN 1 ELSE 0 END) AS RepliedMessages,
    SUM(CASE WHEN Status = N'Closed' THEN 1 ELSE 0 END) AS ClosedMessages
FROM dbo.ContactMessages;

SELECT
    ContactMessageID,
    UserID,
    FullName,
    Email,
    Phone,
    Subject,
    CASE
        WHEN LEN(Message) > 150 THEN LEFT(Message, 150) + N'...'
        ELSE Message
    END AS MessagePreview,
    Status,
    CreatedAt,
    ResolvedAt,
    RepliedAt
FROM dbo.ContactMessages
WHERE
    (@Status = N'all' OR Status = @Status)
    AND
    (
        @Search = N''
        OR FullName LIKE N'%' + @Search + N'%'
        OR Email LIKE N'%' + @Search + N'%'
        OR Phone LIKE N'%' + @Search + N'%'
        OR Subject LIKE N'%' + @Search + N'%'
        OR Message LIKE N'%' + @Search + N'%'
        OR AdminReply LIKE N'%' + @Search + N'%'
    )
ORDER BY
    CASE WHEN Status = N'New' THEN 0 ELSE 1 END,
    CreatedAt DESC;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                AddText(command, "@Search", 300, search);
                AddText(command, "@Status", 20, status);

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.TotalMessages = ReadInt(reader, "TotalMessages");
                        model.NewMessages = ReadInt(reader, "NewMessages");
                        model.ReadMessages = ReadInt(reader, "ReadMessages");
                        model.RepliedMessages = ReadInt(reader, "RepliedMessages");
                        model.ClosedMessages = ReadInt(reader, "ClosedMessages");
                    }

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            model.Messages.Add(new AdminContactMessageListItemViewModel
                            {
                                ContactMessageID = ReadInt(reader, "ContactMessageID"),
                                UserID = ReadNullableInt(reader, "UserID"),
                                FullName = ReadString(reader, "FullName"),
                                Email = ReadString(reader, "Email"),
                                Phone = ReadString(reader, "Phone"),
                                Subject = ReadString(reader, "Subject"),
                                MessagePreview = ReadString(reader, "MessagePreview"),
                                Status = ReadString(reader, "Status"),
                                CreatedAt = ReadDateTime(reader, "CreatedAt"),
                                ResolvedAt = ReadNullableDateTime(reader, "ResolvedAt"),
                                RepliedAt = ReadNullableDateTime(reader, "RepliedAt")
                            });
                        }
                    }
                }
            }

            return model;
        }

        /* =====================================================
           ADMIN MESSAGE DETAILS
           ===================================================== */

        public AdminContactMessageDetailViewModel GetAdminMessageById(int id)
        {
            const string sql = @"
SELECT
    cm.ContactMessageID,
    cm.UserID,
    cm.FullName,
    cm.Email,
    cm.Phone,
    cm.Subject,
    cm.Message,
    cm.Status,
    cm.CreatedAt,
    cm.ResolvedAt,
    cm.AdminReply,
    cm.RepliedAt,
    cm.RepliedByUserID,
    replyingUser.FullName AS RepliedByName
FROM dbo.ContactMessages AS cm
LEFT JOIN dbo.Users AS replyingUser
    ON replyingUser.UserID = cm.RepliedByUserID
WHERE cm.ContactMessageID = @ContactMessageID;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ContactMessageID", SqlDbType.Int).Value = id;
                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new AdminContactMessageDetailViewModel
                    {
                        ContactMessageID = ReadInt(reader, "ContactMessageID"),
                        UserID = ReadNullableInt(reader, "UserID"),
                        FullName = ReadString(reader, "FullName"),
                        Email = ReadString(reader, "Email"),
                        Phone = ReadString(reader, "Phone"),
                        Subject = ReadString(reader, "Subject"),
                        Message = ReadString(reader, "Message"),
                        Status = ReadString(reader, "Status"),
                        CreatedAt = ReadDateTime(reader, "CreatedAt"),
                        ResolvedAt = ReadNullableDateTime(reader, "ResolvedAt"),
                        AdminReply = ReadString(reader, "AdminReply"),
                        RepliedAt = ReadNullableDateTime(reader, "RepliedAt"),
                        RepliedByUserID = ReadNullableInt(reader, "RepliedByUserID"),
                        RepliedByName = ReadString(reader, "RepliedByName")
                    };
                }
            }
        }

        /* =====================================================
           ADMIN REPLY WORKFLOW
           ===================================================== */

        public bool SaveAdminReply(
            int id,
            string reply,
            int adminUserId,
            out string message)
        {
            reply = (reply ?? string.Empty).Trim();

            if (id <= 0)
            {
                message = "Please select a valid contact message.";
                return false;
            }

            if (reply.Length < 3 || reply.Length > 2000)
            {
                message = "Reply must be between 3 and 2000 characters.";
                return false;
            }

            if (adminUserId <= 0)
            {
                message = "The administrator session could not be verified. Please sign in again.";
                return false;
            }

            const string sql = @"
UPDATE dbo.ContactMessages
SET
    AdminReply = @AdminReply,
    RepliedAt = SYSUTCDATETIME(),
    RepliedByUserID = @RepliedByUserID,
    Status = N'Replied',
    ResolvedAt = SYSUTCDATETIME()
WHERE ContactMessageID = @ContactMessageID;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ContactMessageID", SqlDbType.Int).Value = id;
                command.Parameters.Add("@RepliedByUserID", SqlDbType.Int).Value = adminUserId;
                AddText(command, "@AdminReply", 2000, reply);

                connection.Open();

                if (command.ExecuteNonQuery() == 0)
                {
                    message = "Contact message could not be found.";
                    return false;
                }
            }

            message = "Your reply has been saved. The registered user can now read it in My Messages.";
            return true;
        }

        /* =====================================================
           ADMIN STATUS WORKFLOW
           ===================================================== */

        public bool UpdateMessageStatus(
            int id,
            string requestedStatus,
            out string message)
        {
            var status = NormaliseWorkflowStatus(requestedStatus);

            if (status == null)
            {
                message = "The selected message status is not valid.";
                return false;
            }

            const string sql = @"
UPDATE dbo.ContactMessages
SET
    Status = @Status,
    ResolvedAt = CASE
        WHEN @Status IN (N'Replied', N'Closed') THEN COALESCE(ResolvedAt, SYSUTCDATETIME())
        ELSE NULL
    END
WHERE ContactMessageID = @ContactMessageID;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ContactMessageID", SqlDbType.Int).Value = id;
                AddText(command, "@Status", 20, status);

                connection.Open();
                var affectedRows = command.ExecuteNonQuery();

                if (affectedRows == 0)
                {
                    message = "Contact message could not be found.";
                    return false;
                }
            }

            message = "Message status has been updated to " + status + ".";
            return true;
        }

        public bool MarkAsReadIfNew(int id)
        {
            const string sql = @"
UPDATE dbo.ContactMessages
SET Status = N'Read'
WHERE ContactMessageID = @ContactMessageID
  AND Status = N'New';";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ContactMessageID", SqlDbType.Int).Value = id;
                connection.Open();
                return command.ExecuteNonQuery() > 0;
            }
        }

        /* =====================================================
           USER DASHBOARD - MY MESSAGES
           ===================================================== */

        public UserContactMessagesViewModel GetUserMessages(
            int userId,
            string search = "",
            string status = "all")
        {
            search = (search ?? string.Empty).Trim();
            status = NormaliseFilterStatus(status);

            var model = new UserContactMessagesViewModel
            {
                SearchQuery = search,
                SelectedStatus = status
            };

            if (userId <= 0)
            {
                return model;
            }

            const string sql = @"
SELECT
    COUNT(1) AS TotalMessages,
    SUM(CASE WHEN Status IN (N'New', N'Read') THEN 1 ELSE 0 END) AS AwaitingReplyCount,
    SUM(CASE WHEN Status = N'Replied' THEN 1 ELSE 0 END) AS RepliedCount,
    SUM(CASE WHEN Status = N'Closed' THEN 1 ELSE 0 END) AS ClosedCount
FROM dbo.ContactMessages
WHERE UserID = @UserID;

SELECT
    ContactMessageID,
    Subject,
    CASE
        WHEN LEN(Message) > 170 THEN LEFT(Message, 170) + N'...'
        ELSE Message
    END AS MessagePreview,
    Status,
    CreatedAt,
    RepliedAt,
    CASE
        WHEN AdminReply IS NULL OR LTRIM(RTRIM(AdminReply)) = N'' THEN CAST(0 AS bit)
        ELSE CAST(1 AS bit)
    END AS HasAdminReply
FROM dbo.ContactMessages
WHERE
    UserID = @UserID
    AND (@Status = N'all' OR Status = @Status)
    AND
    (
        @Search = N''
        OR Subject LIKE N'%' + @Search + N'%'
        OR Message LIKE N'%' + @Search + N'%'
        OR AdminReply LIKE N'%' + @Search + N'%'
    )
ORDER BY CreatedAt DESC;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                AddText(command, "@Search", 300, search);
                AddText(command, "@Status", 20, status);

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.TotalMessages = ReadInt(reader, "TotalMessages");
                        model.AwaitingReplyCount = ReadInt(reader, "AwaitingReplyCount");
                        model.RepliedCount = ReadInt(reader, "RepliedCount");
                        model.ClosedCount = ReadInt(reader, "ClosedCount");
                    }

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            model.Messages.Add(new UserContactMessageListItemViewModel
                            {
                                ContactMessageID = ReadInt(reader, "ContactMessageID"),
                                Subject = ReadString(reader, "Subject"),
                                MessagePreview = ReadString(reader, "MessagePreview"),
                                Status = ReadString(reader, "Status"),
                                CreatedAt = ReadDateTime(reader, "CreatedAt"),
                                RepliedAt = ReadNullableDateTime(reader, "RepliedAt"),
                                HasAdminReply = ReadBoolean(reader, "HasAdminReply")
                            });
                        }
                    }
                }
            }

            return model;
        }

        public UserContactMessageDetailViewModel GetUserMessageById(
            int id,
            int userId)
        {
            if (id <= 0 || userId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT
    cm.ContactMessageID,
    cm.FullName,
    cm.Email,
    cm.Phone,
    cm.Subject,
    cm.Message,
    cm.Status,
    cm.CreatedAt,
    cm.AdminReply,
    cm.RepliedAt,
    replyingUser.FullName AS RepliedByName
FROM dbo.ContactMessages AS cm
LEFT JOIN dbo.Users AS replyingUser
    ON replyingUser.UserID = cm.RepliedByUserID
WHERE cm.ContactMessageID = @ContactMessageID
  AND cm.UserID = @UserID;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ContactMessageID", SqlDbType.Int).Value = id;
                command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new UserContactMessageDetailViewModel
                    {
                        ContactMessageID = ReadInt(reader, "ContactMessageID"),
                        FullName = ReadString(reader, "FullName"),
                        Email = ReadString(reader, "Email"),
                        Phone = ReadString(reader, "Phone"),
                        Subject = ReadString(reader, "Subject"),
                        Message = ReadString(reader, "Message"),
                        Status = ReadString(reader, "Status"),
                        CreatedAt = ReadDateTime(reader, "CreatedAt"),
                        AdminReply = ReadString(reader, "AdminReply"),
                        RepliedAt = ReadNullableDateTime(reader, "RepliedAt"),
                        RepliedByName = ReadString(reader, "RepliedByName")
                    };
                }
            }
        }

        /* =====================================================
           HELPERS
           ===================================================== */

        private static string NormaliseFilterStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status) ||
                string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                return "all";
            }

            var validStatus = NormaliseWorkflowStatus(status);
            return validStatus ?? "all";
        }

        private static string NormaliseWorkflowStatus(string status)
        {
            if (string.Equals(status, "New", StringComparison.OrdinalIgnoreCase))
                return "New";

            if (string.Equals(status, "Read", StringComparison.OrdinalIgnoreCase))
                return "Read";

            if (string.Equals(status, "Replied", StringComparison.OrdinalIgnoreCase))
                return "Replied";

            if (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
                return "Closed";

            return null;
        }

        private static void AddText(
            SqlCommand command,
            string name,
            int length,
            string value)
        {
            command.Parameters.Add(name, SqlDbType.NVarChar, length).Value =
                (value ?? string.Empty).Trim();
        }

        private static void AddNullableText(
            SqlCommand command,
            string name,
            int length,
            string value)
        {
            command.Parameters.Add(name, SqlDbType.NVarChar, length).Value =
                string.IsNullOrWhiteSpace(value)
                    ? (object)DBNull.Value
                    : value.Trim();
        }

        private static void AddNullableInt(
            SqlCommand command,
            string name,
            int? value)
        {
            command.Parameters.Add(name, SqlDbType.Int).Value =
                value.HasValue && value.Value > 0
                    ? (object)value.Value
                    : DBNull.Value;
        }

        private static int ReadInt(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int? ReadNullableInt(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(value);
        }

        private static string ReadString(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value);
        }

        private static bool ReadBoolean(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value != DBNull.Value && Convert.ToBoolean(value);
        }

        private static DateTime ReadDateTime(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(value);
        }

        private static DateTime? ReadNullableDateTime(
            IDataRecord record,
            string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value
                ? (DateTime?)null
                : Convert.ToDateTime(value);
        }
    }
}
