using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ApiErrorResult(int statusCode, string error, string code)
    {
        return new ObjectResult(new ApiError(error, code)) { StatusCode = statusCode };
    }
}