using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NotificationGateway.API.OpenApi;

public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-Id",
            In = ParameterLocation.Header,
            Description = "Tenant GUID context (e.g. 11111111-1111-1111-1111-111111111111)",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "uuid",
                Default = new Microsoft.OpenApi.Any.OpenApiString("11111111-1111-1111-1111-111111111111")
            }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Application-Id",
            In = ParameterLocation.Header,
            Description = "Application Client GUID context (e.g. 22222222-2222-2222-2222-222222222222)",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "uuid",
                Default = new Microsoft.OpenApi.Any.OpenApiString("22222222-2222-2222-2222-222222222222")
            }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Client-Id",
            In = ParameterLocation.Header,
            Description = "Client Identifier (optional)",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new Microsoft.OpenApi.Any.OpenApiString("order-service")
            }
        });
    }
}
