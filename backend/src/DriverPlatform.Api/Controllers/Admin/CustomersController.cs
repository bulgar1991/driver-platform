using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>Admin → Customers: who booked, their rides and spend, and their contact details.</summary>
[ApiController]
[Route("api/admin/customers")]
[Authorize(Policy = Policies.Staff)]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CustomerList>> List([FromQuery] CustomerQuery query, CancellationToken cancellationToken) =>
        Ok(await customers.ListAsync(query, cancellationToken));

    /// <summary>Every customer matching the filters (up to 10,000), for the Excel export.</summary>
    [HttpGet("export")]
    public async Task<ActionResult<IReadOnlyList<CustomerListItem>>> Export([FromQuery] CustomerQuery query, CancellationToken cancellationToken) =>
        Ok(await customers.ExportAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await customers.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDetail>> Update(Guid id, CustomerRequest request, CancellationToken cancellationToken) =>
        Ok(await customers.UpdateAsync(id, request, cancellationToken));
}
