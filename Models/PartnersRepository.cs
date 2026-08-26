using System;
using System.Configuration;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class PartnersRepository
    {
        private readonly string _connectionString;

        public PartnersRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
                throw new ConfigurationErrorsException("GiveAIDConnection is missing from Web.config.");
            _connectionString = setting.ConnectionString;
        }

        public PartnersPageViewModel GetActivePartners()
        {
            var model = new PartnersPageViewModel();
            const string sql = @"
SELECT PartnerID, PartnerName, Description, LogoURL, WebsiteURL, DisplayOrder, CreatedAt
FROM dbo.Partners
WHERE IsActive = 1
ORDER BY DisplayOrder, PartnerName;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.Partners.Add(new PartnerListItemViewModel
                        {
                            PartnerID = reader.GetInt32(0),
                            PartnerName = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            LogoURL = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            WebsiteURL = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            DisplayOrder = reader.GetInt32(5),
                            CreatedAt = reader.GetDateTime(6)
                        });
                    }
                }
            }

            model.ActivePartnerCount = model.Partners.Count;
            return model;
        }
    }
}
