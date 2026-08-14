using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sector-industry")]
public class SectorIndustryController(SectorIndustryService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<SectorIndustryListsDto> GetLists() => Ok(service.GetLists());

    [Authorize(Roles = "Admin")]
    [HttpPut]
    public IActionResult SaveLists([FromBody] UpdateSectorIndustryListsRequest request)
    {
        service.SaveLists(request);
        return Ok(service.GetLists());
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Dedicated Decision Sources endpoints Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [HttpGet("decision-sources")]
    public ActionResult<DecisionSourcesDto> GetDecisionSources()
        => Ok(service.GetDecisionSources());

    [Authorize(Roles = "Admin")]
    [HttpPut("decision-sources")]
    public ActionResult<DecisionSourcesDto> SaveDecisionSources(
        [FromBody] UpdateDecisionSourcesRequest request)
        => Ok(service.SaveDecisionSources(request));
}