using EME.Diagnostics.Core.Models;
using EME.Diagnostics.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EME.Diagnostics.Reporting;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _repository;

    static ReportService() => QuestPDF.Settings.License = LicenseType.Community;

    public ReportService(IReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> ExportPdfAsync(long reportId, string destinationPath, CancellationToken ct = default)
    {
        var report = await _repository.GetReportAsync(reportId, ct).ConfigureAwait(false);
        if (report == null) throw new InvalidOperationException($"Relatório {reportId} não encontrado.");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var doc = GenerateDocument(report);
        doc.GeneratePdf(destinationPath);
        return destinationPath;
    }

    public async Task ExportAllPdfAsync(string destinationPath, CancellationToken ct = default)
    {
        var reports = await _repository.GetAllReportsAsync(ct).ConfigureAwait(false);
        if (reports.Count == 0) throw new InvalidOperationException("Nenhum relatório salvo.");

        Directory.CreateDirectory(destinationPath);
        foreach (var summary in reports)
        {
            ct.ThrowIfCancellationRequested();
            var report = await _repository.GetReportAsync(summary.Id, ct).ConfigureAwait(false);
            if (report == null) continue;

            var fileName = $"Relatorio_{report.TestType}_{report.CreatedAt:yyyy-MM-dd_HHmmss}.pdf";
            var filePath = Path.Combine(destinationPath, fileName);
            var doc = GenerateDocument(report);
            doc.GeneratePdf(filePath);
        }
    }

    private static Document GenerateDocument(StressReportDetail report)
    {
        bool hasResult = report.Result != "Pendente";
        float angle = hasResult ? (float)(Random.Shared.NextDouble() * 50 - 25) : 0;
        float offsetX = hasResult ? (float)(Random.Shared.NextDouble() * 60 - 30) : 0;
        float offsetY = hasResult ? (float)(Random.Shared.NextDouble() * 30 - 15) : 0;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("E.M.E Diagnostics — Relatório de Stress Test")
                        .SemiBold().FontSize(18).FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"Gerado em: {report.CreatedAt:dd/MM/yyyy HH:mm:ss}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingBottom(8).LineHorizontal(1);
                });

                page.Content().Column(col =>
                {
                    // Summary section
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Informações do Teste").SemiBold().FontSize(13);
                            c.Item().Text($"Tipo: {TestTypeLabel(report.TestType)}");
                            c.Item().Text($"Status: {report.Status}");
                            c.Item().Text($"Duração: {report.Duration:hh\\:mm\\:ss}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Hardware Detectado").SemiBold().FontSize(13);
                            if (report.CpuName != null) c.Item().Text($"CPU: {report.CpuName}");
                            if (report.GpuName != null) c.Item().Text($"GPU: {report.GpuName}");
                            c.Item().Text($"RAM: {report.MemoryTotalGb:F1} GB");
                            if (report.StorageName != null) c.Item().Text($"Storage: {report.StorageName}");
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(1);

                    // Entries grouped by component
                    foreach (var group in report.Entries.GroupBy(e => e.Component))
                    {
                        col.Item().PaddingVertical(4).Text(group.Key).SemiBold().FontSize(12).FontColor(Colors.Blue.Darken2);

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Text("Sensor").SemiBold().FontSize(9);
                                h.Cell().Text("Mín").SemiBold().FontSize(9).AlignRight();
                                h.Cell().Text("Méd").SemiBold().FontSize(9).AlignRight();
                                h.Cell().Text("Máx").SemiBold().FontSize(9).AlignRight();
                                h.Cell().Text("Unid").SemiBold().FontSize(9);
                            });

                            foreach (var entry in group)
                            {
                                t.Cell().Text(entry.SensorName).FontSize(9);
                                t.Cell().Text(entry.MinValue?.ToString("F1") ?? "—").FontSize(9).AlignRight();
                                t.Cell().Text(entry.AvgValue?.ToString("F1") ?? "—").FontSize(9).AlignRight();
                                t.Cell().Text(entry.MaxValue?.ToString("F1") ?? "—").FontSize(9).AlignRight();
                                t.Cell().Text(entry.Unit).FontSize(9);
                            }
                        });
                    }

                    // Result stamp at bottom — rotated like a real stamp
                    if (hasResult)
                    {
                        var isPass = report.Result == "PASS";
                        var color = isPass ? Colors.Green.Darken1 : Colors.Red.Darken1;

                        col.Item().PaddingTop(24).PaddingRight(offsetX + 40).PaddingBottom(offsetY).AlignRight().Element(x =>
                            x.Rotate(angle).Border(3).BorderColor(color).PaddingVertical(10).PaddingHorizontal(14).Column(stamp =>
                            {
                                stamp.Item().Text(report.Result)
                                    .SemiBold().FontSize(22).FontColor(color);
                            }));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado por E.M.E Diagnostics — Página ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    private static string TestTypeLabel(ReportTestType type) => type switch
    {
        ReportTestType.Cpu => "CPU (individual)",
        ReportTestType.Gpu => "GPU (individual)",
        ReportTestType.Memory => "RAM (individual)",
        ReportTestType.Storage => "Storage (individual)",
        ReportTestType.Combined => "Combined (CPU + GPU + RAM + Storage)",
        _ => type.ToString()
    };
}
