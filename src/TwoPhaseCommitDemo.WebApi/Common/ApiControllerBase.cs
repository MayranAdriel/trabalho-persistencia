using Microsoft.AspNetCore.Mvc;
using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.WebApi.Common;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(new { success = true });
        }

        return MapErrorToResponse(result.Error);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return MapErrorToResponse(result.Error);
    }

    private IActionResult MapErrorToResponse(Error error)
    {
        var response = new
        {
            success = false,
            error = new
            {
                code = error.Code,
                message = error.Message,
                type = error.Type.ToString()
            }
        };

        return error.Type switch
        {
            ErrorType.NotFound => NotFound(response),
            ErrorType.Validation => BadRequest(response),
            ErrorType.Conflict => Conflict(response),
            ErrorType.DatabaseError => StatusCode(StatusCodes.Status500InternalServerError, response),
            _ => BadRequest(response)
        };
    }
}

