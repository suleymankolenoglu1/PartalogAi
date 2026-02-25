using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Katalogcu.API.Services;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public FilesController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var validationError = UploadValidation.ValidateUploadFile(file);
            if (!string.IsNullOrWhiteSpace(validationError))
                return BadRequest(validationError);

            // 1. Kök dizini garantiye al (PdfService ile AYNI mantık)
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            
            // 2. Klasör yolu
            string uploadsFolder = Path.Combine(webRoot, "uploads");
            
            // 3. Klasör yoksa oluştur
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // 4. Benzersiz isim oluştur
            string safeFileName = UploadValidation.SanitizeFileName(file.FileName);
            string uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 5. Dosyayı Fiziksel Olarak Kaydet
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // 6. URL Oluştur
            var baseUrl = $"{Request.Scheme}://{Request.Host}/uploads/{uniqueFileName}";
            
            return Ok(new { url = baseUrl });
        }
    }
}
