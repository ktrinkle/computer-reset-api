using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using ComputerResetApi.Entities;
using ComputerResetApi.Helpers;
using ComputerResetApi.Models;

namespace ComputerResetApi.Services
{
    public interface IUserService
    {
        string generateJwtToken(UserSmall user);
    }

    public class UserService : IUserService
    {
        // users hardcoded for dev purposes, need to move to secrets or db
        private List<User> _users = new List<User>
        {
            new User { Id = 1, FirstName = "Computer", LastName = "Reset", Username = "byronpcjr", Password = "IdSFaWr7*@[" }
        };

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
    }
}