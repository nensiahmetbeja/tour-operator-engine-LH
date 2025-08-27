// Lufthansa.Api/Controllers/Requests/PricingUploadRequest.cs
using Microsoft.AspNetCore.Http;

namespace Lufthansa.Api.Controllers.Requests;

public sealed class PricingUploadRequest
{
    // name "File" is what Swagger will show in the form
    public IFormFile File { get; set; } = default!;
}