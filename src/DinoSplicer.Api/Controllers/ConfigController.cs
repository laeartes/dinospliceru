using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController(IOptions<VideoUploadOptions> uploadOptions) : ControllerBase
{
    // Uses IOptions like VideoController, so the values returned here are always the ones upload validation enforces
    [HttpGet]
    public ActionResult<ConfigResponse> Get()
    {
        return new ConfigResponse(VideoUploadConfigResponse.FromOptions(uploadOptions.Value));
    }
}
