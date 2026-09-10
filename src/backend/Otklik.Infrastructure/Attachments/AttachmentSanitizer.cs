using System.Text;
using Otklik.Application.Attachments;
using StbImageSharp;
using StbImageWriteSharp;
using DecoderComponents = StbImageSharp.ColorComponents;
using WriterComponents = StbImageWriteSharp.ColorComponents;

namespace Otklik.Infrastructure.Attachments;

public sealed class AttachmentSanitizer : IAttachmentSanitizer
{
    public const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task<SanitizedAttachment> SanitizeAsync(
        Stream source,
        long declaredLength,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (declaredLength <= 0)
        {
            throw new AttachmentValidationException("Файл пуст.");
        }

        if (declaredLength > MaxFileSize)
        {
            throw new AttachmentValidationException("Файл больше 10 МБ. Выберите файл меньшего размера.");
        }

        await using var buffer = new MemoryStream((int)Math.Min(declaredLength, MaxFileSize));
        await source.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw new AttachmentValidationException("Файл пуст.");
        }

        if (buffer.Length > MaxFileSize)
        {
            throw new AttachmentValidationException("Файл больше 10 МБ. Выберите файл меньшего размера.");
        }

        var content = buffer.ToArray();
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var normalizedContentType = contentType.Trim().ToLowerInvariant();

        return normalizedContentType switch
        {
            "image/png" => SanitizeImage(content, extension, ".png", "image/png"),
            "image/jpeg" => SanitizeImage(content, extension, ".jpg", "image/jpeg"),
            "application/pdf" => SanitizePdf(content, extension),
            _ => throw new AttachmentValidationException("Формат файла не подходит. Загрузите PNG, JPEG или PDF.")
        };
    }

    private static SanitizedAttachment SanitizeImage(
        byte[] content,
        string suppliedExtension,
        string expectedExtension,
        string contentType)
    {
        var extensionMatches = expectedExtension == ".jpg"
            ? suppliedExtension is ".jpg" or ".jpeg"
            : suppliedExtension == expectedExtension;
        if (!extensionMatches || !SignatureMatches(content, expectedExtension))
        {
            throw new AttachmentValidationException("Расширение файла не совпадает с его содержимым.");
        }

        try
        {
            using var source = new MemoryStream(content, writable: false);
            var decoded = ImageResult.FromStream(source, DecoderComponents.RedGreenBlue);
            using var output = new MemoryStream();
            var writer = new ImageWriter();
            if (expectedExtension == ".png")
            {
                writer.WritePng(decoded.Data, decoded.Width, decoded.Height, WriterComponents.RedGreenBlue, output);
            }
            else
            {
                writer.WriteJpg(decoded.Data, decoded.Width, decoded.Height, WriterComponents.RedGreenBlue, output, quality: 90);
            }

            return new SanitizedAttachment(output.ToArray(), contentType, expectedExtension);
        }
        catch (AttachmentValidationException)
        {
            throw;
        }
        catch
        {
            throw new AttachmentValidationException("Не удалось прочитать изображение. Выберите другой файл.");
        }
    }

    private static SanitizedAttachment SanitizePdf(byte[] content, string suppliedExtension)
    {
        if (suppliedExtension != ".pdf" || !content.AsSpan().StartsWith(PdfSignature))
        {
            throw new AttachmentValidationException("Расширение файла не совпадает с его содержимым.");
        }

        var text = Encoding.Latin1.GetString(content);
        string[] forbiddenTokens = ["/Encrypt", "/OpenAction", "/AA", "/JavaScript", "/JS", "/Launch", "/EmbeddedFile"];
        if (forbiddenTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AttachmentValidationException("Защищённые или активные PDF не поддерживаются.");
        }

        return new SanitizedAttachment(content, "application/pdf", ".pdf");
    }

    private static bool SignatureMatches(byte[] content, string extension) => extension switch
    {
        ".png" => content.AsSpan().StartsWith(PngSignature),
        ".jpg" => content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
        _ => false
    };
}
