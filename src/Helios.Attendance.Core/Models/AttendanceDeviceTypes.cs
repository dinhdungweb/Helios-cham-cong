namespace Helios.Attendance.Core.Models;

public static class AttendanceDeviceTypes
{
    public const string ZkRonaldJack = "zk";
    public const string Soyal = "soyal";
    public const string MorphoSigma = "morpho_sigma";
    public const string Somac = "somac";
    public const string CsvExcel = "csv_excel";
    public const string HttpApi = "http_api";

    public static IReadOnlyList<DeviceTypeOption> Options { get; } =
    [
        new(ZkRonaldJack, "ZK / Ronald Jack"),
        new(Soyal, "Soyal"),
        new(MorphoSigma, "Morpho Sigma"),
        new(Somac, "Somac"),
        new(CsvExcel, "CSV / Excel"),
        new(HttpApi, "API HTTP")
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ZkRonaldJack;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return Options.Any(option => option.Value == normalized)
            ? normalized
            : ZkRonaldJack;
    }

    public static string GetDisplayName(string? value)
    {
        var normalized = Normalize(value);
        return Options.First(option => option.Value == normalized).Label;
    }
}

public sealed record DeviceTypeOption(string Value, string Label);
