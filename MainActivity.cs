using System;
using System.IO;

using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;

namespace AKPdfViewer
{
	[Activity(Label = "AKPdfViewer", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
        AKPdfViewer _pdfViewer;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

            ViewGroup mainView = (ViewGroup)FindViewById(Android.Resource.Id.Content);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button>(Resource.Id.myButton);

			button.Click += delegate {

                var filePath = CopyPdfDocument();

                _pdfViewer = new AKPdfViewer(Application.Context, filePath);
                _pdfViewer.MarginTop = _pdfViewer.MarginBottom = 0;
                _pdfViewer.MarginLeft = _pdfViewer.MarginRight = 0;
                _pdfViewer.SetLayout();
                _pdfViewer.SetModalMode(true, "Close");
                mainView.AddView(_pdfViewer);
                _pdfViewer.OnCustomerPdfViewerError += PopulateErrorToWebView;
                _pdfViewer.DidDismiss += DidDismiss;
			};

            void DidDismiss(object sender, EventArgs e)
            {
                if (_pdfViewer != null)
                {
                    if (_pdfViewer.IsModal)
                    {
                        _pdfViewer.DidDismiss -= DidDismiss;
                    }
                    _pdfViewer.OnCustomerPdfViewerError -= PopulateErrorToWebView;
                    mainView.RemoveView(_pdfViewer);
                    _pdfViewer = null;
                }
            }

            void PopulateErrorToWebView(object sender, string errorMsg)
            {
                Console.WriteLine(errorMsg);
            }


            string CopyPdfDocument()
            {
                string pdfName = "test.pdf";
                string pdfPath = Path.Combine("data/data/com.companyname.akpdfviewer", pdfName);
                // Check if your DB has already been extracted.
                if (!File.Exists(pdfPath))
                {
                    using (BinaryReader br = new BinaryReader(Android.App.Application.Context.Assets.Open(pdfName)))
                    {
                        using (BinaryWriter bw = new BinaryWriter(new FileStream(pdfPath, FileMode.Create)))
                        {
                            byte[] buffer = new byte[2048];
                            int len = 0;
                            while ((len = br.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                bw.Write(buffer, 0, len);
                            }
                        }
                    }
                }

                return pdfPath;
            }
		}
	}
}

