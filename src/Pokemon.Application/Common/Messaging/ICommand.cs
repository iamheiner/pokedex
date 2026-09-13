using MediatR;
namespace Pokemon.Application.Common.Messaging;

/// <summary>Contrato explícito de una operación que modifica estado.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;
