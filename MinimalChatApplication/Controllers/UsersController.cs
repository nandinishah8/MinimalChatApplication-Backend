﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimalChatApplication.Data;
using MinimalChatApplication.Models;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace MinimalChatApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly MinimalChatContext _context;
        IConfiguration _configuration;

        public UsersController(MinimalChatContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: api/Users
        [HttpGet]
        [Authorize]
       

        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            var currentUser = HttpContext.User;
            var userId = currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = currentUser.FindFirst(ClaimTypes.Name)?.Value;
            var userEmail = currentUser.FindFirst(ClaimTypes.Email)?.Value;
            await Console.Out.WriteLineAsync(userId);

            if (!HttpContext.User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { message = "Unauthorized access" });
            }
            var users = await _context.Users.Where(u => u.Id != Convert.ToInt32(userId))
                .Select(u => new User
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Password = u.Password

                })
                .ToListAsync();

            return Ok(users);
        }
    
    

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            if (_context.Users == null)
            {
                return NotFound();
            }
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return user;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }


        // POST: api/register
        [HttpPost("/api/register")]
        public async Task<ActionResult<User>> Register(User model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Invalid request data." });
            }

            // Check if the email is already registered
            if (_context.Users.Any(u => u.Email == model.Email))
            {
                return Conflict(new { error = "Email is already registered." });
            }

            // Create a new User object from the request model
            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                Password = HashPassword(model.Password)
            };

            // Add the user to the database
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Return the success response with the user information
            return Ok(user);
        }



        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (_context.Users == null)
            {
                return NotFound();
            }
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool UserExists(int id)
        {
            return (_context.Users?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        // Hash the password using a suitable hashing algorithm (e.g., bcrypt)
        private string HashPassword(string password)
        {

            // Generate a random salt
            string salt = BCrypt.Net.BCrypt.GenerateSalt();

            // Hash the password with the salt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, salt);

            return hashedPassword;
        }

        // POST: api/login
        [HttpPost("/api/login")]
        public async Task<ActionResult<User>> Login(loginRequest loginData)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Invalid request data." });
            }

            // Find the user by email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginData.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginData.Password, user.Password))
            {
                return Unauthorized(new { error = "Incorrect email or password." });
            }

            // Login successful, generate JWT token and return user profile
            var profile = new { user.Id, user.Name, user.Email };
            var token = GenerateJwtToken(user);

            user.Token = token;
            await _context.SaveChangesAsync();

            return Ok(user);
        }
        private int GetUserId(HttpContext context)
        {
            var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            var token = authorizationHeader?.Replace("Bearer ", "");

            var user = _context.Users.FirstOrDefault(u => u.Token == token);

            return user?.Id ?? -1;
        } 




        // Helper method to generate JWT token
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: signIn) ;


            string Token = new JwtSecurityTokenHandler().WriteToken(token);

            return Token;
        }
    }
}
