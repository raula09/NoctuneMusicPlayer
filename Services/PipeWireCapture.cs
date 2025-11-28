using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MusicPlayerApp.Audio;

public class PipeWireCapture
{
    readonly string _sourceName;
    readonly Action<float[]> _onSamples;
    Process? _process;
    CancellationTokenSource? _cts;
    Task? _readTask;

    public PipeWireCapture(string sourceName, Action<float[]> onSamples)
    {
        _sourceName = sourceName;
        _onSamples = onSamples;
    }

    public void Start()
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.WriteLine("PipeWire capture is only available on Linux");
            return;
        }

        if (_process != null)
            return;

        _cts = new CancellationTokenSource();

        var psi = new ProcessStartInfo
        {
            FileName = "pw-record",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("--raw");
        psi.ArgumentList.Add("--rate");
        psi.ArgumentList.Add("48000");
        psi.ArgumentList.Add("--channels");
        psi.ArgumentList.Add("2");
        psi.ArgumentList.Add("--format");
        psi.ArgumentList.Add("f32");
        psi.ArgumentList.Add("--target");
        psi.ArgumentList.Add(_sourceName);
        psi.ArgumentList.Add("-");

        try
        {
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Start();
            _readTask = Task.Run(() => ReadLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start PipeWire capture: {ex.Message}");
            _process = null;
        }
    }

    async Task ReadLoop(CancellationToken token)
    {
        if (_process == null)
            return;

        var stream = _process.StandardOutput.BaseStream;
        int frameSizeBytes = sizeof(float) * 2;
        int framesPerChunk = 1024;
        int chunkSize = frameSizeBytes * framesPerChunk;
        byte[] buffer = new byte[chunkSize];
        float[] floatBuffer = new float[framesPerChunk * 2];

        while (!token.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            if (read <= 0)
                break;

            int floatCount = read / sizeof(float);
            if (floatCount == 0)
                continue;

            if (floatBuffer.Length < floatCount)
                floatBuffer = new float[floatCount];

            Buffer.BlockCopy(buffer, 0, floatBuffer, 0, floatCount * sizeof(float));

            float[] copy = new float[floatCount];
            Array.Copy(floatBuffer, copy, floatCount);
            _onSamples(copy);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();

        try
        {
            if (_process != null && !_process.HasExited)
                _process.Kill();
        }
        catch
        {
        }

        _process?.Dispose();
        _process = null;
    }
}