public class Spectrum
{
    public Memory<double> Frequencies { get; set; }
    public Memory<double> Values { get; set; }
    public int Averages { get; set; }
    public int Stacking { get; set; } = 1;

    public static Spectrum FromValues(Memory<double> values, double sampleRate, int averages)
    {
        int length = (values.Length / 2);
        Memory<double> frequencies = new double[length];
        double dT = (sampleRate / values.Length);
        for (int i = 0; i < length; i++)
        {
            frequencies.Span[i] = i * dT;
        }
        return new Spectrum() { Frequencies = frequencies, Values = values.Slice(0, length), Averages = averages };
    }

    bool trimmedDC = false;
    public void TrimDC()
    {
        if (!trimmedDC)
        {
            trimmedDC = true;
            Frequencies = Frequencies[1..];
            Values = Values[1..];
        }
    }

    int trimmedStartEndBins = 0;
    public void TrimStartEnd(int bins)
    {
        if (trimmedStartEndBins != 0)
            throw new Exception($"TrimStartEnd already called with bins: {trimmedStartEndBins}");
        trimmedStartEndBins = bins;
        Frequencies = Frequencies.Slice(bins, Frequencies.Length - bins * 2);
        Values = Values.Slice(bins, Values.Length - bins * 2);
    }
}