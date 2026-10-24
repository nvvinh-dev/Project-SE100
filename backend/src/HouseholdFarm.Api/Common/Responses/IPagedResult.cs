namespace HouseholdFarm.Api.Common.Responses;

// Để ResponseEnvelopeFilter nhận ra PagedResult<T> với mọi T mà không cần reflection.
public interface IPagedResult
{
    object Items { get; }

    PageMeta Meta { get; }
}
