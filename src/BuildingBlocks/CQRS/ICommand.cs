using MediatR;
namespace BuildingBlocks.CQRS;


public interface ICommand : ICommand<Unit>
{

}
//TResponse genera una respuesta generica, pero para esto debe recibir una peticion generica
public interface ICommand<out TResponse> : IRequest<TResponse>
{

}