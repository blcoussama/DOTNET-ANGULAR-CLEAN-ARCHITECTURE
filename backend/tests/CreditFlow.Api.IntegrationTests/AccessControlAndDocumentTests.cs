using System.Net;
using System.Net.Http.Json;
using CreditFlow.Api.IntegrationTests.Infrastructure;
using CreditFlow.Application.Applications;
using CreditFlow.Domain.Applications;

namespace CreditFlow.Api.IntegrationTests;

[Collection(SharedApiDefinition.Name)]
public sealed class AccessControlTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task A_client_never_sees_another_client_application()
    {
        var owner = await _api.RegisterClientAsync();
        var intruder = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(owner);
        var document = await ApiDriver.UploadOkAsync(owner, draft.Id, "ID_CARD");

        await ApiDriver.ProblemAsync(await intruder.Http.GetAsync($"/api/v1/applications/{draft.Id}"), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await intruder.Http.PutAsJsonAsync($"/api/v1/applications/{draft.Id}", new { productType = "AUTO", amount = 50_000, durationMonths = 24 }), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await intruder.Http.PostAsync($"/api/v1/applications/{draft.Id}/submit", null), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await intruder.Http.PostAsync($"/api/v1/applications/{draft.Id}/cancel", null), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await ApiDriver.UploadAsync(intruder.Http, draft.Id, "OTHER", ApiDriver.PdfBytes), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await intruder.Http.GetAsync($"/api/v1/applications/{draft.Id}/documents/{document.Id}"), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await intruder.Http.DeleteAsync($"/api/v1/applications/{draft.Id}/documents/{document.Id}"), HttpStatusCode.NotFound);
        (await ApiDriver.PageAsync<ApplicationSummaryDto>(intruder.Http, "/api/v1/applications/mine")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Roles_are_enforced_with_403()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);

        await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/review/applications"), HttpStatusCode.Forbidden);
        await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/dashboard/stats"), HttpStatusCode.Forbidden);
        await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/admin/users"), HttpStatusCode.Forbidden);
        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync("/api/v1/admin/users"), HttpStatusCode.Forbidden);
        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync("/api/v1/admin/products"), HttpStatusCode.Forbidden);
        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync("/api/v1/applications/mine"), HttpStatusCode.Forbidden);
        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync("/api/v1/clients/me/profile"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        using var anonymous = _api.Anonymous();

        await ApiDriver.ProblemAsync(await anonymous.GetAsync("/api/v1/applications/mine"), HttpStatusCode.Unauthorized);
        await ApiDriver.ProblemAsync(await anonymous.GetAsync("/api/v1/review/applications"), HttpStatusCode.Unauthorized);
        await ApiDriver.ProblemAsync(await anonymous.GetAsync($"/api/v1/applications/{Guid.NewGuid()}/documents/{Guid.NewGuid()}"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reviewers_cannot_see_drafts()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var draft = await ApiDriver.CreateDraftAsync(client);

        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync($"/api/v1/review/applications/{draft.Id}"), HttpStatusCode.NotFound);
        await ApiDriver.ProblemAsync(await analyst.Http.PostAsync($"/api/v1/review/applications/{draft.Id}/assign", null), HttpStatusCode.NotFound);
        var queue = await ApiDriver.PageAsync<CreditFlow.Application.Review.ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={draft.Reference}");
        queue.TotalCount.ShouldBe(0);
    }
}

