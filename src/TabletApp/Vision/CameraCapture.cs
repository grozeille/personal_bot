using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace TabletApp.Vision;

/// <summary>Orientation de caméra demandée.</summary>
internal enum CameraFacing
{
    Front,
    Back,
}

/// <summary>Description d'une caméra vidéo détectée.</summary>
internal sealed record CameraDeviceInfo(string Id, string Name, CameraFacing? Facing);

/// <summary>Résultat d'une capture photo.</summary>
internal sealed record CapturedPhoto(byte[] Jpeg, string DeviceName);

/// <summary>
/// Capture photo ponctuelle via Windows MediaCapture + MediaFrameReader (sans flux permanent).
/// Ouvre la caméra vidéo seule (sans toucher au micro), laisse l'exposition auto converger,
/// applique un rattrapage gamma si la scène est sombre, redimensionne via SkiaSharp et libère la caméra.
/// </summary>
internal static class CameraCapture
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Liste les caméras vidéo disponibles sur la machine.</summary>
    public static async Task<IReadOnlyList<CameraDeviceInfo>> ListCamerasAsync(CancellationToken ct = default)
    {
        DeviceInformationCollection devices =
            await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(ct).ConfigureAwait(false);
        var result = new List<CameraDeviceInfo>(devices.Count);
        foreach (DeviceInformation dev in devices)
        {
            CameraFacing? facing = dev.EnclosureLocation?.Panel switch
            {
                Windows.Devices.Enumeration.Panel.Front => CameraFacing.Front,
                Windows.Devices.Enumeration.Panel.Back => CameraFacing.Back,
                _ => null,
            };
            result.Add(new CameraDeviceInfo(dev.Id, dev.Name, facing));
        }
        return result;
    }

    /// <summary>
    /// Sélectionne la caméra la plus adaptée (filtre nom optionnel, sinon panneau demandé, sinon première disponible).
    /// </summary>
    internal static CameraDeviceInfo? SelectDevice(
        IReadOnlyList<CameraDeviceInfo> devices, CameraFacing facing, string? preferredName = null)
    {
        if (devices.Count == 0)
        {
            return null;
        }
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            CameraDeviceInfo? byName = devices.FirstOrDefault(
                d => d.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }
        return devices.FirstOrDefault(d => d.Facing == facing) ?? devices[0];
    }

    /// <summary>Nom de caméra préféré dans appsettings.json ("Camera":"Preferred" ou "Front"/"Back").</summary>
    internal static string? ResolvePreferredName(CameraFacing facing, string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return null;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Camera", out JsonElement cam))
            {
                return null;
            }
            string key = facing == CameraFacing.Back ? "Back" : "Front";
            if (cam.TryGetProperty(key, out JsonElement specific) && specific.GetString() is { Length: > 0 } s)
            {
                return s;
            }
            if (cam.TryGetProperty("Preferred", out JsonElement pref) && pref.GetString() is { Length: > 0 } p)
            {
                return p;
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// Ouvre la caméra, capture une photo JPEG non noire (redimensionnée à <paramref name="maxDimension"/> px max)
    /// et referme la caméra.
    /// </summary>
    public static async Task<CapturedPhoto> CaptureJpegWithInfoAsync(
        CameraFacing facing = CameraFacing.Front,
        int maxDimension = 1024,
        int quality = 85,
        string? preferredName = null,
        CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IReadOnlyList<CameraDeviceInfo> devices = await ListCamerasAsync(ct).ConfigureAwait(false);
            string? preferred = preferredName ?? ResolvePreferredName(facing);
            CameraDeviceInfo selected = SelectDevice(devices, facing, preferred)
                ?? throw new InvalidOperationException("Aucune caméra vidéo détectée.");

            using var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = selected.Id,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            };
            await capture.InitializeAsync(settings).AsTask(ct).ConfigureAwait(false);

            byte[]? jpeg = await TryCaptureViaFrameReaderAsync(capture, maxDimension, quality, ct)
                .ConfigureAwait(false);
            if (jpeg is null)
            {
                jpeg = await CaptureViaPhotoStreamAsync(capture, maxDimension, quality, ct)
                    .ConfigureAwait(false);
            }
            return new CapturedPhoto(jpeg, selected.Name);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<byte[]> CaptureJpegAsync(
        CameraFacing facing = CameraFacing.Front,
        int maxDimension = 1024,
        int quality = 85,
        CancellationToken ct = default)
    {
        CapturedPhoto result = await CaptureJpegWithInfoAsync(facing, maxDimension, quality, null, ct)
            .ConfigureAwait(false);
        return result.Jpeg;
    }

    private sealed record RawFrame(byte[] BgraPixels, int Width, int Height, double AvgLuma);

    private static async Task<byte[]?> TryCaptureViaFrameReaderAsync(
        MediaCapture capture, int maxDimension, int quality, CancellationToken ct)
    {
        MediaFrameSource? source = capture.FrameSources.Values
            .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);
        if (source is null)
        {
            return null;
        }

        using var reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8)
            .AsTask(ct).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<RawFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = DateTime.UtcNow;
        int framesSeen = 0;
        RawFrame? lastFrame = null;

        reader.FrameArrived += (sender, _) =>
        {
            if (tcs.Task.IsCompleted)
            {
                return;
            }
            using MediaFrameReference? frame = sender.TryAcquireLatestFrame();
            using SoftwareBitmap? sb = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (sb is null)
            {
                return;
            }
            framesSeen++;
            using SoftwareBitmap bgra = sb.BitmapPixelFormat == BitmapPixelFormat.Bgra8
                ? sb
                : SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            int w = bgra.PixelWidth;
            int h = bgra.PixelHeight;
            byte[] pixels = new byte[w * h * 4];
            bgra.CopyToBuffer(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(pixels));
            double avgLuma = MeasureAverageLuma(pixels);
            var raw = new RawFrame(pixels, w, h, avgLuma);
            lastFrame = raw;

            // Laisse l'exposition auto converger (~12 frames ou >= 700 ms).
            bool warmedUp = framesSeen >= 12 || (framesSeen >= 4 && (DateTime.UtcNow - started).TotalMilliseconds >= 700);
            if (warmedUp && avgLuma >= 4.0)
            {
                tcs.TrySetResult(raw);
            }
            else if (framesSeen >= 30)
            {
                tcs.TrySetResult(raw);
            }
        };

        MediaFrameReaderStartStatus status = await reader.StartAsync().AsTask(ct).ConfigureAwait(false);
        if (status != MediaFrameReaderStartStatus.Success)
        {
            return null;
        }

        RawFrame? chosen = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var reg = timeout.Token.Register(() =>
            {
                if (lastFrame is not null)
                {
                    tcs.TrySetResult(lastFrame);
                }
                else
                {
                    tcs.TrySetCanceled(timeout.Token);
                }
            });
            chosen = await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            chosen = lastFrame;
        }
        finally
        {
            await reader.StopAsync().AsTask(CancellationToken.None).ConfigureAwait(false);
        }

        return chosen is not null
            ? EncodeBgraToJpeg(chosen.BgraPixels, chosen.Width, chosen.Height, chosen.AvgLuma, maxDimension, quality)
            : null;
    }

    private static async Task<byte[]> CaptureViaPhotoStreamAsync(
        MediaCapture capture, int maxDimension, int quality, CancellationToken ct)
    {
        await Task.Delay(500, ct).ConfigureAwait(false);
        using var stream = new InMemoryRandomAccessStream();
        await capture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream)
            .AsTask(ct).ConfigureAwait(false);
        stream.Seek(0);
        byte[] raw = new byte[stream.Size];
        await stream.ReadAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(raw),
            (uint)stream.Size, InputStreamOptions.None).AsTask(ct).ConfigureAwait(false);
        return ResizeAndEncodeJpeg(raw, maxDimension, quality);
    }

    private static byte[]? EncodeBgraToJpeg(
        byte[] pixels, int w, int h, double avgLuma, int maxDimension, int quality)
    {
        ApplyAutoGammaIfDark(pixels, avgLuma);
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        SKBitmap target = bitmap;
        SKBitmap? resized = null;
        try
        {
            int longest = Math.Max(w, h);
            if (maxDimension > 0 && longest > maxDimension)
            {
                float scale = (float)maxDimension / longest;
                int nw = Math.Max(1, (int)Math.Round(w * scale));
                int nh = Math.Max(1, (int)Math.Round(h * scale));
                resized = bitmap.Resize(new SKImageInfo(nw, nh, SKColorType.Bgra8888, SKAlphaType.Premul), SKSamplingOptions.Default);
                if (resized is not null)
                {
                    target = resized;
                }
            }
            using SKData? data = target.Encode(SKEncodedImageFormat.Jpeg, quality);
            return data?.ToArray();
        }
        finally
        {
            resized?.Dispose();
        }
    }

    internal static double MeasureAverageLuma(byte[] bgraPixels)
    {
        if (bgraPixels.Length < 4)
        {
            return 0.0;
        }
        long sum = 0;
        int samples = 0;
        int step = Math.Max(4, (bgraPixels.Length / 4 / 250) * 4);
        for (int i = 0; i + 2 < bgraPixels.Length; i += step)
        {
            sum += bgraPixels[i] + bgraPixels[i + 1] + bgraPixels[i + 2];
            samples += 3;
        }
        return samples == 0 ? 0.0 : (double)sum / samples;
    }

    /// <summary>
    /// Éclaircit les ombres (LUT gamma) si la luminosité moyenne est sous-exposée (&lt; 85/255).
    /// </summary>
    internal static void ApplyAutoGammaIfDark(byte[] bgraPixels, double avgLuma)
    {
        if (avgLuma <= 12.0 || avgLuma >= 85.0)
        {
            return;
        }
        double gamma = Math.Clamp(Math.Log(100.0 / 255.0) / Math.Log(avgLuma / 255.0), 0.45, 1.0);
        if (gamma >= 0.98)
        {
            return;
        }
        var lut = new byte[256];
        for (int v = 0; v < 256; v++)
        {
            lut[v] = (byte)Math.Clamp((int)Math.Round(255.0 * Math.Pow(v / 255.0, gamma)), 0, 255);
        }
        for (int i = 0; i + 2 < bgraPixels.Length; i += 4)
        {
            bgraPixels[i] = lut[bgraPixels[i]];
            bgraPixels[i + 1] = lut[bgraPixels[i + 1]];
            bgraPixels[i + 2] = lut[bgraPixels[i + 2]];
        }
    }

    /// <summary>
    /// Redimensionne un JPEG si son côté maximal dépasse <paramref name="maxDimension"/>.
    /// </summary>
    internal static byte[] ResizeAndEncodeJpeg(byte[] rawJpeg, int maxDimension, int quality)
    {
        if (maxDimension <= 0 || rawJpeg.Length == 0)
        {
            return rawJpeg;
        }
        using var bitmap = SKBitmap.Decode(rawJpeg);
        if (bitmap is null)
        {
            return rawJpeg;
        }
        int longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= maxDimension)
        {
            return rawJpeg;
        }
        float scale = (float)maxDimension / longest;
        int width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        var info = new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType);
        using SKBitmap resized = bitmap.Resize(info, SKSamplingOptions.Default);
        if (resized is null)
        {
            return rawJpeg;
        }
        using SKData encoded = resized.Encode(SKEncodedImageFormat.Jpeg, quality);
        return encoded is not null ? encoded.ToArray() : rawJpeg;
    }
}
