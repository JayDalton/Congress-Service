using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

namespace CongressClient
{
  /// <summary>
  /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
  /// </summary>
  public sealed partial class MainPage : Page
  {
    private const string filePath = "c:/Temp/";
    private ObservableCollection<DocItem> documents;

    const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
    const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

    public MainPage()
    {
      this.InitializeComponent();
      documents = new ObservableCollection<DocItem>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
      var folderPicker = new Windows.Storage.Pickers.FolderPicker();
      folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
      folderPicker.FileTypeFilter.Add("*");

      StorageFolder folder = await folderPicker.PickSingleFolderAsync();
      if (folder != null)
      {
        var fileList = await folder.GetFilesAsync();
        foreach (var file in fileList)
        {
          await Task.Delay(500);
          switch (file.ContentType)
          {
            case "image/png":
            case "image/jpeg":
              documents.Add(await loadStorageFileBitmapAsync(file));
              break;
            case "application/pdf":
              documents.Add(await loadStorageFileDocumentAsync(file));
              break;
          }
        }
      }
    }

    private async Task<DocItem> loadStorageFileBitmapAsync(StorageFile file)
    {
      var doc = new DocItem() { Label = file.DisplayType, File = file, Preview = new BitmapImage() };

      var thumbnail = await file.GetThumbnailAsync(
        ThumbnailMode.PicturesView, 100,
        ThumbnailOptions.ResizeThumbnail
      );
      await doc.Preview.SetSourceAsync(thumbnail);

      return doc;
    }

    private async Task<PdfDocument> loadStorageFilePdfAsync(StorageFile file)
    {
      PdfDocument _pdfDocument;
      try
      {
        _pdfDocument = await PdfDocument.LoadFromFileAsync(file);
      }
      catch (Exception ex)
      {
        _pdfDocument = null;
        switch (ex.HResult)
        {
          case WrongPassword:
            Debug.WriteLine("Document is password-protected and password is incorrect.");
            break;

          case GenericFail:
            Debug.WriteLine("Document is not a valid PDF.");
            break;

          default:
            // File I/O errors are reported as exceptions.
            Debug.WriteLine(ex.Message);
            break;
        }
      }
      return _pdfDocument;
    }

    private async Task<InMemoryRandomAccessStream> resizeImageStream(IRandomAccessStream stream, int maxHeight = 100, int maxWidth = 100)
    {
      var result = new InMemoryRandomAccessStream();
      if (stream.Size > 0)
      {
        // scaling result
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

        BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(result, decoder);
        var ratioWidth = (double)maxWidth / decoder.PixelWidth;
        var ratioHeight = (double)maxHeight / decoder.PixelHeight;
        var ratioScale = Math.Min(ratioWidth, ratioHeight);

        var aspectHeight = (uint)(ratioScale * decoder.PixelHeight);
        var aspectWidth = (uint)(ratioScale * decoder.PixelWidth);

        //encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;
        encoder.BitmapTransform.ScaledHeight = aspectHeight;
        encoder.BitmapTransform.ScaledWidth = aspectWidth;
        await encoder.FlushAsync();

        result.Seek(0);

        // write result to preview
        //This.Preview = new byte[result.Size];
        //using (var reader = new DataReader(result.GetInputStreamAt(0)))
        //{
        //  await reader.LoadAsync((uint)result.Size);
        //  reader.ReadBytes(This.Preview);
        //}
      }
      return result;
    }

    private async Task<DocItem> loadStorageFileDocumentAsync(StorageFile file)
    {
      var doc = new DocItem() { File = file, Preview = new BitmapImage() };
      PdfDocument _pdfDocument = await loadStorageFilePdfAsync(file);

      if (_pdfDocument != null)
      {
        doc.Label = string.Format("PDF {0} pages", _pdfDocument.PageCount);
        using (var page = _pdfDocument.GetPage(0))
        {
          var stream = new InMemoryRandomAccessStream();
          await page.RenderToStreamAsync(stream);
          stream = await resizeImageStream(stream);
          await doc.Preview.SetSourceAsync(stream);
        }

        if (_pdfDocument.IsPasswordProtected)
        {
          doc.Label = "protected";
          Debug.WriteLine("Document is password protected.");
        }
      }

      return doc;
    }
  }

  public class DocItem
  {
    public string Label { get; set; }
    public StorageFile File { get; set; }
    public BitmapImage Preview { get; set; }
    public DocItemType Type { get; set; }
  }

  public enum DocItemType
  {
    Image,
    Document
  }
}
