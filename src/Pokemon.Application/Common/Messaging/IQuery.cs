using MediatR;
namespace Pokemon.Application.Common.Messaging;

/// <summary>
/// Contrato explícito de una consulta sin escritura persistente.
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
