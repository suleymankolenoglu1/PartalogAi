using FluentValidation;
using Katalogcu.Application.Features.Folders.Commands.CreateFolder;
using Katalogcu.Application.Features.Folders.Commands.DeleteFolder;
using Katalogcu.Application.Features.Folders.Queries.GetMyFolders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class FoldersController : ControllerBase
    {
        private readonly ISender _sender;

        public FoldersController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyFolders()
        {
            try
            {
                var result = await _sender.Send(new GetMyFoldersQuery());
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Klasörler alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderDto request)
        {
            try
            {
                var result = await _sender.Send(new CreateFolderCommand(request.Name));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "duplicate" => BadRequest(result.ErrorMessage),
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Klasör oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFolder(Guid id)
        {
            try
            {
                var result = await _sender.Send(new DeleteFolderCommand(id));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Klasör silinirken hata oluştu.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public class CreateFolderDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
