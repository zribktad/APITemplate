namespace APITemplate.Application.Common.Contracts;
public interface IDateRangeFilter
{
    DateTime? CreatedFrom { get; }
    DateTime? CreatedTo { get; }
}
