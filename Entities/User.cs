using System.Text.Json.Serialization;

namespace ComputerResetApi.Entities
{
    public class User
    {
        public int fbId { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
    }
}