using System.Data.SqlClient;

namespace Test
{
    public class UserRepository
    {
        private SqlConnection _connection;
        
        public void GetUser(string userId)
        {
            string query = "SELECT * FROM Users WHERE Id = " + userId;
            SqlCommand cmd = new SqlCommand(query, _connection);
            cmd.ExecuteReader();
        }
    }
}
