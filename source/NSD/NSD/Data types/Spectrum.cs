public class Spectrum
{
    public Memory<double> Frequencies { get; set; }
    public Memory<double> Values { get; set; }

    public static Spectrum FromValues(Memory<double> values, double sampleRate)
    {
        int length = (values.Length / 2);
        Memory<double> frequencies = new double[length];
        double dT = (sampleRate / values.Length);
        for (int i = 0; i < length; i++)
        {
            frequencies.Span[i] = i * dT;
        }
        return new Spectrum() { Frequencies = frequencies, Values = values.Slice(0, length) };
    }

    public void TrimStartEnd(int length)
    {
        Frequencies = Frequencies.Slice(length, Frequencies.Length - length * 2);
        Values = Values.Slice(length, Values.Length - length * 2);
    }
}