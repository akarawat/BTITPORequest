using BTITPORequest.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTITPORequest.Controllers
{
    /// <summary>
    /// Proxy endpoint — ดึง signature image จาก bt_digitalsign
    /// เพื่อแก้ปัญหา CORS และ API key ที่ browser เรียกตรงไม่ได้
    /// </summary>
    [Authorize]
    [Route("api/signature")]
    public class SignatureController : ControllerBase
    {
        private readonly IDigitalSignService _signService;
        private readonly ILogger<SignatureController> _logger;

        public SignatureController(IDigitalSignService signService, ILogger<SignatureController> logger)
        {
            _signService = signService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/signature/image/{samAcc}
        /// คืน base64 PNG ของลายเซ็น หรือ 404 ถ้าไม่มี
        /// </summary>
        [HttpGet("image/{samAcc}")]
        public async Task<IActionResult> GetSignatureImage(string samAcc)
        {
            if (string.IsNullOrWhiteSpace(samAcc))
                return BadRequest();

            // Sanitize — รับแค่ alphanumeric + dot + underscore
            var safe = System.Text.RegularExpressions.Regex.Replace(samAcc, @"[^a-zA-Z0-9._\-]", "");
            if (string.IsNullOrEmpty(safe)) return BadRequest();

            var base64 = await _signService.GetSignatureImageAsync(safe);
            if (string.IsNullOrEmpty(base64))
                return NotFound(new { message = $"No signature found for {safe}" });

            return Ok(new { samAcc = safe, imageBase64 = base64 });
        }
    }
}
