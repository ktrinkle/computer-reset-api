using ComputerResetApi.Entities;

namespace ComputerResetApi.Models
{
    public class AuthenticateResponse
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Token { get; set; }


        public AuthenticateResponse(User user)
        {
            Id = user.FbId;
            FirstName = user.FirstName;
            LastName = user.LastName;
        }
    }
}