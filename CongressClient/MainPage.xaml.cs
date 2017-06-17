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
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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
        var contentTypes = new List<string>() { "application/pdf", "image/png", "image/jpeg" };
        IReadOnlyList<StorageFile> fileList = await folder.GetFilesAsync();
        foreach (var file in fileList)
        {
          if (contentTypes.Contains(file.ContentType))
          {
            await Task.Delay(500);
            var doc = new DocItem() { File = file };
            documents.Add(doc);
          }
        }
      }
    }
  }

  public class DocItem
  {
    public StorageFile File { get; set; }
    public string Label { get; set; }
    public PresenterItemType Type { get; set; }
    public byte[] Content { get; set; }
    public byte[] Preview { get; set; }
  }

  public enum PresenterItemType
  {
    Image,
    Document
  }
}
