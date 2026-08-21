using System.Globalization;
using System.Text;
using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

public static class TowNoticePdf
{
    public static byte[] Build(TowingRequest tow)
    {
        var title = tow.RequestType == "repossession" ? "Vehicle Repossession / Tow Notice" : "Private Property Tow Notice";
        var lines = new List<string>
        {
            "CLT++ - UNOFFICIAL DEMONSTRATION",
            title,
            "",
            $"Date: {tow.CreatedAt.LocalDateTime:MMMM d, yyyy}",
            $"Tracking number: {tow.TrackingNumber}",
            "",
            string.IsNullOrWhiteSpace(tow.OwnerName) ? "Vehicle owner: Owner information unavailable" : $"Vehicle owner: {tow.OwnerName}",
            string.IsNullOrWhiteSpace(tow.OwnerAddress) ? "Owner mailing address: Not available from an authorized source" : $"Owner mailing address: {tow.OwnerAddress}",
            "",
            "Vehicle:",
            $"  {JoinVehicle(tow)}",
            $"  Plate: {tow.PlateState} {tow.LicensePlate}",
            $"  VIN: {tow.Vin}",
            "",
            $"The vehicle was removed from: {tow.TowFromAddress}",
            $"The vehicle is stored at: {tow.StorageAddress}",
            "",
            "Towing company:",
            $"  {tow.TowingCompanyName}",
            $"  Contact: {tow.TowingCompanyContactName}",
            $"  Phone: {tow.TowingCompanyPhone}",
            $"  Email: {tow.TowingCompanyEmail}",
            $"  Address: {tow.TowingCompanyAddress}",
            string.IsNullOrWhiteSpace(tow.TowingPermitNumber) ? "" : $"  Permit / registration: {tow.TowingPermitNumber}",
            "",
            string.IsNullOrWhiteSpace(tow.PropertyOwnerOrLienholder) ? "" : $"Authorizing property owner / lienholder: {tow.PropertyOwnerOrLienholder}",
            string.IsNullOrWhiteSpace(tow.AuthorizationReference) ? "" : $"Authorization reference: {tow.AuthorizationReference}",
            "",
            tow.RequestType == "repossession"
                ? "This notice states that the vehicle identified above was reported as repossessed/towed by the business listed above."
                : "This notice states that the vehicle identified above was reported as removed from private property by the towing business listed above.",
            "",
            "Owner information, when present, must come from a lawful and authorized source. DMV personal information is protected by federal and state privacy law.",
            "CLT++ does not itself provide DMV access and this document is not an NCDMV, court, law-enforcement, or City of Charlotte document."
        }.Where(x => x is not null).ToList()!;

        var wrapped = lines.SelectMany(x => Wrap(x, 92)).ToList();
        return BuildOnePagePdf(wrapped);
    }

    private static string JoinVehicle(TowingRequest t)
    {
        var parts = new[] { t.Year, t.Make, t.Model, t.BodyType, t.Color }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" ", parts);
    }

    private static IEnumerable<string> Wrap(string line, int width)
    {
        if (string.IsNullOrEmpty(line)) { yield return ""; yield break; }
        var current = new StringBuilder();
        foreach (var word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > width)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static byte[] BuildOnePagePdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("50 742 Td");
        var first = true;
        foreach (var raw in lines.Take(56))
        {
            if (!first) content.AppendLine("0 -13 Td");
            first = false;
            content.Append('(').Append(Escape(raw)).AppendLine(") Tj");
        }
        content.AppendLine("ET");
        var stream = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new List<byte[]>();
        objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
        objects.Add(Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"));
        objects.Add(Concat(Ascii($"<< /Length {stream.Length} >>\nstream\n"), stream, Ascii("\nendstream")));
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        using var ms = new MemoryStream();
        Write(ms, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write(ms, $"{i + 1} 0 obj\n");
            ms.Write(objects[i]);
            Write(ms, "\nendobj\n");
        }
        var xref = ms.Position;
        Write(ms, $"xref\n0 {objects.Count + 1}\n");
        Write(ms, "0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++) Write(ms, $"{offsets[i].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        Write(ms, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\r", " ").Replace("\n", " ");
    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
    private static byte[] Concat(params byte[][] parts) { using var ms = new MemoryStream(); foreach (var p in parts) ms.Write(p); return ms.ToArray(); }
    private static void Write(Stream stream, string value) { var bytes = Ascii(value); stream.Write(bytes); }
}
