using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/sector-industry")]
public class SectorIndustryController(SectorIndustryService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<SectorIndustryListsDto> GetLists() => Ok(service.GetLists());

    [HttpPut]
    public IActionResult SaveLists([FromBody] UpdateSectorIndustryListsRequest request)
    {
        service.SaveLists(request);
        return Ok(service.GetLists());
    }
}
