using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Expenses;
using HouseholdFarm.Api.Features.FarmTasks;
using HouseholdFarm.Api.Features.Harvests;
using HouseholdFarm.Api.Features.Reports.Finance;
using HouseholdFarm.Api.Features.Sales;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Features.Supplies;

namespace HouseholdFarm.Api.Features.Ai;

/// <summary>
/// Công cụ AI (<c>AITool</c>) của trợ lý hỏi đáp (BR-AI-09): chỉ đọc, mỗi công cụ gọi hàm đọc có sẵn của module sở hữu dữ liệu
/// với danh tính người đang hỏi (BR-AI-02), nên phân quyền, phạm vi, lọc theo hộ áp dụng như gọi API.
/// Không có công cụ đọc nhật ký thao tác, danh sách tài khoản, cài đặt hộ, danh mục khách hàng (có số điện thoại, BR-AI-07).
/// </summary>
public sealed class AssistantToolbox(
    ISeasonService seasons,
    IFarmTaskService farmTasks,
    IHarvestService harvests,
    IProduceStockService produceStock,
    ISupplyService supplies,
    IStockTransactionService stockTransactions,
    IExpenseService expenses,
    ISaleService sales,
    ISeasonFinanceService seasonFinance)
{
    // Một lần gọi trả tối đa chừng này dòng để câu hỏi không tốn quá nhiều token; kèm tổng số để AI biết danh sách bị cắt.
    private const int MaxItems = 50;

    private const int MaxSeasonIds = 20;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<AIToolDefinition> Definitions { get; } =
    [
        Define(
            "list_seasons",
            "Danh sách mùa vụ người hỏi được xem: cây trồng, mã thửa, ngày bắt đầu, ngày dự kiến thu hoạch, mục tiêu sản lượng, "
            + "sản lượng đã thu, độ lệch sản lượng, số công việc. Dùng để tìm seasonId cho các công cụ khác.",
            new()
            {
                ["status"] = EnumProperty<SeasonStatus>("Trạng thái mùa vụ."),
                ["search"] = StringProperty("Tìm theo tên cây trồng hoặc mã thửa."),
                ["fromDate"] = DateProperty("Mùa vụ bắt đầu từ ngày này."),
                ["toDate"] = DateProperty("Mùa vụ bắt đầu đến ngày này."),
            }),
        Define(
            "get_season_detail",
            "Chi tiết một mùa vụ. Phần tài chính chỉ có khi người hỏi có quyền xem.",
            new() { ["seasonId"] = StringProperty("Id mùa vụ.") },
            "seasonId"),
        Define(
            "list_farm_tasks",
            "Danh sách công việc canh tác: loại việc, ngày dự kiến, người thực hiện, trạng thái, trễ hạn.",
            new()
            {
                ["seasonId"] = StringProperty("Id mùa vụ."),
                ["status"] = EnumProperty<FarmTaskStatus>("Trạng thái công việc."),
                ["isOverdue"] = BoolProperty("true: chỉ công việc trễ hạn."),
                ["fromDate"] = DateProperty("Ngày dự kiến từ."),
                ["toDate"] = DateProperty("Ngày dự kiến đến."),
            }),
        Define(
            "list_harvest_records",
            "Danh sách phiếu thu hoạch chưa hủy: ngày thu, loại chất lượng, sản lượng.",
            new()
            {
                ["seasonId"] = StringProperty("Id mùa vụ."),
                ["qualityGrade"] = EnumProperty<QualityGrade>("Loại chất lượng."),
                ["fromDate"] = DateProperty("Ngày thu từ."),
                ["toDate"] = DateProperty("Ngày thu đến."),
            }),
        Define(
            "get_produce_stock",
            "Tồn nông sản theo mùa vụ và loại chất lượng. Không truyền seasonId thì chỉ trả các dòng còn tồn.",
            new()
            {
                ["seasonId"] = StringProperty("Id mùa vụ."),
                ["qualityGrade"] = EnumProperty<QualityGrade>("Loại chất lượng."),
            }),
        Define(
            "list_supplies",
            "Danh sách vật tư kèm tồn kho, ngưỡng tồn tối thiểu; giá chỉ có khi người hỏi có quyền Quản lý kho.",
            new()
            {
                ["group"] = EnumProperty<SupplyGroup>("Nhóm vật tư."),
                ["search"] = StringProperty("Tìm theo mã hoặc tên vật tư."),
                ["belowMin"] = BoolProperty("true: chỉ vật tư dưới ngưỡng tồn tối thiểu."),
            }),
        Define(
            "list_stock_transactions",
            "Lịch sử giao dịch kho (nhập, xuất, điều chỉnh) chưa hủy, mới nhất trước.",
            new()
            {
                ["supplyId"] = StringProperty("Id vật tư."),
                ["group"] = EnumProperty<SupplyGroup>("Nhóm vật tư."),
                ["type"] = EnumProperty<StockTransactionType>("Loại giao dịch kho."),
                ["seasonId"] = StringProperty("Id mùa vụ (giao dịch xuất)."),
                ["fromDate"] = DateProperty("Ngày giao dịch từ."),
                ["toDate"] = DateProperty("Ngày giao dịch đến."),
            }),
        Define(
            "list_expenses",
            "Danh sách chi phí ghi tay chưa hủy (không gồm chi phí vật tư), mới nhất trước.",
            new()
            {
                ["seasonId"] = StringProperty("Id mùa vụ."),
                ["category"] = EnumProperty<ExpenseCategory>("Nhóm chi phí."),
                ["general"] = BoolProperty("true: chỉ chi phí chung của hộ, không gắn mùa vụ."),
                ["fromDate"] = DateProperty("Ngày chi từ."),
                ["toDate"] = DateProperty("Ngày chi đến."),
            }),
        Define(
            "list_sales",
            "Danh sách giao dịch bán chưa hủy: ngày bán, khách hàng, lượng, doanh thu, đã thu, công nợ, trạng thái thanh toán.",
            new()
            {
                ["seasonId"] = StringProperty("Id mùa vụ."),
                ["paymentStatus"] = EnumProperty<PaymentStatus>("Trạng thái thanh toán."),
                ["fromDate"] = DateProperty("Ngày bán từ."),
                ["toDate"] = DateProperty("Ngày bán đến."),
            }),
        Define(
            "get_sale_debts",
            "Công nợ bán hàng chưa thu của hộ, nợ lâu nhất trước, kèm tổng số và tổng tiền.",
            []),
        Define(
            "get_season_finance",
            "Số liệu tài chính của mùa vụ: chi phí vật tư, chi phí ghi tay, doanh thu, đã thu, công nợ, lợi nhuận "
            + "(tạm tính khi chưa chốt). Luôn dùng công cụ này cho số liệu tài chính theo mùa vụ, không tự cộng.",
            new()
            {
                ["seasonIds"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = $"Id các mùa vụ, tối đa {MaxSeasonIds}.",
                },
            },
            "seasonIds"),
    ];

    /// <summary>
    /// Gọi công cụ theo yêu cầu của model. Lỗi quyền, không tìm thấy, tham số sai trả thành kết quả để AI báo lại cho người hỏi,
    /// không trả số liệu (BR-AI-02).
    /// </summary>
    public async Task<JsonNode> InvokeAsync(AIToolCall call, CancellationToken ct)
    {
        var args = new ToolArguments(call.Arguments);
        try
        {
            object result = call.Name switch
            {
                "list_seasons" => Page(await seasons.GetListAsync(
                    new SeasonQuery
                    {
                        Status = args.Enum<SeasonStatus>("status"),
                        Search = args.String("search"),
                        From = args.Date("fromDate"),
                        To = args.Date("toDate"),
                        PageSize = MaxItems,
                    },
                    ct)),
                "get_season_detail" => SeasonDetailForAI.From(await seasons.GetDetailAsync(args.RequiredGuid("seasonId"), ct)),
                "list_farm_tasks" => Page(await farmTasks.GetListAsync(
                    new FarmTaskQuery
                    {
                        SeasonId = args.Guid("seasonId"),
                        Status = args.Enum<FarmTaskStatus>("status"),
                        IsOverdue = args.Bool("isOverdue"),
                        From = args.Date("fromDate"),
                        To = args.Date("toDate"),
                        PageSize = MaxItems,
                    },
                    ct)),
                "list_harvest_records" => Page(await harvests.GetListAsync(
                    new HarvestRecordQuery
                    {
                        SeasonId = args.Guid("seasonId"),
                        QualityGrade = args.Enum<QualityGrade>("qualityGrade"),
                        From = args.Date("fromDate"),
                        To = args.Date("toDate"),
                        PageSize = MaxItems,
                    },
                    ct)),
                "get_produce_stock" => await produceStock.GetOverviewAsync(
                    new ProduceStockOverviewQuery
                    {
                        SeasonId = args.Guid("seasonId"),
                        QualityGrade = args.Enum<QualityGrade>("qualityGrade"),
                    },
                    ct),
                "list_supplies" => Page(await supplies.GetListAsync(
                    new SupplyQuery
                    {
                        Group = args.Enum<SupplyGroup>("group"),
                        Search = args.String("search"),
                        BelowMin = args.Bool("belowMin"),
                        PageSize = MaxItems,
                    },
                    ct)),
                "list_stock_transactions" => await ListStockTransactionsAsync(args, ct),
                "list_expenses" => Page(await expenses.GetListAsync(
                    new ExpenseQuery
                    {
                        SeasonId = args.Guid("seasonId"),
                        Category = args.Enum<ExpenseCategory>("category"),
                        General = args.Bool("general"),
                        From = args.Date("fromDate"),
                        To = args.Date("toDate"),
                        PageSize = MaxItems,
                    },
                    ct)),
                "list_sales" => Page(await sales.GetListAsync(
                    new SaleQuery
                    {
                        SeasonId = args.Guid("seasonId"),
                        PaymentStatus = args.Enum<PaymentStatus>("paymentStatus"),
                        From = args.Date("fromDate"),
                        To = args.Date("toDate"),
                        PageSize = MaxItems,
                    },
                    ct)),
                "get_sale_debts" => await sales.GetDebtsAsync(ct),
                "get_season_finance" => await seasonFinance.GetAsync(args.RequiredGuidList("seasonIds", MaxSeasonIds), ct),
                _ => throw new ToolArgumentException($"Không có công cụ {call.Name}."),
            };

            return JsonSerializer.SerializeToNode(result, _json) ?? new JsonObject();
        }
        catch (AppException ex) when (ex.Code is ErrorCodes.Forbidden or ErrorCodes.Unauthenticated)
        {
            // BR-AI-02: không trả số liệu, chỉ báo để AI trả lời người hỏi là không có quyền xem.
            return Error("FORBIDDEN", "Người hỏi không có quyền xem thông tin này.");
        }
        catch (AppException ex) when (ex.Code is ErrorCodes.NotFound)
        {
            return Error("NOT_FOUND", "Không tìm thấy dữ liệu, hoặc dữ liệu nằm ngoài phạm vi người hỏi được xem.");
        }
        catch (AppException ex) when (ex.Code is ErrorCodes.ValidationError)
        {
            return Error("INVALID_ARGUMENT", ex.Message);
        }
        catch (ToolArgumentException ex)
        {
            return Error("INVALID_ARGUMENT", ex.Message);
        }
    }

    // BR-AI-09: giao dịch kho lọc được theo nhóm vật tư; API lịch sử chỉ lọc theo một vật tư nên lọc nhóm ở đây.
    private async Task<object> ListStockTransactionsAsync(ToolArguments args, CancellationToken ct)
    {
        var group = args.Enum<SupplyGroup>("group");
        var query = new StockTransactionQuery
        {
            SupplyId = args.Guid("supplyId"),
            Type = args.Enum<StockTransactionType>("type"),
            SeasonId = args.Guid("seasonId"),
            From = args.Date("fromDate"),
            To = args.Date("toDate"),
            PageSize = group is null ? MaxItems : PageQuery.MaxPageSize,
        };

        var page = await stockTransactions.GetListAsync(query, ct);
        if (group is null)
        {
            return Page(page);
        }

        var groupSupplies = await supplies.GetListAsync(
            new SupplyQuery { Group = group, PageSize = PageQuery.MaxPageSize },
            ct);
        var supplyIds = groupSupplies.Items.Select(s => s.Id).ToHashSet();
        var items = page.Items.Where(t => supplyIds.Contains(t.SupplyId)).ToList();
        return new
        {
            Items = items.Take(MaxItems).ToList(),
            ShownCount = Math.Min(items.Count, MaxItems),
            Note = page.TotalCount > page.Items.Count
                ? $"Chỉ xét {page.Items.Count} giao dịch gần nhất trong {page.TotalCount} giao dịch; thu hẹp khoảng ngày để xem đủ."
                : null,
        };
    }

    /// <summary>
    /// Chi tiết mùa vụ gửi cho AI: bỏ số điện thoại của người được phân công (BR-AI-07), chỉ giữ họ tên và vai trò;
    /// bỏ phiên bản và các cờ thao tác vì AI chỉ đọc.
    /// </summary>
    private sealed record SeasonDetailForAI(
        Guid Id,
        string PlotCode,
        string CropName,
        Unit YieldUnit,
        DateOnly StartDate,
        DateOnly ExpectedHarvestDate,
        decimal TargetYield,
        SeasonStatus Status,
        DateTime? StartedAt,
        DateTime? ClosedAt,
        DateTime? CancelledAt,
        string? CancelReason,
        decimal HarvestedYield,
        decimal YieldDeviation,
        int CompletedTasksCount,
        int TotalTasksCount,
        IReadOnlyList<ProduceStockSummaryItem> ProduceStocks,
        SeasonFinancialSummary? FinancialSummary,
        IReadOnlyList<SeasonAssigneeForAI> Assignments)
    {
        public static SeasonDetailForAI From(SeasonDetailResponse s) => new(
            s.Id,
            s.PlotCode,
            s.CropName,
            s.YieldUnit,
            s.StartDate,
            s.ExpectedHarvestDate,
            s.TargetYield,
            s.Status,
            s.StartedAt,
            s.ClosedAt,
            s.CancelledAt,
            s.CancelReason,
            s.HarvestedYield,
            s.YieldDeviation,
            s.CompletedTasksCount,
            s.TotalTasksCount,
            s.ProduceStocks,
            s.FinancialSummary,
            s.Assignments.Select(a => new SeasonAssigneeForAI(a.FullName, a.Role)).ToList());
    }

    private sealed record SeasonAssigneeForAI(string FullName, UserRole Role);

    private static object Page<T>(PagedResult<T> page) => new
    {
        page.Items,
        page.TotalCount,
        Note = page.TotalCount > page.Items.Count
            ? $"Chỉ có {page.Items.Count} trong {page.TotalCount} dòng; thu hẹp bộ lọc để xem đủ."
            : null,
    };

    private static JsonObject Error(string code, string message) => new() { ["error"] = code, ["message"] = message };

    private static AIToolDefinition Define(
        string name,
        string description,
        JsonObject properties,
        params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0)
        {
            schema["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray());
        }

        return new AIToolDefinition(name, description, schema);
    }

    private static JsonObject StringProperty(string description) =>
        new() { ["type"] = "string", ["description"] = description };

    private static JsonObject BoolProperty(string description) =>
        new() { ["type"] = "boolean", ["description"] = description };

    private static JsonObject DateProperty(string description) =>
        new() { ["type"] = "string", ["format"] = "date", ["description"] = $"{description} Dạng yyyy-MM-dd." };

    private static JsonObject EnumProperty<TEnum>(string description)
        where TEnum : struct, Enum =>
        new()
        {
            ["type"] = "string",
            ["enum"] = new JsonArray(Enum.GetNames<TEnum>().Select(n => (JsonNode)n).ToArray()),
            ["description"] = description,
        };

    private sealed class ToolArgumentException(string message) : Exception(message);

    /// <summary>Đọc tham số model gửi; giá trị sai kiểu thì báo lại cho model thay vì đoán.</summary>
    private sealed class ToolArguments(JsonObject args)
    {
        public string? String(string name) =>
            args[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : null;

        public bool? Bool(string name) => args[name] switch
        {
            null => null,
            JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
            JsonValue value when value.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsed) => parsed,
            _ => throw new ToolArgumentException($"Tham số {name} phải là true hoặc false."),
        };

        public TEnum? Enum<TEnum>(string name)
            where TEnum : struct, Enum
        {
            var text = String(name);
            if (text is null)
            {
                return null;
            }

            return System.Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && System.Enum.IsDefined(value)
                ? value
                : throw new ToolArgumentException(
                    $"Tham số {name} chỉ nhận: {string.Join(", ", System.Enum.GetNames<TEnum>())}.");
        }

        public Guid? Guid(string name)
        {
            var text = String(name);
            if (text is null)
            {
                return null;
            }

            return System.Guid.TryParse(text, out var id)
                ? id
                : throw new ToolArgumentException($"Tham số {name} phải là id lấy từ kết quả công cụ khác.");
        }

        public Guid RequiredGuid(string name) =>
            Guid(name) ?? throw new ToolArgumentException($"Thiếu tham số {name}.");

        public DateOnly? Date(string name)
        {
            var text = String(name);
            if (text is null)
            {
                return null;
            }

            return DateOnly.TryParseExact(text, "yyyy-MM-dd", out var date)
                ? date
                : throw new ToolArgumentException($"Tham số {name} phải có dạng yyyy-MM-dd.");
        }

        public IReadOnlyCollection<Guid> RequiredGuidList(string name, int max)
        {
            if (args[name] is not JsonArray array || array.Count == 0)
            {
                throw new ToolArgumentException($"Thiếu tham số {name}.");
            }

            if (array.Count > max)
            {
                throw new ToolArgumentException($"Tham số {name} tối đa {max} id.");
            }

            return array
                .Select(item => item is JsonValue value && value.TryGetValue<string>(out var text)
                    && System.Guid.TryParse(text, out var id)
                        ? id
                        : throw new ToolArgumentException($"Tham số {name} phải là danh sách id lấy từ kết quả công cụ khác."))
                .Distinct()
                .ToList();
        }
    }
}
