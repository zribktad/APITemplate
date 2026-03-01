using System.Linq.Expressions;

namespace APITemplate.Application.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}
