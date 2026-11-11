using Companies.Api.Http;
using Companies.Application.Companies;
using Microsoft.AspNetCore.Mvc;

namespace Companies.Api.Controllers;

/// <summary>
/// HTTP surface for companies. Each action translates between HTTP and <see cref="ICompanyService"/>;
/// rules and decisions live in the application layer.
/// </summary>
[ApiController]
[Route("api/companies")]
public sealed class CompaniesController(ICompanyService companies) : ControllerBase
{
    /// <summary>Lists companies. Supports filters (name, domain), ranked free-text search (q) and paging.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<CompanyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] CompanyQuery query, CancellationToken cancellationToken) =>
        Ok(await companies.SearchAsync(query, cancellationToken));

    [HttpGet("{id}", Name = nameof(GetById))]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await companies.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var result = await companies.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtRoute(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPut("{id}")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        var result = await companies.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await companies.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
