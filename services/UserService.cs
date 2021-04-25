using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ComputerResetApi.Helpers;
using ComputerResetApi.Entities;

namespace ComputerResetApi.Services
{
    public interface IUserService
    {
        string generateJwtToken(UserSmall user);
        string getFbFromHeader(HttpContext context);
    }

    public class UserService : IUserService
    {
        private readonly AppSettings _appSettings;

        public UserService(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }
        
        public string generateJwtToken(UserSmall user)
        {
            // generate token that is valid for 1 day
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { 
                    new Claim("fbId", user.facebookId),
                    new Claim("firstName", user.firstName),
                    new Claim("lastName", user.lastName)
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string getFbFromHeader(HttpContext context)
        {
            User userContext = (User)context.Items["User"];
            return userContext.fbId;
        }
        
    }
}