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
    private ObservableCollection<DocItem> documents;
    private ObservableCollection<BitmapImage> docContent;

    public MainPage()
    {
      this.InitializeComponent();
      documents = new ObservableCollection<DocItem>();
      docContent = new ObservableCollection<BitmapImage>();
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
      var doc = new DocItem() { File = file, Preview = new BitmapImage() };
      doc.Type = DocItemType.Image;
      doc.Label = file.DisplayType;

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
      const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
      const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

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

    private Size scaleSizeDown(Size size, double maxHeight = 100, double maxWidth = 100)
    {
      var ratioWidth = maxWidth / size.Width;
      var ratioHeight = maxHeight / size.Height;
      var ratioScale = Math.Min(ratioWidth, ratioHeight);

      var aspectHeight = ratioScale * size.Height;
      var aspectWidth = ratioScale * size.Width;

      return new Size(aspectWidth, aspectHeight);
    }

    private async Task<InMemoryRandomAccessStream> resizeImageStream(IRandomAccessStream stream, int maxHeight = 100, int maxWidth = 100)
    {
      var result = new InMemoryRandomAccessStream();
      if (stream.Size > 0)
      {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(result, decoder);

        //encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;

        var size = scaleSizeDown(new Size(decoder.PixelHeight, decoder.PixelWidth));
        encoder.BitmapTransform.ScaledHeight = (uint)size.Height;
        encoder.BitmapTransform.ScaledWidth = (uint)size.Width;
        await encoder.FlushAsync();

        result.Seek(0); // ???
      }
      return result;
    }

    private async Task<DocItem> loadStorageFileDocumentAsync(StorageFile file)
    {
      var doc = new DocItem() { File = file, Preview = new BitmapImage(), Type = DocItemType.Document };
      PdfDocument _pdfDocument = await loadStorageFilePdfAsync(file);

      if (_pdfDocument != null)
      {
        doc.Label = string.Format("PDF {0} pages", _pdfDocument.PageCount);
        using (var page = _pdfDocument.GetPage(0))
        {
          uint qualityRatio = 1;
          var options = new PdfPageRenderOptions
          {
            DestinationHeight = (uint)page.Size.Height * qualityRatio,
            DestinationWidth = (uint)page.Size.Width * qualityRatio,
            BitmapEncoderId = BitmapEncoder.JpegXREncoderId
          };

          var stream = new InMemoryRandomAccessStream();
          await page.RenderToStreamAsync(stream, options);
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

    private async Task loadItemView(DocItem item)
    {
      Progress.IsActive = true;
      Progress.Visibility = Visibility.Visible;

      docContent.Clear();
      switch (item.Type)
      {
        case DocItemType.Image:
          var image = await loadImageBitmapImage(item);
          docContent.Add(image);
          break;
        case DocItemType.Document:
          var pages = await loadDocumentPages(item);
          foreach (var page in pages)
          {
            docContent.Add(page);
          }
          break;
      }

      Progress.IsActive = false;
      Progress.Visibility = Visibility.Collapsed;
    }

    private async Task<BitmapImage> loadImageBitmapImage(DocItem item)
    {
      var resultImage = new BitmapImage();
      using (var stream = await item.File.OpenAsync(FileAccessMode.Read))
      {
        await resultImage.SetSourceAsync(stream);
      }
      return resultImage;
    }

    private async Task<IEnumerable<BitmapImage>> loadDocumentPages(DocItem item)
    {
      var resultList = new List<BitmapImage>();
      var pdfDocument = await loadStorageFilePdfAsync(item.File);
      if (pdfDocument != null)
      {
        var PageCount = pdfDocument.PageCount;
        for (uint idx = 0; idx < PageCount; idx++)
        {
          using (var page = pdfDocument.GetPage(idx))
          {
            var bitmapImage = new BitmapImage();
            using (var bmpStream = new InMemoryRandomAccessStream())
            {
              var qualityRatio = 1.5;
              var options = new PdfPageRenderOptions
              {
                DestinationHeight = (uint)(page.Size.Height * qualityRatio),
                DestinationWidth = (uint)(page.Size.Width * qualityRatio),
                BitmapEncoderId = BitmapEncoder.PngEncoderId
              };
              await page.RenderToStreamAsync(bmpStream, options);
              await bitmapImage.SetSourceAsync(bmpStream);
            }
            resultList.Add(bitmapImage);
          }
        }
      }
      return resultList;
    }

    private async void DocumentListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (sender is ListView)
      {
        var docList = sender as ListView;
        if (docList.SelectedItem != null)
        {
          var doc = docList.SelectedItem as DocItem;
          await loadItemView(doc);
        }
      }
    }
  }

  public class DocFlip
  {
    public uint Index { get; set; }
    public BitmapImage Image { get; set; }
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
