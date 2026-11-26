using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace DynamicIsland;

static class OcrService
{
    public static async Task<string?> ExtractTextAsync(BitmapSource bitmapSource)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
            using (var memoryStream = new MemoryStream())
            {
                encoder.Save(memoryStream);
                byte[] bytes = memoryStream.ToArray();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
            }

            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            OcrEngine? engine = null;
            var ruLang = new Windows.Globalization.Language("ru");
            var enLang = new Windows.Globalization.Language("en-US");

            if (OcrEngine.IsLanguageSupported(ruLang))
                engine = OcrEngine.TryCreateFromLanguage(ruLang);
            else if (OcrEngine.IsLanguageSupported(enLang))
                engine = OcrEngine.TryCreateFromLanguage(enLang);
            else
                engine = OcrEngine.TryCreateFromUserProfileLanguages();

            if (engine == null) return null;

            var result = await engine.RecognizeAsync(softwareBitmap);
            return result?.Text;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return null;
        }
    }

    public static async Task<string?> ExtractFromAreaAsync(Int32Rect area)
    {
        try
        {
            byte[] pixels = QrReader.Capture(area.X, area.Y, area.Width, area.Height);
            var bmp = BitmapSource.Create(area.Width, area.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null, pixels, area.Width * 4);
            return await ExtractTextAsync(bmp);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return null;
        }
    }

    public static async Task<string?> ExtractFromFileAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var uri = new Uri(path, UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return await ExtractTextAsync(bmp);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return null;
        }
    }
}
