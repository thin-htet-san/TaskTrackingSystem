using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Auth;

namespace TaskTrackingSystem.WebApi.Features.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);

                if (response == null)
                {
                    // Wrap the error inside your official Result architecture
                    return Unauthorized(Result<AuthResponseDto>.Failure(ResultMessages.InvalidCredentials, 401));
                }

                // Wrap the payload inside your official Result architecture
                return Ok(Result<AuthResponseDto>.Success(response));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(Result<AuthResponseDto>.Failure(ex.Message, 400));
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var response = await _authService.RegisterAsync(registerDto);
                return Ok(Result<AuthResponseDto>.Success(response));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(Result<AuthResponseDto>.Failure(ex.Message, 400));
            }
        }
    }
}