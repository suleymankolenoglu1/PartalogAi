using Katalogcu.Application.Common.Models;

namespace Katalogcu.API.Services;

public static class FormFileUploadAdapter
{
    public static UploadedFile ToUploadedFile(this IFormFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new UploadedFile(
            () => file.OpenReadStream(),
            file.FileName,
            file.Name,
            file.ContentType,
            file.Length);
    }
}
