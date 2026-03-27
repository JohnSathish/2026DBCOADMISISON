using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.ViewModels;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Admissions.Queries.ListClassXiiSubjects;

public sealed class ListClassXiiSubjectsQueryHandler
    : IRequestHandler<ListClassXiiSubjectsQuery, IReadOnlyList<ClassXiiSubjectOptionDto>>
{
    private static readonly HashSet<string> AllowedBoards = new(StringComparer.OrdinalIgnoreCase)
    {
        "MBOSE", "CBSE", "ISC"
    };

    private static readonly HashSet<string> AllowedStreams = new(StringComparer.OrdinalIgnoreCase)
    {
        "ARTS", "SCIENCE", "COMMERCE"
    };

    private readonly IClassXiiSubjectCatalogRepository _catalog;
    private readonly ILogger<ListClassXiiSubjectsQueryHandler> _logger;

    public ListClassXiiSubjectsQueryHandler(
        IClassXiiSubjectCatalogRepository catalog,
        ILogger<ListClassXiiSubjectsQueryHandler> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClassXiiSubjectOptionDto>> Handle(
        ListClassXiiSubjectsQuery request,
        CancellationToken cancellationToken)
    {
        var board = (request.Board ?? string.Empty).Trim();
        var stream = (request.Stream ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(board) || string.IsNullOrEmpty(stream))
        {
            throw new ArgumentException("Board and stream query parameters are required.");
        }

        if (!AllowedBoards.Contains(board))
        {
            throw new ArgumentException("Invalid board. Use MBOSE, CBSE, or ISC.");
        }

        if (!AllowedStreams.Contains(stream))
        {
            throw new ArgumentException("Invalid stream. Use ARTS, SCIENCE, or COMMERCE.");
        }

        var items = await _catalog.ListActiveByBoardAndStreamAsync(board, stream, cancellationToken);
        if (items.Count == 0)
        {
            _logger.LogWarning(
                "Class XII subject catalog is empty for Board={Board} Stream={Stream}. " +
                "Restore data in admissions.subjects_master (e.g. admin replace catalog or scripts/import-subjects-master-from-csv.ps1).",
                board.Trim(),
                stream.Trim());
        }

        return items;
    }
}
