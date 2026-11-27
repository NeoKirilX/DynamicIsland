using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace DynamicIsland;

public partial class MainWindow
{
    const double QrNoticeSeconds = 7, PickerHintSeconds = 600;
    static readonly TimeSpan MenuHideTime = TimeSpan.FromMilliseconds(600);
    static readonly TimeSpan PickerHideTime = TimeSpan.FromMilliseconds(150);
    static readonly TimeSpan FocusSettleTime = TimeSpan.FromSeconds(3);

    bool _readingQr, _pickingArea;
    DateTime _focusSettlesAt;

    bool IsFocusUnsettled => _pickingArea || DateTime.UtcNow < _focusSettlesAt;

    void QrRow_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Qr);

    void QrScreen_Click(object sender, RoutedEventArgs e) => ReadQr(false);

    void QrArea_Click(object sender, RoutedEventArgs e) => ReadQr(true);

    void QrGenerate_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg, file) = QrService.GenerateAndSave();
        if (success && !string.IsNullOrEmpty(file))
        {
            _shelf.Add([file]);
            SyncShelf();
            UpdateTargets();
        }
        ShowPanel(Panel.None);
        Notify(Glyph.Qr, Brushes.White, "QR-код", success ? "Создан и скопирован в буфер" : msg);
    }

    async void ReadQr(bool pickArea)
    {
        if (_readingQr) return;

        _readingQr = true;
        ShowPanel(Panel.None);
        string? found = null;
        bool looked = false;
        try
        {
            await Task.Delay(MenuHideTime);
            Int32Rect? area = pickArea ? await PickAreaAsync() : QrReader.Desktop;
            if (area is { } chosen)
            {
                found = await Task.Run(() => QrReader.Read(chosen));
                looked = true;
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
        finally
        {
            _readingQr = false;
        }
        if (looked) ShowQr(found, pickArea);
    }

    async Task<Int32Rect?> PickAreaAsync()
    {
        var picker = new AreaPicker(QrReader.Desktop);
        _pickingArea = true;
        Notify(Glyph.Qr, Brushes.White, "Обведите QR-код", "Esc или правая кнопка — отмена", PickerHintSeconds, true);
        try
        {
            picker.ShowDialog();
        }
        finally
        {
            _pickingArea = false;
            _focusSettlesAt = DateTime.UtcNow + FocusSettleTime;
            EndTransient();
        }
        if (picker.Picked != null) await Task.Delay(PickerHideTime);
        return picker.Picked;
    }

    void ShowQr(string? found, bool inArea)
    {
        if (string.IsNullOrWhiteSpace(found))
        {
            Notify(Glyph.Qr, _dim, "QR-код не найден", inArea ? "В выбранной области его не видно" : "На экране его не видно", force: true);
            return;
        }

        string text = found.Trim();
        bool link = Uri.TryCreate(text, UriKind.Absolute, out Uri? address)
            && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);
        bool copied = Copy(text);
        string title = link ? copied ? "Скопировано · клик откроет" : "Ссылка · клик откроет" : copied ? "Текст скопирован" : "QR-код";
        Notify(Glyph.Qr, Brushes.White, title, text.ReplaceLineEndings(" "), QrNoticeSeconds, true, open: link ? () => OpenLink(address!) : null);
    }

    void OcrScreen_Click(object sender, RoutedEventArgs e) => ExtractOcr(false);

    void OcrArea_Click(object sender, RoutedEventArgs e) => ExtractOcr(true);

    async void OcrShelf_Click(object sender, RoutedEventArgs e)
    {
        var picture = _shelf.Items.FirstOrDefault(i => i.IsPhoto || (i.Picture != null));
        if (picture == null || string.IsNullOrEmpty(picture.Path))
        {
            Notify(Glyph.Tray, _dim, "В полке нет картинок", "Добавьте изображение в полку и повторите", force: true);
            return;
        }

        ShowPanel(Panel.None);
        Notify(Glyph.Check, Brushes.White, "Распознавание...", System.IO.Path.GetFileName(picture.Path), 3);
        string? text = await OcrService.ExtractFromFileAsync(picture.Path);
        ShowOcrResult(text);
    }

    async void ExtractOcr(bool pickArea)
    {
        if (_readingQr) return;
        _readingQr = true;
        ShowPanel(Panel.None);
        string? found = null;
        try
        {
            await Task.Delay(MenuHideTime);
            Int32Rect? area = pickArea ? await PickAreaAsync() : QrReader.Desktop;
            if (area is { } chosen)
            {
                Notify(Glyph.Check, Brushes.White, "Распознавание текста...", "Обработка области...", 3);
                found = await OcrService.ExtractFromAreaAsync(chosen);
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
        finally
        {
            _readingQr = false;
        }

        ShowOcrResult(found);
    }

    void ShowOcrResult(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Notify(Glyph.Cross, _dim, "Текст не найден", "Не удалось распознать символы", force: true);
            return;
        }

        string clean = text.Trim();
        Copy(clean);
        string preview = clean.Length > 80 ? clean[..80] + "…" : clean;
        Notify(Glyph.Check, _green, "Текст скопирован", preview.ReplaceLineEndings(" "), 6, true);
    }

    static bool Copy(string text)
    {
        try
        {
            Clipboard.SetDataObject(text, true);
            return true;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return false;
        }
    }

    static void OpenLink(Uri address)
    {
        try { Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { App.Log(ex); }
    }
}
