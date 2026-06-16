using BTITPORequest.Data;
using BTITPORequest.Helpers;
using BTITPORequest.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTITPORequest.Controllers
{
    [Authorize]
    [Route("api/attachment")]
    public class AttachmentController : ControllerBase
    {
        private readonly DbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AttachmentController> _logger;

        // Allowed file types
        private static readonly string[] AllowedTypes =
            { "application/pdf", "image/jpeg", "image/jpg", "image/png" };
        private static readonly string[] AllowedExt =
            { ".pdf", ".jpg", ".jpeg", ".png" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        // Upload folder under wwwroot/uploads
        private string UploadRoot => System.IO.Path.Combine(
            Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        public AttachmentController(DbContext db, IConfiguration config,
            ILogger<AttachmentController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        // ── GET LIST ─────────────────────────────────────────
        [HttpGet("{poId:int}")]
        public async Task<IActionResult> GetList(int poId)
        {
            using var conn = _db.GetBTITReqConnection();
            var list = await conn.QueryAsync<POAttachmentModel>(
                "ITPO_sp_GetAttachments", new { POId = poId },
                commandType: System.Data.CommandType.StoredProcedure);
            return Ok(list);
        }

        // ── UPLOAD ───────────────────────────────────────────
        [HttpPost("upload/{poId:int}")]
        public async Task<IActionResult> Upload(int poId, IFormFile file)
        {
            var user = SessionHelper.GetUser(HttpContext.Session)
                    ?? SessionHelper.GetUserFromClaims(User);

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file selected" });

            if (file.Length > MaxFileSize)
                return BadRequest(new { error = "File too large (max 10 MB)" });

            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (!AllowedExt.Contains(ext))
                return BadRequest(new { error = "Only PDF, JPG, PNG allowed" });

            var contentType = file.ContentType.ToLower();
            if (!AllowedTypes.Any(t => contentType.Contains(t.Split('/')[1])))
                contentType = ext == ".pdf" ? "application/pdf"
                            : ext == ".png"  ? "image/png"
                            :                  "image/jpeg";

            try
            {
                // Create subfolder per PO: uploads/po_{poId}/
                var subDir = System.IO.Path.Combine(UploadRoot, $"po_{poId}");
                Directory.CreateDirectory(subDir);

                // Stored filename = GUID + ext (prevent path traversal)
                var storedName = $"{Guid.NewGuid():N}{ext}";
                var filePath   = System.IO.Path.Combine(subDir, storedName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                // Save to DB
                using var conn = _db.GetBTITReqConnection();
                var attachId = await conn.ExecuteScalarAsync<int>(
                    "ITPO_sp_AddAttachment",
                    new
                    {
                        POId        = poId,
                        FileName    = file.FileName,
                        StoredName  = storedName,
                        FileSize    = file.Length,
                        ContentType = contentType,
                        UploadedBy  = user?.SamAcc ?? "unknown"
                    },
                    commandType: System.Data.CommandType.StoredProcedure);

                _logger.LogInformation("Attachment uploaded: PO={po} File={file} By={user}",
                    poId, file.FileName, user?.SamAcc);

                return Ok(new
                {
                    attachId,
                    fileName   = file.FileName,
                    storedName,
                    fileSize   = file.Length,
                    contentType,
                    uploadedBy  = user?.SamAcc,
                    uploadedAt  = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    fileSizeDisplay = file.Length > 1_048_576
                        ? $"{file.Length / 1_048_576.0:N1} MB"
                        : file.Length > 1024
                        ? $"{file.Length / 1024.0:N0} KB"
                        : $"{file.Length} B"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload error PO={po}", poId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ── DOWNLOAD / VIEW ───────────────────────────────────
        [HttpGet("download/{poId:int}/{attachId:int}")]
        public async Task<IActionResult> Download(int poId, int attachId)
        {
            using var conn = _db.GetBTITReqConnection();
            var attach = await conn.QueryFirstOrDefaultAsync<POAttachmentModel>(
                "SELECT * FROM ITPO_POAttachments WHERE AttachId = @AttachId AND POId = @POId",
                new { AttachId = attachId, POId = poId });

            if (attach == null) return NotFound();

            var filePath = System.IO.Path.Combine(UploadRoot, $"po_{poId}", attach.StoredName);
            if (!System.IO.File.Exists(filePath)) return NotFound("File not found on disk");

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, attach.ContentType, attach.FileName);
        }

        // ── DELETE ───────────────────────────────────────────
        [HttpDelete("{attachId:int}")]
        public async Task<IActionResult> Delete(int attachId)
        {
            var user = SessionHelper.GetUser(HttpContext.Session)
                    ?? SessionHelper.GetUserFromClaims(User);

            using var conn = _db.GetBTITReqConnection();
            // Get file info before delete
            var attach = await conn.QueryFirstOrDefaultAsync<POAttachmentModel>(
                "SELECT * FROM ITPO_POAttachments WHERE AttachId = @AttachId",
                new { AttachId = attachId });

            if (attach == null) return NotFound();

            // Delete from DB
            await conn.ExecuteAsync("ITPO_sp_DeleteAttachment",
                new { AttachId = attachId, UserSam = user?.SamAcc },
                commandType: System.Data.CommandType.StoredProcedure);

            // Delete from disk
            var filePath = System.IO.Path.Combine(UploadRoot,
                $"po_{attach.POId}", attach.StoredName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _logger.LogInformation("Attachment deleted: {id} by {user}", attachId, user?.SamAcc);
            return Ok(new { success = true });
        }
    }
}
