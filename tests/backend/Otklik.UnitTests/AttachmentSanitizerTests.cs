using System.Text;
using Otklik.Application.Attachments;
using Otklik.Infrastructure.Attachments;
using StbImageWriteSharp;
using Xunit;

namespace Otklik.UnitTests;

public sealed class AttachmentSanitizerTests
{
    private readonly AttachmentSanitizer _sanitizer = new();

    [Fact]
    public async Task Jpeg_is_decoded_and_reencoded_without_exif_or_gps_markers()
    {
        var source = AddExifSegment(CreateJpeg());
        await using var stream = new MemoryStream(source);

        var sanitized = await _sanitizer.SanitizeAsync(
            stream,
            source.LongLength,
            "photo.jpeg",
            "image/jpeg",
            TestContext.Current.CancellationToken);

        Assert.Equal("image/jpeg", sanitized.ContentType);
        Assert.Equal(".jpg", sanitized.Extension);
        var text = Encoding.Latin1.GetString(sanitized.Content);
        Assert.DoesNotContain("Exif", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GPS", text, StringComparison.Ordinal);
        Assert.True(sanitized.Content.AsSpan(0, 3).SequenceEqual(new byte[] { 0xff, 0xd8, 0xff }));
    }

    [Fact]
    public async Task Content_with_disguised_extension_is_rejected()
    {
        var source = CreateJpeg();
        await using var stream = new MemoryStream(source);

        var exception = await Assert.ThrowsAsync<AttachmentValidationException>(() =>
            _sanitizer.SanitizeAsync(
                stream,
                source.LongLength,
                "photo.png",
                "image/png",
                TestContext.Current.CancellationToken));

        Assert.Contains("не совпадает", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Declared_file_over_ten_megabytes_is_rejected_before_reading()
    {
        await using var stream = new MemoryStream();

        var exception = await Assert.ThrowsAsync<AttachmentValidationException>(() =>
            _sanitizer.SanitizeAsync(
                stream,
                AttachmentSanitizer.MaxFileSize + 1,
                "large.png",
                "image/png",
                TestContext.Current.CancellationToken));

        Assert.Contains("10 МБ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypted_or_active_pdf_is_rejected()
    {
        var source = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << /Encrypt true /OpenAction 2 0 R >> endobj\nstartxref\n0\n%%EOF");
        await using var stream = new MemoryStream(source);

        await Assert.ThrowsAsync<AttachmentValidationException>(() =>
            _sanitizer.SanitizeAsync(
                stream,
                source.LongLength,
                "document.pdf",
                "application/pdf",
                TestContext.Current.CancellationToken));
    }

    private static byte[] CreateJpeg()
    {
        var pixels = new byte[]
        {
            80, 120, 100, 90, 130, 110,
            100, 140, 120, 110, 150, 130
        };
        using var stream = new MemoryStream();
        new ImageWriter().WriteJpg(
            pixels,
            2,
            2,
            ColorComponents.RedGreenBlue,
            stream,
            quality: 90);
        return stream.ToArray();
    }

    private static byte[] AddExifSegment(byte[] jpeg)
    {
        var metadata = Encoding.ASCII.GetBytes("Exif\0\0GPSLatitude=55.75;GPSLongitude=37.62");
        var length = metadata.Length + 2;
        using var result = new MemoryStream();
        result.Write(jpeg, 0, 2);
        result.WriteByte(0xff);
        result.WriteByte(0xe1);
        result.WriteByte((byte)(length >> 8));
        result.WriteByte((byte)length);
        result.Write(metadata);
        result.Write(jpeg, 2, jpeg.Length - 2);
        return result.ToArray();
    }
}
