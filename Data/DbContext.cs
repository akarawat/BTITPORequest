using Microsoft.Data.SqlClient;
using System.Data;

namespace BTITPORequest.Data
{
    public class DbContext
    {
        private readonly IConfiguration _config;

        public DbContext(IConfiguration config)
        {
            _config = config;
        }

        public IDbConnection GetBTITReqConnection()
            => new SqlConnection(_config.GetConnectionString("BTITReq"));

        public IDbConnection GetBT_HRConnection()
            => new SqlConnection(_config.GetConnectionString("BT_HR"));
    }
}
