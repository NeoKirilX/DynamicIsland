using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.QrCode;

namespace DynamicIsland;

static class QrService
{
    public static (bool Success, string Message, string? FilePath) GenerateAndSave(string? text = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText();
            }

            if (string.IsNullOrWhiteSpace(text))
                text = "https://github.com/NeoKirilX/DynamicIsland";

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = 320,
                    Height = 320,
                    Margin = 2,
                    PureBarcode = false,
                }
            };

            var pixelData = writer.Write(text);
            var bitmap = BitmapSource.Create(
                pixelData.Width,
                pixelData.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelData.Pixels,
                pixelData.Width * 4
            );
            bitmap.Freeze();

            // 1. Copy image to Windows clipboard
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    Clipboard.SetImage(bitmap);
                }
                catch { }
            });

            // 2. Save image to disk in Pictures\DynamicIsland
            string picturesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DynamicIsland");
            Directory.CreateDirectory(picturesDir);
            string filename = $"qr_code_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(picturesDir, filename);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(fileStream);
            }

            // 3. Add to shelf
            var shelf = Settings.Shelf.ToList();
            if (!shelf.Contains(filePath))
            {
                shelf.Insert(0, filePath);
                Settings.Shelf = shelf.ToArray();
            }

            return (true, "QR-код скопирован в буфер и добавлен на полку", filePath);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return (false, ex.Message, null);
        }
    }
}
