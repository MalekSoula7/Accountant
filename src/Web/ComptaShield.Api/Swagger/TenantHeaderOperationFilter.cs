using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ComptaShield.Api.Swagger;

/// <summary>
/// OpenAPI/Swagger filter that adds multi-tenancy headers (X-Dossier-Id, X-Exercice-Id)
/// to all endpoint documentation.
/// </summary>
public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Dossier-Id",
            In = ParameterLocation.Header,
            Description = "Dossier (Client Company) UUID for two-tier tenant scoping",
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Exercice-Id",
            In = ParameterLocation.Header,
            Description = "Fiscal Year (Exercice) UUID within the selected Dossier",
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
