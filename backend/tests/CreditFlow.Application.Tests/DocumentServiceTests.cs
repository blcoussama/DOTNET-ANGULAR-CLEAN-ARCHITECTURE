using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Application.Documents;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CreditFlow.Application.Tests;

public sealed class DocumentServiceTests
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8, .. new byte[100]];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private readonly Guid _clientId = Guid.NewGuid();
    private readonly ICreditApplicationRepository _applications = Substitute.For<ICreditApplicationRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _storage.SaveAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("2026/10/0123456789abcdef0123456789abcdef");
        _service = new DocumentService(_applications, _storage, _unitOfWork, new FixedTimeProvider(TestData.Now), NullLogger<DocumentService>.Instance);
    }

    private CreditApplication Given(CreditApplication application)
    {
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        return application;
    }

    [Theory]
    [InlineData("pdf", FileSignatures.Pdf)]
    [InlineData("png", FileSignatures.Png)]
    [InlineData("jpeg", FileSignatures.Jpeg)]
    public async Task Upload_detects_the_real_type_from_magic_bytes(string kind, string expected)
    {
        var application = Given(TestData.Draft(_clientId));
        var bytes = kind switch { "pdf" => Pdf, "png" => Png, _ => Jpeg };

        var document = await _service.UploadAsync(_clientId, application.Id, DocumentType.IdCard, "scan.bin", new MemoryStream(bytes), default);

        document.ContentType.ShouldBe(expected);
        document.SizeBytes.ShouldBe(bytes.Length);
        document.FileName.ShouldBe("scan.bin");
        application.Documents.Count.ShouldBe(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_refuses_a_fake_pdf()
    {
        var application = Given(TestData.Draft(_clientId));

        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.UploadAsync(_clientId, application.Id, DocumentType.IdCard, "faux.pdf", new MemoryStream("<html>pas un pdf</html>"u8.ToArray()), default));

        ex.Errors.ShouldContain(e => e.PropertyName == "file");
        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_refuses_more_than_5_MB()
    {
        var application = Given(TestData.Draft(_clientId));
        var tooLarge = new byte[ApplicationDocument.MaxSizeBytes + 1];
        "%PDF-"u8.CopyTo(tooLarge);

        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.UploadAsync(_clientId, application.Id, DocumentType.Payslip, "gros.pdf", new MemoryStream(tooLarge), default));
        ex.Errors.Single().ErrorMessage.ShouldContain("5 Mo");
    }

    [Fact]
    public async Task Upload_accepts_exactly_5_MB()
    {
        var application = Given(TestData.Draft(_clientId));
        var max = new byte[ApplicationDocument.MaxSizeBytes];
        "%PDF-"u8.CopyTo(max);

        var document = await _service.UploadAsync(_clientId, application.Id, DocumentType.Payslip, "max.pdf", new MemoryStream(max), default);
        document.SizeBytes.ShouldBe(ApplicationDocument.MaxSizeBytes);
    }

    [Fact]
    public async Task Upload_requires_a_type_and_a_non_empty_file()
    {
        var application = Given(TestData.Draft(_clientId));

        (await Should.ThrowAsync<ValidationException>(() => _service.UploadAsync(_clientId, application.Id, null, "a.pdf", new MemoryStream(Pdf), default)))
            .Errors.ShouldContain(e => e.PropertyName == "type");
        (await Should.ThrowAsync<ValidationException>(() => _service.UploadAsync(_clientId, application.Id, DocumentType.Other, "a.pdf", new MemoryStream(), default)))
            .Errors.ShouldContain(e => e.PropertyName == "file");
        (await Should.ThrowAsync<ValidationException>(() => _service.UploadAsync(_clientId, application.Id, DocumentType.Other, "a.pdf", null, default)))
            .Errors.ShouldContain(e => e.PropertyName == "file");
    }

    [Fact]
    public async Task Upload_on_someone_else_application_is_not_found()
    {
        var application = Given(TestData.Draft(Guid.NewGuid()));

        await Should.ThrowAsync<NotFoundException>(() =>
            _service.UploadAsync(_clientId, application.Id, DocumentType.IdCard, "a.pdf", new MemoryStream(Pdf), default));
    }

    [Fact]
    public async Task Upload_on_a_submitted_application_is_a_conflict()
    {
        var application = Given(TestData.Submitted(_clientId));

        await Should.ThrowAsync<ConflictException>(() =>
            _service.UploadAsync(_clientId, application.Id, DocumentType.Other, "a.pdf", new MemoryStream(Pdf), default));
    }

    [Fact]
    public async Task Stored_file_is_deleted_when_saving_fails()
    {
        var application = Given(TestData.Draft(_clientId));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictException("conflit"));

        await Should.ThrowAsync<ConflictException>(() =>
            _service.UploadAsync(_clientId, application.Id, DocumentType.IdCard, "a.pdf", new MemoryStream(Pdf), default));
        await _storage.Received(1).DeleteAsync("2026/10/0123456789abcdef0123456789abcdef", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_upload_conflict_is_retried_on_the_reloaded_application()
    {
        var stale = TestData.Draft(_clientId);
        var reloaded = TestData.Draft(_clientId);
        _applications.GetAsync(stale.Id, Arg.Any<CancellationToken>()).Returns(stale, reloaded);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(
            _ => throw new ConflictException("conflit", "concurrency_conflict"),
            _ => Task.CompletedTask);

        var document = await _service.UploadAsync(_clientId, stale.Id, DocumentType.Payslip, "b.pdf", new MemoryStream(Pdf), default);

        reloaded.Documents.ShouldHaveSingleItem().Id.ShouldBe(document.Id);
        _unitOfWork.Received(1).DiscardChanges();
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Persistent_concurrency_conflict_gives_up_and_deletes_the_stored_file()
    {
        var application = Given(TestData.Draft(_clientId));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictException("conflit", "concurrency_conflict"));

        await Should.ThrowAsync<ConflictException>(() =>
            _service.UploadAsync(_clientId, application.Id, DocumentType.IdCard, "a.pdf", new MemoryStream(Pdf), default));
        await _unitOfWork.Received(5).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _storage.Received(1).DeleteAsync("2026/10/0123456789abcdef0123456789abcdef", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reviewer_can_download_from_a_submitted_application()
    {
        var application = Given(TestData.Submitted(_clientId));
        var document = application.Documents.First();
        _storage.OpenReadAsync(document.StorageKey, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Pdf));

        var content = await _service.OpenAsync(new CurrentActor(Guid.NewGuid(), Roles.Analyst), application.Id, document.Id, default);

        content.ContentType.ShouldBe("application/pdf");
        content.FileName.ShouldBe(document.FileName);
    }

    [Fact]
    public async Task Reviewer_cannot_download_from_a_draft()
    {
        var application = Given(TestData.DraftWithDocuments(_clientId));

        await Should.ThrowAsync<NotFoundException>(() =>
            _service.OpenAsync(new CurrentActor(Guid.NewGuid(), Roles.Analyst), application.Id, application.Documents.First().Id, default));
    }

    [Fact]
    public async Task Another_client_cannot_download()
    {
        var application = Given(TestData.Submitted(_clientId));

        await Should.ThrowAsync<NotFoundException>(() =>
            _service.OpenAsync(new CurrentActor(Guid.NewGuid(), Roles.Client), application.Id, application.Documents.First().Id, default));
    }

    [Fact]
    public async Task Delete_removes_the_document_then_the_file()
    {
        var application = Given(TestData.DraftWithDocuments(_clientId));
        var document = application.Documents.First();

        await _service.DeleteAsync(_clientId, application.Id, document.Id, default);

        application.Documents.ShouldNotContain(document);
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _storage.DeleteAsync(document.StorageKey, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Delete_on_a_submitted_application_is_a_conflict()
    {
        var application = Given(TestData.Submitted(_clientId));

        await Should.ThrowAsync<DomainConflictException>(() =>
            _service.DeleteAsync(_clientId, application.Id, application.Documents.First().Id, default));
    }

    [Theory]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, FileSignatures.Pdf)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, FileSignatures.Jpeg)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, FileSignatures.Png)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, null)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, null)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, null)]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, null)]
    [InlineData(new byte[0], null)]
    public void FileSignatures_detects_only_pdf_jpeg_and_png(byte[] header, string? expected) =>
        FileSignatures.Detect(header).ShouldBe(expected);
}
