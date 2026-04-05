using System;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace MusicPlayerApp.Audio;

public class VisualizerService
{
    readonly float[] _buffer = new float[8192];
    int _writeIndex;
    readonly object _lock = new();

    public void AddSamples(float[] samples)
    {
        lock (_lock)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float v = samples[i];
                if (float.IsNaN(v) || float.IsInfinity(v))
                    v = 0;

                _buffer[_writeIndex] = v;
                _writeIndex++;
                if (_writeIndex >= _buffer.Length)
                    _writeIndex = 0;
            }
        }
    }

    public double[] GetSpectrum(int barCount)
    {
        int fftSize = 2048;
        Complex[] fft = new Complex[fftSize];

        lock (_lock)
        {
            int idx = _writeIndex;
            for (int i = 0; i < fftSize; i++)
            {
                idx--;
                if (idx < 0)
                    idx = _buffer.Length - 1;
                float sample = _buffer[idx];
                double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (fftSize - 1)));
                fft[i] = new Complex(sample * window, 0);
            }
        }

        Fourier.Forward(fft, FourierOptions.Matlab);

        int usableBins = fftSize / 2;
        if (barCount > usableBins)
            barCount = usableBins;

        double[] bars = new double[barCount];
        int binsPerBar = usableBins / barCount;
        if (binsPerBar < 1)
            binsPerBar = 1;

        for (int b = 0; b < barCount; b++)
        {
            int start = b * binsPerBar;
            int end = Math.Min(usableBins, start + binsPerBar);
            double sum = 0;
            int count = 0;

            for (int i = start; i < end; i++)
            {
                double mag = fft[i].Magnitude;
                sum += mag;
                count++;
            }

            double avg = count > 0 ? sum / count : 0;
            double db = avg > 0 ? 20 * Math.Log10(avg) : -120;
            double norm = (db + 80) / 80;
            if (norm < 0) norm = 0;
            if (norm > 1) norm = 1;
            bars[b] = norm;
        }

        return bars;
    }
}
