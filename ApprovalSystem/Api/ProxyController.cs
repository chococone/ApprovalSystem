using Microsoft.AspNetCore.Mvc;

namespace ApprovalSystem.Api;

[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
}