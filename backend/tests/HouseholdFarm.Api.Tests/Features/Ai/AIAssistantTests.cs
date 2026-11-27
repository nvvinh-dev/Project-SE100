using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Ai;
using HouseholdFarm.Api.Tests.Features.Sales;
using HouseholdFarm.Api.Tests.Infrastructure;
using static HouseholdFarm.Api.Tests.Features.Ai.AiTestHelpers;

namespace HouseholdFarm.Api.Tests.Features.Ai;

/// <summary>Trợ lý hỏi đáp dữ liệu của hộ (FR-AI-02).</summary>
public class AIAssistantTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-AI-02: hỏi trợ lý trả câu trả lời, gửi kèm lịch sử phiên, ghi một lượt Assistant của người hỏi")]
    public async Task Ask_ReturnsAnswer_SendsHistory_RecordsOneUsage()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient().ThenText("Hộ chưa có mùa vụ nào.");
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await app.ClientFor(household.Member).PostAsJsonAsync(
            AssistantPath,
            Ask("Còn mùa nào khác không?", ("Có bao nhiêu mùa vụ?", "Hộ có 0 mùa vụ.")),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Hộ chưa có mùa vụ nào.", data.GetProperty("answer").GetString());
        Assert.Equal(1, data.GetProperty("quota").GetProperty("used").GetInt32());

        var request = Assert.Single(ai.Requests);
        Assert.Collection(
            request.Turns,
            t => Assert.Equal("Có bao nhiêu mùa vụ?", Assert.IsType<AIUserTurn>(t).Text),
            t => Assert.Equal("Hộ có 0 mùa vụ.", Assert.IsType<AIModelTurn>(t).Text),
            t => Assert.Equal("Còn mùa nào khác không?", Assert.IsType<AIUserTurn>(t).Text));
        Assert.Contains(request.Tools, tool => tool.Name == "get_season_finance");

        var usage = Assert.Single(await UsagesAsync(factory.Postgres, household, ct));
        Assert.Equal(household.Member.Id, usage.UserId);
        Assert.Equal(AIFeature.Assistant, usage.Feature);
    }

    [Fact(DisplayName = "BR-AI-04: một câu hỏi gọi hai công cụ chỉ tính một lượt")]
    public async Task Ask_WithTwoToolCalls_CountsOneUsage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await SaleTestData.ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await SaleTestData.CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var ai = new FakeAIClient()
            .ThenToolCalls(
                new AIToolCall("1", "list_seasons", new JsonObject { ["status"] = "InProgress" }),
                new AIToolCall("2", "get_produce_stock", new JsonObject { ["seasonId"] = season.Id.ToString() }))
            .ThenText("Mùa vụ đang canh tác còn 300 kg loại 1.");
        await using var app = factory.WithFakeAI(ai);

        var response = await app.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Còn bao nhiêu lúa?"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, ai.Requests.Count);
        var results = Assert.IsType<AIToolResultsTurn>(ai.Requests[1].Turns[^1]).Results;
        Assert.Equal(["list_seasons", "get_produce_stock"], results.Select(r => r.Name));
        Assert.Equal(season.Id.ToString(), results[0].Output["items"]![0]!["id"]!.GetValue<string>());
        Assert.Contains("300", results[1].Output.ToJsonString());
        Assert.Single(await UsagesAsync(factory.Postgres, household, ct));
    }

    [Fact(DisplayName = "TC-25: Thành viên không có TC hỏi doanh thu thì công cụ báo không có quyền, không có số liệu doanh thu")]
    public async Task TC25_Ask_RevenueAsMemberWithoutFinance_ToolReturnsForbiddenWithoutFigures()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await SaleTestData.ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);
        await SaleTestData.CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);
        await SaleTestData.CreateSaleAsync(factory.Postgres, household, season.Id, ct, quantity: 123m, unitPrice: 7_000m);
        var memberAI = RevenueQuestionScript(season.Id);
        var ownerAI = RevenueQuestionScript(season.Id);
        await using var memberApp = factory.WithFakeAI(memberAI);
        await using var ownerApp = factory.WithFakeAI(ownerAI);

        var memberResponse = await memberApp.ClientFor(household.Member)
            .PostAsJsonAsync(AssistantPath, Ask("Doanh thu mùa lúa vừa rồi bao nhiêu?"), ct);
        var ownerResponse = await ownerApp.ClientFor(household.Owner)
            .PostAsJsonAsync(AssistantPath, Ask("Doanh thu mùa lúa vừa rồi bao nhiêu?"), ct);

        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        var memberResults = Assert.IsType<AIToolResultsTurn>(memberAI.Requests[1].Turns[^1]).Results;
        Assert.All(memberResults, r =>
        {
            Assert.Equal(ErrorCodes.Forbidden, r.Output["error"]!.GetValue<string>());
            Assert.DoesNotContain("861000", r.Output.ToJsonString());
        });

        // Đối chứng: Chủ hộ hỏi cùng câu thì công cụ trả đúng doanh thu 123 × 7.000.
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        var ownerResults = Assert.IsType<AIToolResultsTurn>(ownerAI.Requests[1].Turns[^1]).Results;
        Assert.All(ownerResults, r => Assert.Contains("861000", r.Output.ToJsonString()));
    }

    [Fact(DisplayName = "BR-AI-02: công cụ chỉ thấy mùa vụ trong phạm vi của người hỏi")]
    public async Task Ask_Tools_UseAskerScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await SaleTestData.ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var ai = new FakeAIClient()
            .ThenToolCalls(
                new AIToolCall("1", "list_seasons", []),
                new AIToolCall("2", "get_season_detail", new JsonObject { ["seasonId"] = season.Id.ToString() }))
            .ThenText("Chưa được phân công mùa vụ nào.");
        await using var app = factory.WithFakeAI(ai);

        // Thành viên chưa được phân công vào mùa vụ nào.
        var response = await app.ClientFor(household.Member).PostAsJsonAsync(AssistantPath, Ask("Tôi có mùa vụ nào?"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = Assert.IsType<AIToolResultsTurn>(ai.Requests[1].Turns[^1]).Results;
        Assert.Equal(0, results[0].Output["totalCount"]!.GetValue<int>());
        Assert.Equal("NOT_FOUND", results[1].Output["error"]!.GetValue<string>());
    }

    [Fact(DisplayName = "FR-AI-02: tham số công cụ sai được báo lại cho AI, không làm hỏng câu hỏi")]
    public async Task Ask_ToolWithInvalidArgument_ReturnsErrorToAI()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient()
            .ThenToolCalls(
                new AIToolCall("1", "list_farm_tasks", new JsonObject { ["status"] = "Unknown", ["fromDate"] = "26/11/2026" }),
                new AIToolCall("2", "delete_everything", []))
            .ThenText("Không đọc được dữ liệu.");
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await app.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Việc trễ hạn?"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = Assert.IsType<AIToolResultsTurn>(ai.Requests[1].Turns[^1]).Results;
        Assert.All(results, r => Assert.Equal("INVALID_ARGUMENT", r.Output["error"]!.GetValue<string>()));
    }

    [Fact(DisplayName = "FR-AI-02: model gọi công cụ mãi thì vòng cuối không đưa công cụ, buộc trả lời bằng chữ")]
    public async Task Ask_WhenModelKeepsCallingTools_LastRoundHasNoTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient();
        for (var i = 0; i < 4; i++)
        {
            ai.ThenToolCalls(new AIToolCall(null, "get_sale_debts", []));
        }

        ai.ThenText("Hộ không còn công nợ.");
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await app.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Ai còn nợ?"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, ai.Requests.Count);
        Assert.Empty(ai.Requests[^1].Tools);
        Assert.Single(await UsagesAsync(factory.Postgres, household, ct));
    }

    [Fact(DisplayName = "FR-AI-02: câu hỏi trống hoặc quá dài trả VALIDATION_ERROR, không gọi dịch vụ AI")]
    public async Task Ask_WithInvalidQuestion_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient();
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);
        var client = app.ClientFor(household.Owner);

        var empty = await client.PostAsJsonAsync(AssistantPath, Ask("   "), ct);
        var tooLong = await client.PostAsJsonAsync(AssistantPath, Ask(new string('a', 501)), ct);

        await empty.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await tooLong.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Empty(ai.Requests);
    }

    [Fact(DisplayName = "BR-AI-07: chỉ dẫn gửi AI không chứa số điện thoại, địa chỉ, thông tin liên hệ của hộ")]
    public async Task Ask_SystemInstruction_HasNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient().ThenText("Xin chào.");
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);

        await app.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Xin chào"), ct);

        var instruction = Assert.Single(ai.Requests).SystemInstruction;
        Assert.DoesNotContain(household.Owner.PhoneNumber!, instruction);
        Assert.DoesNotContain(household.Household.ProductionAddress, instruction);
        Assert.DoesNotContain(household.Household.Name, instruction);
    }

    [Fact(DisplayName = "BR-AI-07: chi tiết mùa vụ gửi AI có họ tên người được phân công, không có số điện thoại")]
    public async Task Ask_SeasonDetailTool_SendsNoPhoneNumber()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await SaleTestData.ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);
        var ai = new FakeAIClient()
            .ThenToolCalls(new AIToolCall("1", "get_season_detail", new JsonObject { ["seasonId"] = season.Id.ToString() }))
            .ThenText("Mùa vụ có một người được phân công.");
        await using var app = factory.WithFakeAI(ai);

        var response = await app.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Ai làm mùa vụ này?"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = Assert.Single(Assert.IsType<AIToolResultsTurn>(ai.Requests[1].Turns[^1]).Results).Output;
        var assignee = Assert.Single(detail["assignments"]!.AsArray());
        Assert.Equal(household.Member.FullName, assignee!["fullName"]!.GetValue<string>());
        var output = detail.ToJsonString();
        Assert.DoesNotContain(household.Member.PhoneNumber!, output);
        Assert.DoesNotContain("phoneNumber", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "FR-AI-02: câu trả lời quá dài được cắt ở dấu cách gần nhất và thêm dấu ba chấm")]
    public async Task Ask_LongAnswer_TruncatesAtWordBoundary()
    {
        var ct = TestContext.Current.CancellationToken;
        var longAnswer = string.Concat(Enumerable.Repeat("lúa ", 1500));
        var ai = new FakeAIClient().ThenText(longAnswer);
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await app.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Kể dài"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
        var answer = body!["data"]!["answer"]!.GetValue<string>();
        Assert.True(answer.Length <= AskAssistantRequestValidator.AnswerMaxLength);
        Assert.EndsWith("lúa...", answer);
    }

    private static FakeAIClient RevenueQuestionScript(Guid seasonId) => new FakeAIClient()
        .ThenToolCalls(
            new AIToolCall("1", "get_season_finance", new JsonObject { ["seasonIds"] = new JsonArray(seasonId.ToString()) }),
            new AIToolCall("2", "list_sales", new JsonObject { ["seasonId"] = seasonId.ToString() }))
        .ThenText("Câu trả lời giả lập.");
}
