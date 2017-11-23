# AKPdfViewer

PDF viewer that can be embeded and used internally in Xamarin android applications

## About
This is lighweight android pdf viewer that can be easily embedded in any Xamarin android app. Reading PDFs internally in apps for android is not that straightforward - AKPdfViewer utilizes PDFRenderer class provided by android to process any pdf file provided by its url and display it to user in scrollable ListView. User views pdf swiping from top<>bottom. Viewer supports pinch zooming, double tapping, asynchrous loading, tries to keep in memory only significant pdf pages. Application in this git is a simple wrapper that showcases on its main page MainActivity.cs how AKPdfViewer should be initiated to allow PDF browsing.

This application contains embedded in assets test pdf file that showcases working of AKPdfViewer viewer. This pdf "test.pdf" is copied to local application files from assets on button click just because in this version AKPdfViewer expects url to pdf file (and we cannot really provide real url to asset contents). Just deploy this app and click on button to see how AKPdfViewer displays embedded test.pdf file.

## Installation and Usage

1. Embed AKPdfViewer.cs class in your application
2. When you want to display the viewer with pdf contents:

Initiate the viewer by providing it with filePath which is url of the pdf file
AKPdfViewer pdfViewer = new AKPdfViewer(Application.Context, filePath); 

Set Margins that viewer should have. If they are set to 0 then viewer will cover all screen.
pdfViewer.MarginTop = pdfViewer.MarginBottom = pdfViewer.MarginLeft = pdfViewer.MarginRight = 0;
         
Call SetLayout which sets viewer layout depending on margins set above
pdfViewer.SetLayout();	

[optional] call SetModalMode method. This if provided with true as second argument will display Close button on the viewer. Close button Label is determined by second agument.
pdfViewer.SetModalMode(true, "Close");

Finally add viewer to you screen. It is simply added as subview.
mainView.AddView(pdfViewer);

[optional] hook up to DidDismiss event if you want to handle dismissing of viewer. DidDismiss will fire when user clicks close button so viewer should be called with SetModalMode
pdfViewer.DidDismiss += DidDismiss;

[optional] hook up to OnCustomerPdfViewerError event if you want to handle any errors that appear during pages rendering
pdfViewer.OnCustomerPdfViewerError += PopulateErrorToWebView;

3. If you want to close the viewer just remove it from the view (for example from DidDismiss Event handler) and dispose of pdfViewer object.

## Notes
Since AKPdfViewer bases in PDFRenderer it will only work in API 21 and newer since PDFRenderer has been added in API 21.

Wanna touch base? office@webproject.waw.pl
