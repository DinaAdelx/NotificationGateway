using MediatR;
using NotificationGateway.Application.Abstractions;

namespace NotificationGateway.Application.Templates.PreviewTemplate;

public record PreviewTemplateCommand(string ContentJson, string? Subject, string PayloadJson) : IRequest<PreviewTemplateResult>;
public record PreviewTemplateResult(string Body, string? Subject);

public class PreviewTemplateHandler(ITemplateRenderer renderer)
    : IRequestHandler<PreviewTemplateCommand, PreviewTemplateResult>
{
    public Task<PreviewTemplateResult> Handle(PreviewTemplateCommand request, CancellationToken ct)
    {
        var result = renderer.Preview(request.ContentJson, request.Subject, request.PayloadJson);
        return Task.FromResult(new PreviewTemplateResult(result.Body, result.Subject));
    }
}
