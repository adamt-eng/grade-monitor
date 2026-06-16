using Grade_Monitor.Discord_App;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

/// <summary>
/// Reads the faculty's self-hosted image CAPTCHA (six randomly-coloured digits over
/// coloured noise lines). The digits are the darkest pixels, so an Otsu threshold
/// isolates them and drops the lighter noise; connected-component analysis then keeps
/// the digit-shaped blobs. We only accept a CAPTCHA that yields exactly six clean
/// components, normalise each digit to a uniform height, pack them tightly into a
/// single printed-looking number, and send that to ocr.space. Returns null whenever
/// the read is not a confident six digits - the caller simply fetches a fresh CAPTCHA
/// (which is free and unlimited) and tries again, so we never submit a shaky answer
/// against the rate-limited login endpoint.
/// </summary>
internal static class ImageCaptchaSolver
{
    private const int DigitCount = 6;

    // Packing layout (validated against ocr.space Engine 2).
    private const int DigitHeight = 50;
    private const int Gap = 14;
    private const int Margin = 30;

    private static readonly HttpClient OcrHttp = new() { BaseAddress = new Uri("https://api.ocr.space/") };

    internal static async Task<string?> SolveAsync(byte[] imageBytes, ulong discordUserId)
    {
        var packed = TryBuildPackedImage(imageBytes);
        if (packed == null)
            return null;

        var text = await RecognizeAsync(packed);
        var digits = new string((text ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.Length != DigitCount)
            return null;

        LoggingService.WriteLog($"{discordUserId}: OCR read CAPTCHA as '{digits}'", ConsoleColor.DarkGreen);
        return digits;
    }

    // Isolates the six digits and returns a clean black-on-white PNG, or null when the
    // image doesn't cleanly yield exactly six digit-shaped components.
    private static byte[]? TryBuildPackedImage(byte[] imageBytes)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        var width = image.Width;
        var height = image.Height;

        // Grayscale (luminance).
        var gray = new byte[width * height];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    gray[(y * width) + x] = (byte)((0.299 * p.R) + (0.587 * p.G) + (0.114 * p.B));
                }
            }
        });

        // Otsu threshold: digits are the darkest pixels, so foreground = value <= threshold.
        var threshold = OtsuThreshold(gray);
        var foreground = new bool[width * height];
        for (var i = 0; i < gray.Length; i++)
            foreground[i] = gray[i] <= threshold;

        var components = FindComponents(foreground, width, height)
            .Where(IsDigitShaped)
            .OrderBy(c => c.MinX)
            .ToList();

        if (components.Count != DigitCount)
            return null;

        return Pack(components, foreground, width);
    }

    private static int OtsuThreshold(byte[] gray)
    {
        var histogram = new int[256];
        foreach (var value in gray)
            histogram[value]++;

        long total = gray.Length;
        long sum = 0;
        for (var i = 0; i < 256; i++)
            sum += (long)i * histogram[i];

        long sumB = 0;
        long weightB = 0;
        double maxVariance = -1;
        var threshold = 0;

        for (var t = 0; t < 256; t++)
        {
            weightB += histogram[t];
            if (weightB == 0)
                continue;

            var weightF = total - weightB;
            if (weightF == 0)
                break;

            sumB += (long)t * histogram[t];
            var meanB = (double)sumB / weightB;
            var meanF = (double)(sum - sumB) / weightF;
            var between = (double)weightB * weightF * (meanB - meanF) * (meanB - meanF);

            if (between > maxVariance)
            {
                maxVariance = between;
                threshold = t;
            }
        }

        return threshold;
    }

    // 8-connected component labelling via iterative flood fill.
    private static List<Component> FindComponents(bool[] foreground, int width, int height)
    {
        var visited = new bool[foreground.Length];
        var stack = new Stack<int>();
        var components = new List<Component>();

        for (var start = 0; start < foreground.Length; start++)
        {
            if (!foreground[start] || visited[start])
                continue;

            visited[start] = true;
            stack.Push(start);
            var component = new Component
            {
                MinX = int.MaxValue,
                MinY = int.MaxValue,
                MaxX = int.MinValue,
                MaxY = int.MinValue
            };

            while (stack.Count > 0)
            {
                var index = stack.Pop();
                var x = index % width;
                var y = index / width;

                component.Area++;
                if (x < component.MinX) component.MinX = x;
                if (x > component.MaxX) component.MaxX = x;
                if (y < component.MinY) component.MinY = y;
                if (y > component.MaxY) component.MaxY = y;

                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= height)
                        continue;

                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        if (nx < 0 || nx >= width)
                            continue;

                        var neighbour = (ny * width) + nx;
                        if (foreground[neighbour] && !visited[neighbour])
                        {
                            visited[neighbour] = true;
                            stack.Push(neighbour);
                        }
                    }
                }
            }

            components.Add(component);
        }

        return components;
    }

    // Heuristics that separate digits from leftover dot noise and thin diagonal lines.
    private static bool IsDigitShaped(Component c)
    {
        var w = c.Width;
        var h = c.Height;
        var aspect = (double)w / h;
        var fill = (double)c.Area / (w * h);

        return c.Area >= 12
            && h is >= 6 and <= 45
            && w <= 32
            && aspect is > 0.12 and < 3.0
            && fill >= 0.20;
    }

    private static byte[] Pack(List<Component> components, bool[] foreground, int width)
    {
        var glyphs = new List<Image<L8>>(components.Count);
        try
        {
            foreach (var c in components)
            {
                var w = c.Width;
                var h = c.Height;
                var glyph = new Image<L8>(w, h);
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                        glyph[x, y] = foreground[((c.MinY + y) * width) + c.MinX + x] ? new L8(255) : new L8(0);

                var newWidth = Math.Max(1, (int)Math.Round((double)w * DigitHeight / h));
                glyph.Mutate(ctx => ctx.Resize(newWidth, DigitHeight, KnownResamplers.Bicubic));
                glyphs.Add(glyph);
            }

            var totalWidth = (Margin * 2) + glyphs.Sum(g => g.Width) + (Gap * (glyphs.Count - 1));
            var totalHeight = DigitHeight + (Margin * 2);

            var canvas = new Image<L8>(totalWidth, totalHeight, new L8(255)); // white background

            var offsetX = Margin;
            foreach (var glyph in glyphs)
            {
                for (var y = 0; y < glyph.Height; y++)
                    for (var x = 0; x < glyph.Width; x++)
                        if (glyph[x, y].PackedValue > 127)
                            canvas[offsetX + x, Margin + y] = new L8(0); // black digit on white

                offsetX += glyph.Width + Gap;
            }

            using (canvas)
            {
                using var ms = new MemoryStream();
                canvas.SaveAsPng(ms);
                return ms.ToArray();
            }
        }
        finally
        {
            foreach (var glyph in glyphs)
                glyph.Dispose();
        }
    }

    private static async Task<string?> RecognizeAsync(byte[] pngBytes)
    {
        var apiKey = DiscordApp.AppConfig.OcrSpaceApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("ocr.space API key is not configured.");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("2"), "OCREngine");
        form.Add(new StringContent("true"), "scale");
        form.Add(new StringContent("eng"), "language");
        form.Add(new StringContent("false"), "isOverlayRequired");

        var file = new ByteArrayContent(pngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "captcha.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, "parse/image");
        request.Content = form;
        request.Headers.Add("apikey", apiKey);

        using var response = await OcrHttp.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(body);

        // A top-level "error" means the service rejected the request (e.g. rate limit or
        // quota exhausted) - surface it rather than silently retrying against a dead key.
        var error = json["error"];
        if (error != null)
            throw new Exception($"ocr.space error: {error}");

        if (json.Value<bool>("IsErroredOnProcessing"))
            return null;

        return json["ParsedResults"]?.FirstOrDefault()?["ParsedText"]?.ToString();
    }

    private sealed class Component
    {
        internal int MinX;
        internal int MinY;
        internal int MaxX;
        internal int MaxY;
        internal int Area;

        internal int Width => MaxX - MinX + 1;
        internal int Height => MaxY - MinY + 1;
    }
}
