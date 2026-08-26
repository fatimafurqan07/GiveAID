using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace GiveAID_Project.Models
{
    public class HomePageViewModel
    {
        public int ActiveUsers { get; set; }
        public int ActiveNgos { get; set; }
        public int ActiveCauses { get; set; }
        public int ActiveProgrammes { get; set; }
        public int CompletedDonations { get; set; }
        public decimal FundsRaised { get; set; }
        public List<HomeCauseItem> Causes { get; set; } = new List<HomeCauseItem>();
        public List<HomeProgrammeItem> Programmes { get; set; } = new List<HomeProgrammeItem>();
        public List<HomeNgoItem> Ngos { get; set; } = new List<HomeNgoItem>();
    }

    public class HomeCauseItem
    {
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string Slug { get; set; }
        public string ShortDescription { get; set; }
        public int ProgrammeCount { get; set; }
    }

    public class HomeProgrammeItem
    {
        public int ProgrammeID { get; set; }
        public string ProgrammeName { get; set; }
        public string ShortDescription { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal RaisedAmount { get; set; }
        public string NgoName { get; set; }
        public string CauseName { get; set; }
        public int ProgressPercent => TargetAmount <= 0 ? 0 : (int)Math.Min(100, Math.Round((RaisedAmount / TargetAmount) * 100));
    }

    public class HomeNgoItem
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string Category { get; set; }
        public string City { get; set; }
        public string Description { get; set; }
        public int ProgrammeCount { get; set; }
    }

    public class HomePageRepository
    {
        private readonly string _connectionString;
        public HomePageRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["GiveAIDConnection"];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
                throw new ConfigurationErrorsException("GiveAIDConnection is missing from Web.config.");
            _connectionString = setting.ConnectionString;
        }

        public HomePageViewModel GetHomePage()
        {
            var model = new HomePageViewModel();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                model.ActiveUsers = Count(conn, "SELECT COUNT(*) FROM dbo.Users WHERE IsActive=1");
                model.ActiveNgos = Count(conn, "SELECT COUNT(*) FROM dbo.NGOs WHERE IsActive=1");
                model.ActiveCauses = Count(conn, "SELECT COUNT(*) FROM dbo.Causes WHERE IsActive=1");
                model.ActiveProgrammes = Count(conn, "SELECT COUNT(*) FROM dbo.Programmes WHERE Status IN (N'Active',N'Upcoming')");
                using (var cmd = new SqlCommand("SELECT COUNT(*),COALESCE(SUM(Amount),0) FROM dbo.Donations WHERE DonationStatus=N'Completed'", conn))
                using (var reader = cmd.ExecuteReader())
                    if (reader.Read()) { model.CompletedDonations = reader.GetInt32(0); model.FundsRaised = reader.GetDecimal(1); }

                const string causesSql = @"SELECT TOP 6 c.CauseID,c.CauseName,c.Slug,COALESCE(c.ShortDescription,c.Description,N''),
                    (SELECT COUNT(*) FROM dbo.Programmes p WHERE p.CauseID=c.CauseID AND p.Status IN(N'Active',N'Upcoming'))
                    FROM dbo.Causes c WHERE c.IsActive=1 ORDER BY c.IsFeatured DESC,c.DisplayOrder,c.CauseName";
                using (var cmd = new SqlCommand(causesSql, conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) model.Causes.Add(new HomeCauseItem { CauseID=reader.GetInt32(0),CauseName=reader.GetString(1),Slug=reader.GetString(2),ShortDescription=reader.GetString(3),ProgrammeCount=reader.GetInt32(4) });

                const string programmesSql = @"SELECT TOP 3 p.ProgrammeID,p.ProgrammeName,COALESCE(p.ShortDescription,p.Description,N''),COALESCE(p.Location,N'Pakistan'),p.Status,p.TargetAmount,
                    COALESCE((SELECT SUM(d.Amount) FROM dbo.Donations d WHERE d.ProgrammeID=p.ProgrammeID AND d.DonationStatus=N'Completed'),0),n.NGOName,c.CauseName
                    FROM dbo.Programmes p JOIN dbo.NGOs n ON n.NGOID=p.NGOID JOIN dbo.Causes c ON c.CauseID=p.CauseID
                    WHERE n.IsActive=1 AND c.IsActive=1 AND p.Status IN(N'Active',N'Upcoming')
                    ORDER BY p.IsFeatured DESC,CASE WHEN p.Status=N'Active' THEN 0 ELSE 1 END,p.StartDate";
                using (var cmd = new SqlCommand(programmesSql, conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) model.Programmes.Add(new HomeProgrammeItem { ProgrammeID=reader.GetInt32(0),ProgrammeName=reader.GetString(1),ShortDescription=reader.GetString(2),Location=reader.GetString(3),Status=reader.GetString(4),TargetAmount=reader.GetDecimal(5),RaisedAmount=reader.GetDecimal(6),NgoName=reader.GetString(7),CauseName=reader.GetString(8) });

                const string ngosSql = @"SELECT TOP 3 n.NGOID,n.NGOName,COALESCE(n.Category,N'Community Support'),COALESCE(n.City,N'Pakistan'),COALESCE(n.Description,N''),
                    (SELECT COUNT(*) FROM dbo.Programmes p WHERE p.NGOID=n.NGOID AND p.Status IN(N'Active',N'Upcoming'))
                    FROM dbo.NGOs n WHERE n.IsActive=1 ORDER BY n.NGOName";
                using (var cmd = new SqlCommand(ngosSql, conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) model.Ngos.Add(new HomeNgoItem { NGOID=reader.GetInt32(0),NGOName=reader.GetString(1),Category=reader.GetString(2),City=reader.GetString(3),Description=reader.GetString(4),ProgrammeCount=reader.GetInt32(5) });
            }
            return model;
        }

        private static int Count(SqlConnection connection, string sql)
        {
            using (var cmd = new SqlCommand(sql, connection)) return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
