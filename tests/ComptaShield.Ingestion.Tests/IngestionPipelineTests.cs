using System.Text;
using ComptaShield.Ingestion.Commands;
using ComptaShield.Ingestion.Entities;
using ComptaShield.Ingestion.Models;
using ComptaShield.Ingestion.Parsers;
using ComptaShield.Ingestion.Persistence;
using ComptaShield.Ingestion.Storage;
using ComptaShield.Ingestion.Validators;
using ComptaShield.SharedKernel.Enums;
using ComptaShield.SharedKernel.MultiTenancy;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ComptaShield.Ingestion.Tests;

public sealed class IngestionPipelineTests
{
    private static readonly Guid CabinetId = Guid.NewGuid();
    private static readonly Guid DossierId = Guid.NewGuid();
    private static readonly Guid ExerciceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static (IngestionDbContext db, ScopedTenantContext tenant) CreateContext(string dbName)
    {
        var tenant = new ScopedTenantContext
        {
            UserId = UserId,
            CabinetId = CabinetId,
            DossierId = DossierId,
            ExerciceId = ExerciceId,
            Role = DossierRole.Partner,
            IsExerciceLocked = false
        };

        var options = new DbContextOptionsBuilder<IngestionDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new IngestionDbContext(options, tenant), tenant);
    }

    private sealed class FakeWormStorageService : IWormStorageService
    {
        private readonly HashSet<string> _storedKeys = [];

        public Task<string?> StoreInboundFileAsync(
            Guid cabinetId, Guid dossierId, Guid exerciceId,
            Stream fileStream, string sha256Hash, string fileExtension,
            CancellationToken ct = default)
        {
            var key = $"{cabinetId}/{dossierId}/{exerciceId}/inbound/{sha256Hash}.{fileExtension}";
            if (!_storedKeys.Add(key))
            {
                return Task.FromResult<string?>(null); // Duplicate
            }
            return Task.FromResult<string?>(key);
        }

        public Task<string> StoreDeliverableAsync(
            Guid cabinetId, Guid dossierId, Guid exerciceId,
            Stream fileStream, string docType, string version, string sha256Hash,
            CancellationToken ct = default)
        {
            var key = $"{cabinetId}/{dossierId}/{exerciceId}/deliverables/{docType}_{version}_{sha256Hash}.pdf";
            _storedKeys.Add(key);
            return Task.FromResult(key);
        }

        public Task<Stream> GetFileAsync(string objectKey, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("<xml></xml>")));

        public Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult($"https://vault.comptashield.local/{objectKey}?token=fake");
    }

    private sealed class FakePublishEndpoint : IPublishEndpoint
    {
        public List<object> PublishedMessages { get; } = [];

        public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish(object message, CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class
        {
            PublishedMessages.Add(values);
            return Task.CompletedTask;
        }

        public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
        {
            PublishedMessages.Add(values);
            return Task.CompletedTask;
        }

        public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
        {
            PublishedMessages.Add(values);
            return Task.CompletedTask;
        }

        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => throw new NotImplementedException();
    }

    [Fact]
    public async Task UploadDocument_Should_Reject_Duplicate_Sha256()
    {
        var dbName = Guid.NewGuid().ToString();
        var (db, tenant) = CreateContext(dbName);
        var storage = new FakeWormStorageService();
        var publisher = new FakePublishEndpoint();
        var handler = new UploadDocumentCommandHandler(db, storage, publisher, tenant);

        var fileBytes = Encoding.UTF8.GetBytes("Facture N 123456 pour Client ABC");

        // First upload succeeds
        using (var stream1 = new MemoryStream(fileBytes))
        {
            var result1 = await handler.HandleAsync(stream1, "facture_123456.pdf");
            Assert.True(result1.Success);
            Assert.NotNull(result1.DocumentId);
            Assert.Single(publisher.PublishedMessages);
        }

        // Second upload of identical content fails with duplicate message
        using (var stream2 = new MemoryStream(fileBytes))
        {
            var result2 = await handler.HandleAsync(stream2, "facture_123456_copie.pdf");
            Assert.False(result2.Success);
            Assert.Contains("Duplicate", result2.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TeifXmlInvoiceParser_Should_Parse_All_Fields_Correctly()
    {
        var parser = new TeifXmlInvoiceParser();
        var xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Invoice>
                <SupplierName>STE TUNISIENNE DE DISTRIBUTION</SupplierName>
                <SupplierMatriculeFiscal>1234567/A/M/000</SupplierMatriculeFiscal>
                <InvoiceNumber>FAC-2026-0891</InvoiceNumber>
                <InvoiceDate>2026-03-15</InvoiceDate>
                <DueDate>2026-04-15</DueDate>
                <DigitalSignature>TUNTRUST_CERT_SHA256_VALID</DigitalSignature>
                <Lines>
                    <Line>
                        <Description>Serveur Dell PowerEdge R750</Description>
                        <Quantity>2.0000</Quantity>
                        <UnitPrice>15000.0000</UnitPrice>
                        <Discount>1000.0000</Discount>
                        <TvaRate>19.0000</TvaRate>
                        <LineTotal>29000.0000</LineTotal>
                    </Line>
                    <Line>
                        <Description>Licence Windows Server 2025</Description>
                        <Quantity>2.0000</Quantity>
                        <UnitPrice>2500.0000</UnitPrice>
                        <Discount>0.0000</Discount>
                        <TvaRate>19.0000</TvaRate>
                        <LineTotal>5000.0000</LineTotal>
                    </Line>
                </Lines>
                <TotalHT>34000.0000</TotalHT>
                <TotalTVA>6460.0000</TotalTVA>
                <TimbreFiscal>1.0000</TimbreFiscal>
                <TotalTTC>40461.0000</TotalTTC>
                <TvaBreakdown>
                    <TvaLine>
                        <Rate>19.0000</Rate>
                        <TaxableAmount>34000.0000</TaxableAmount>
                        <TaxAmount>6460.0000</TaxAmount>
                    </TvaLine>
                </TvaBreakdown>
            </Invoice>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlContent));
        var invoice = parser.Parse(stream);

        Assert.Equal("STE TUNISIENNE DE DISTRIBUTION", invoice.SupplierName);
        Assert.Equal("1234567/A/M/000", invoice.SupplierMatriculeFiscal);
        Assert.Equal("FAC-2026-0891", invoice.InvoiceNumber);
        Assert.Equal(new DateTime(2026, 3, 15), invoice.InvoiceDate);
        Assert.Equal("TUNTRUST_CERT_SHA256_VALID", invoice.DigitalSignatureInfo);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(34000.0000m, invoice.TotalHT);
        Assert.Equal(6460.0000m, invoice.TotalTVA);
        Assert.Equal(1.0000m, invoice.TimbreFiscal);
        Assert.Equal(40461.0000m, invoice.TotalTTC);
        Assert.Single(invoice.TvaBreakdown);
    }

    [Fact]
    public void Validator_Should_Route_Low_Confidence_To_Human_Review()
    {
        var validator = new DocumentMathAndConfidenceValidator();
        var invoice = new ExtractedInvoiceData
        {
            SupplierName = "Fournisseur Test",
            TotalHT = 100m,
            TotalTVA = 19m,
            TimbreFiscal = 1m,
            TotalTTC = 120m,
            OverallConfidence = 0.88m, // Low confidence (< 0.95)
            OcrFields =
            [
                new OcrFieldResult { FieldName = "TotalTTC", Value = "120.00", Confidence = 0.88m }
            ]
        };

        var result = validator.Validate(invoice);

        Assert.False(result.IsValid);
        Assert.Equal(DocumentStatus.RequiresHumanReview, result.TargetStatus);
        Assert.Contains(result.Discrepancies, d => d.Contains("threshold"));
    }

    [Fact]
    public void Validator_Should_Route_Math_Discrepancy_To_Human_Review()
    {
        var validator = new DocumentMathAndConfidenceValidator();
        var invoice = new ExtractedInvoiceData
        {
            SupplierName = "Fournisseur Test",
            TotalHT = 100m,
            TotalTVA = 19m,
            TimbreFiscal = 1m,
            TotalTTC = 125m, // Discrepancy! 100 + 19 + 1 = 120, not 125
            OverallConfidence = 0.98m,
            OcrFields =
            [
                new OcrFieldResult { FieldName = "TotalTTC", Value = "125.00", Confidence = 0.98m }
            ]
        };

        var result = validator.Validate(invoice);

        Assert.False(result.IsValid);
        Assert.Equal(DocumentStatus.RequiresHumanReview, result.TargetStatus);
        Assert.Contains(result.Discrepancies, d => d.Contains("Cross-sum failed"));
    }

    [Fact]
    public void Validator_Should_Route_Sound_Invoice_To_ReadyForPosting()
    {
        var validator = new DocumentMathAndConfidenceValidator();
        var invoice = new ExtractedInvoiceData
        {
            SupplierName = "Fournisseur Conforme",
            TotalHT = 1000m,
            TotalTVA = 190m,
            TimbreFiscal = 1m,
            TotalTTC = 1191m,
            OverallConfidence = 0.99m,
            Lines =
            [
                new ExtractedInvoiceLine
                {
                    LineNumber = 1,
                    Quantity = 10m,
                    UnitPrice = 100m,
                    Discount = 0m,
                    LineTotal = 1000m
                }
            ],
            OcrFields =
            [
                new OcrFieldResult { FieldName = "TotalHT", Value = "1000.00", Confidence = 0.99m },
                new OcrFieldResult { FieldName = "TotalTTC", Value = "1191.00", Confidence = 0.98m }
            ]
        };

        var result = validator.Validate(invoice);

        Assert.True(result.IsValid);
        Assert.Equal(DocumentStatus.ReadyForPosting, result.TargetStatus);
        Assert.Empty(result.Discrepancies);
    }
}
