using CsvHelper;
using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Import.Parsers;

public interface ISourceParser
{
    SourceSystem Source { get; }
    IReadOnlyList<string> RequiredHeaders { get; }
    ParsedSale Parse(IReaderRow row);
}
