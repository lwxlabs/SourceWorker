using Microsoft.Extensions.DependencyInjection;

namespace LwX.Flow;

public interface IRequest<TResponse>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestFlow
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

public sealed class ServiceProviderRequestFlow(IServiceProvider serviceProvider) : IRequestFlow
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);
        return (Task<TResponse>)handlerType.GetMethod("Handle")!.Invoke(handler, [request, cancellationToken])!;
    }
}
