using CsvHelper;
using NSD;
using System.Globalization;

//var noise = Signals.WhiteNoise(1000000, 50, 1e-9);
var noise = Signals.PowerLawGaussian(100000, 1, 1);

using (StreamWriter writer = new(@$"1nV 1f 50SPS.csv"))
using (CsvWriter csvWrite = new(writer, CultureInfo.InvariantCulture))
{
    foreach (var sample in noise.Span)
    {
        csvWrite.WriteField(sample);
        csvWrite.NextRecord();
    }
}