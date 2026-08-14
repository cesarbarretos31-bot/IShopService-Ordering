using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BuildingBlocks.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest,
        TResponse>> logger) :
        IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>
        where TResponse : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            logger.LogInformation("[Empezamos] Manejo de Peticion={Request} - Respuesta={Response} - Respuesta Data={RequestData}",
                typeof(TRequest).Name, typeof(TResponse).Name, request);

            var timer = new Stopwatch();
            timer.Start();
            var response = await next();
            timer.Stop();
            var timeToken = timer.Elapsed;
            if (timeToken.Seconds > 3)
                logger.LogWarning("[Performance] La petición {Request} toma {TimeToken} segundos",
                    typeof(TRequest).Name, timeToken.Seconds);
            logger.LogInformation("[Final] Manejar {Request} with {Response}", typeof(TRequest).Name,
                typeof(TResponse).Name);
            return response;
        }
    }
}