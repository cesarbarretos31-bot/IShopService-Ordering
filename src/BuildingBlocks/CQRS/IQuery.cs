using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocks.CQRS
{
    /*esta interfz devueelve un resultado de consulta not null*/
   public interface IQuery <out TResponse>: IRequest<TResponse>
        where TResponse : notnull
    {
    }
}
