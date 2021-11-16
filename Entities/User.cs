using System.Text.Json.Serialization;

namespace ComputerResetApi.Entities
{
    public class User
    {
        public string FbId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}