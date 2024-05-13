using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace DDEyC_Auth.Utils
{
    public interface IAuthUtils
    {
        string GenerateJWT(Users user);
        bool ValidateJWT(string token);
        int GetUserIdFromToken(string token);
        (int userId, string token, DateTime expirationDate, DateTime creationDate) GetUserFromToken(string token);
    }

    public class AuthUtils : IAuthUtils
    {
        private readonly IConfiguration _configuration;

        public AuthUtils(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public string GenerateJWT(Users user)
        {
            var key = Encoding.ASCII.GetBytes(_configuration.GetValue<string>("Jwt:Key"));
            var expirationMinutes = _configuration.GetValue<int>("Jwt:ExpirationMinutes");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("Id", user.UserId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var stringToken = tokenHandler.WriteToken(token);

            return stringToken;
        }

        public bool ValidateJWT(string token)
        {
            var key = Encoding.ASCII.GetBytes(_configuration.GetValue<string>("Jwt:Key"));

            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                // Token inválido
                return false;
            }
        }

        public int GetUserIdFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            var userIdClaim = jwtToken?.Claims.FirstOrDefault(c => c.Type == "Id");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            throw new UnauthorizedAccessException("ID de usuario no encontrado o no válido en el token JWT.");
        }

        /// <summary>
        /// Retrieves the user ID, token, expiration date, and creation date from a JWT token.
        /// </summary>
        /// <param name="token">The JWT token.</param>
        /// <returns>A tuple containing the user ID, token, expiration date, and creation date.</returns>
        public (int userId, string token, DateTime expirationDate, DateTime creationDate) GetUserFromToken(string token)
        {
            // Initialize a JwtSecurityTokenHandler to read the token
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            // Retrieve the user ID claim from the token
            var userIdClaim = jwtToken?.Claims.FirstOrDefault(c => c.Type == "Id");

            // Retrieve the creation date claim from the token
            var creationDateClaim = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);

            // Check if the user ID claim is valid and parse it to an integer
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Retrieve the expiration date of the token
                var expirationDate = jwtToken.ValidTo;

                // Parse the creation date claim to a DateTime object
                var creationDate = DateTime.MinValue;
                if (creationDateClaim != null && long.TryParse(creationDateClaim.Value, out long unixTime))
                {
                    creationDate = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                }

                // Return a tuple containing the user ID, token, expiration date, and creation date
                return (userId, token, expirationDate, creationDate);
            }

            // Throw an exception if the user ID claim is not valid or missing
            throw new UnauthorizedAccessException("User ID not found or invalid in the JWT token.");
        }
    }
}
