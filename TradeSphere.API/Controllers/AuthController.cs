using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var token = await _authService.LoginAsync(model.Username, model.Password);
            if (token == null)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }
            return Ok(new { token });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            try
            {
                var token = await _authService.RegisterAsync(model.Username, model.Email, model.Password, model.Role);
                return Ok(new { token });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            var result = await _authService.ForgotPasswordAsync(model.Email);
            if (!result)
            {
                return NotFound(new { message = "Email not found" });
            }
            return Ok(new { message = "OTP sent successfully" });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto model)
        {
            var result = await _authService.VerifyOtpAsync(model.Email, model.Otp);
            if (!result)
            {
                return BadRequest(new { message = "Invalid or expired OTP" });
            }
            return Ok(new { message = "OTP verified successfully" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            var result = await _authService.ResetPasswordAsync(model.Email, model.Otp, model.NewPassword);
            if (!result)
            {
                return BadRequest(new { message = "Failed to reset password. Invalid or expired OTP" });
            }
            return Ok(new { message = "Password reset successfully" });
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthDto model)
        {
            // TODO: Implement Google authentication
            // For now, return a mock response
            return Ok(new { token = "mock-google-token-placeholder", message = "Google auth not yet implemented" });
        }
    }
}
