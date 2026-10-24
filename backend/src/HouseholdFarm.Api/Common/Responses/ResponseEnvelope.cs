using System.Text.Json.Serialization;

namespace HouseholdFarm.Api.Common.Responses;

/// <summary>Dạng chung của mọi phản hồi thành công: <c>{ data, meta?, warnings }</c> (D-32).</summary>
public sealed record ResponseEnvelope(
    object? Data,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PageMeta? Meta,
    IReadOnlyList<ResponseWarning> Warnings);
