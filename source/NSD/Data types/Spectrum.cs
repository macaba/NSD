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

    // It is well known that windowing of data segments is necessary in the WOSA method to reduce the bias
    // of the spectral estimate[14]. When calculating onesided spectral estimates containing only positive
    // Fourier frequencies windowing causes a bias at low frequency bins—a fact that is also well known:
    // one cannot trust the lowest frequency bins on the spectrum analyzer.The bias stems from aliasing of
    // power from negative bins and bin zero to the lowest positive frequency bins.Aliasing from bin zero can
    // be eliminated by subtracting the mean data value from the segment.Aliasing from negative bins however, cannot be reduced that way.Hence we propose
    // not to use the first few frequency bins.The first frequency bin that yields unbiased spectral estimates
    // depends on the window function used.The bin is given by the effective half-width of the window
    // transfer function. 
    int trimmedStartEndBins = 0;
    public void TrimStartEnd(int bins)
    {
        if (trimmedStartEndBins != 0)
            throw new Exception($"TrimStartEnd already called with bins: {trimmedStartEndBins}");
        trimmedStartEndBins = bins;
        Frequencies = Frequencies.Slice(bins, Frequencies.Length - (bins * 2));
        Values = Values.Slice(bins, Values.Length - (bins * 2));
    }
}