[Collection(SharedApiDefinition.Name)]
public sealed class DocumentTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task Upload_accepts_pdf_and_png_and_detects_the_real_type()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);

        var pdf = await ApiDriver.UploadAsync(client.Http, draft.Id, "ID_CARD", ApiDriver.PdfBytes, "cin.pdf");
        var png = await ApiDriver.UploadAsync(client.Http, draft.Id, "OTHER", ApiDriver.PngBytes, "photo.bin", "application/octet-stream");

        pdf.StatusCode.ShouldBe(HttpStatusCode.Created);
        png.StatusCode.ShouldBe(HttpStatusCode.Created);
        var pngDoc = await ApiDriver.ReadAsync<DocumentDto>(png);
        pngDoc.ContentType.ShouldBe("image/png");
        pngDoc.FileName.ShouldBe("photo.bin");
        pngDoc.SizeBytes.ShouldBe(ApiDriver.PngBytes.Length);
    }

    [Fact]
    public async Task Parallel_uploads_on_the_same_application_all_succeed()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);
        string[] types = ["ID_CARD", "PAYSLIP", "BANK_STATEMENT", "OTHER"];

        var responses = await Task.WhenAll(types.Select(type =>
            ApiDriver.UploadAsync(client.Http, draft.Id, type, ApiDriver.PdfBytes, $"{type.ToLowerInvariant()}.pdf")));

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }

        var detail = await client.Http.GetFromJsonAsync<ApplicationDetailDto>($"/api/v1/applications/{draft.Id}", ApiDriver.Json);
        detail!.Documents.Select(d => d.Type).ShouldBe(
            [DocumentType.IdCard, DocumentType.Payslip, DocumentType.BankStatement, DocumentType.Other], ignoreOrder: true);
    }

    [Fact]
    public async Task Fake_pdf_is_refused_by_its_magic_bytes()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);

        var response = await ApiDriver.UploadAsync(client.Http, draft.Id, "PAYSLIP", "<script>alert(1)</script>"u8.ToArray(), "bulletin.pdf");

        (await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("file", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Files_over_5_MB_are_refused()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);
        var big = new byte[(5 * 1024 * 1024) + 1];
        ApiDriver.PdfBytes.CopyTo(big, 0);

        var response = await ApiDriver.UploadAsync(client.Http, draft.Id, "PAYSLIP", big, "gros.pdf");

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest);
        problem.GetProperty("errors").GetProperty("file")[0].GetString()!.ShouldContain("5 Mo");
    }

    [Fact]
    public async Task Upload_requires_a_valid_document_type()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);

        (await ApiDriver.ProblemAsync(await ApiDriver.UploadAsync(client.Http, draft.Id, null, ApiDriver.PdfBytes), HttpStatusCode.BadRequest))
            .GetProperty("errors").TryGetProperty("type", out _).ShouldBeTrue();
        (await ApiDriver.ProblemAsync(await ApiDriver.UploadAsync(client.Http, draft.Id, "PASSPORT", ApiDriver.PdfBytes), HttpStatusCode.BadRequest))
            .GetProperty("errors").TryGetProperty("type", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Download_returns_the_original_bytes_as_an_attachment()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);
        var document = await ApiDriver.UploadOkAsync(client, draft.Id, "ID_CARD");

        var response = await client.Http.GetAsync($"/api/v1/applications/{draft.Id}/documents/{document.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileName!.Trim('"').ShouldBe("id_card.pdf");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(ApiDriver.PdfBytes);
    }

    [Fact]
    public async Task Stored_files_use_random_names_outside_the_web_root()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);
        await ApiDriver.UploadAsync(client.Http, draft.Id, "OTHER", ApiDriver.PdfBytes, "nom-tres-reconnaissable.pdf");

        var files = Directory.GetFiles(factory.StorageRoot, "*", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty();
        files.ShouldAllBe(f => !f.Contains("reconnaissable", StringComparison.Ordinal) && Path.GetExtension(f) == string.Empty);
    }

    [Fact]
    public async Task Analysts_download_only_once_the_application_is_submitted()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        await ApiDriver.CompleteProfileAsync(client);
        var draft = await ApiDriver.CreateDraftAsync(client);
        var idCard = await ApiDriver.UploadOkAsync(client, draft.Id, "ID_CARD");
        await ApiDriver.UploadOkAsync(client, draft.Id, "PAYSLIP");

        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync($"/api/v1/applications/{draft.Id}/documents/{idCard.Id}"), HttpStatusCode.NotFound);

        (await client.Http.PostAsync($"/api/v1/applications/{draft.Id}/submit", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var response = await analyst.Http.GetAsync($"/api/v1/applications/{draft.Id}/documents/{idCard.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(ApiDriver.PdfBytes);
    }

    [Fact]
    public async Task Documents_can_be_deleted_only_while_editable()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);
        var extra = await ApiDriver.UploadOkAsync(client, draft.Id, "OTHER");

        (await client.Http.DeleteAsync($"/api/v1/applications/{draft.Id}/documents/{extra.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ApiDriver.ProblemAsync(await client.Http.GetAsync($"/api/v1/applications/{draft.Id}/documents/{extra.Id}"), HttpStatusCode.NotFound);

        var submitted = await ApiDriver.SubmittedApplicationAsync(client);
        var locked = submitted.Documents[0];
        var delete = await client.Http.DeleteAsync($"/api/v1/applications/{submitted.Id}/documents/{locked.Id}");
        (await ApiDriver.ProblemAsync(delete, HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("not_editable");
        await ApiDriver.ProblemAsync(await ApiDriver.UploadAsync(client.Http, submitted.Id, "OTHER", ApiDriver.PdfBytes), HttpStatusCode.Conflict);
    }
}
