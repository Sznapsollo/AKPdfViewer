# AKPdfViewer

PDF viewer that can be embeded and used internally in Xamarin android applications

## About
This is lighweight android pdf viewer that can be easily embedded in any Xamarin android app. Reading PDFs internally in apps for android is not that straightforward - AKPdfViewer utilizes PDFRenderer class provided by android to process any pdf file provided by its url and display it to user in scrollable ListView. User views pdf swiping from top<>bottom. Viewer supports pinch zooming, double tapping, asynchrous loading, tries to keep in memory only significant pdf pages. Application in this git is a simple wrapper that showcases on its main page MainActivity.cs how AKPdfViewer should be initiated to allow PDF browsing.

## Installation and Usage
This application contains embedded in assets test pdf file that showcases working of AKPdfViewer viewer. This pdf "test.pdf" is copied to local application files from assets on button click just because in this version AKPdfViewer expects url to pdf file (and we cannot really provide real url to asset contents). Just deploy this app and click on button to see how AKPdfViewer displays embedded test.pdf file.

## Notes
Since AKPdfViewer bases in PDFRenderer it will only work in API 21 and newer since PDFRenderer has been added in API 21.

Wanna touch base? office@webproject.waw.pl
