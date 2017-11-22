using System;
using System.IO;
using System.Collections.Generic;

using Android.Content;
using Android.Graphics.Drawables;
using Android.Widget;
using Android.Views;
using Android.Graphics.Pdf;
using Android.OS;
using Android.Runtime;
using Android.Util;
             
namespace AKPdfViewer
{
    public class AKPdfViewer : RelativeLayout
    {
        private Context _context;
        private RelativeLayout _pdfToolsContainer = null;
        private RelativeLayout _pdfCloseButtonContainer = null;
        private TextView _pdfPagesCount = null;
        private TextView _closeButton = null;
        private PdfListView _pdfList = null;
        private PdfPagesAdapter _pdfAdapter = null;
        private string _url;

        private interface IPDFTaskListener
        {
			void RenderingErrorCaught(string message);
			Context ContextProp { get; set; }

		}

        public event EventHandler<string> OnCustomerPdfViewerError;
        public event EventHandler DidDismiss;

        private class PdfPagesAdapter : ArrayAdapter, IPDFTaskListener
        {
            private List<KeyValuePair<int, PdfPageGenerationTask>> _pdfPageGenerationTasks;
            private int _resource;
            private int _count = 0;
            private double _zoom = 1.0f;
            private double _minZoom = 0.5f;
            private double _maxZoom = 2.0f;
            private string _url;

            public event EventHandler<string> OnPdfPagesAdapterError;

			private class PdfPageGenerationTask : AsyncTask<string, Java.Lang.Void, Android.Graphics.Bitmap>
			{
				private WeakReference<ImageView> _convertViewReference;
				private Android.Graphics.Bitmap _pdfPageBitmap = null;
				private IPDFTaskListener _listener;

				public int TaskIndex
				{
					get; set;
				}

				public PdfPageGenerationTask(ImageView convertView, IPDFTaskListener listener)
				{
					_convertViewReference = new WeakReference<ImageView>(convertView);
					_listener = listener;
				}

				protected override void OnCancelled(Java.Lang.Object result)
				{
					Console.WriteLine(string.Format("{0} {1} called on task with index {2}", "PdfPageGenerationTask", "OnCancelled", TaskIndex.ToString()));
					RecycleBitmap(_pdfPageBitmap);

					base.OnCancelled(result);
				}

				protected override Android.Graphics.Bitmap RunInBackground(params string[] @params)
				{
					if (IsCancelled)
					{
						RecycleBitmap(_pdfPageBitmap);
						return null;
					}

					string url = (string)@params[0];
					int position = int.Parse(@params[1]);
					double zoom = double.Parse(@params[2]);

					_pdfPageBitmap = null;

					if (File.Exists(url))
					{
						var file = new Java.IO.File(url);
						var mFileDescriptor = ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
						// This is the PdfRenderer we use to render the PDF.
						if (mFileDescriptor != null)
						{
							// create a new renderer
							using (PdfRenderer renderer = new PdfRenderer(mFileDescriptor))
							{
								using (PdfRenderer.Page page = renderer.OpenPage(position))
								{
									try
									{
                                        _pdfPageBitmap = Android.Graphics.Bitmap.CreateBitmap((int)(page.Width.Px() * zoom), (int)(page.Height.Px() * zoom), Android.Graphics.Bitmap.Config.Argb4444);
										page.Render(_pdfPageBitmap, null, null, PdfRenderMode.ForDisplay);
									}
									catch (Exception e)
									{
										_listener.RenderingErrorCaught(string.Format("PdfPageGenerationTask failed for index {0} :{1}", TaskIndex.ToString(), e.Message));
										Console.WriteLine(e);
									}
									page.Close();
								}
								renderer.Close();
							}
						}
					}

					return _pdfPageBitmap;
				}

				protected override void OnPostExecute(Android.Graphics.Bitmap result)
				{
					if (IsCancelled)
					{
						RecycleBitmap(result);
						return;
					}

					if (_convertViewReference != null)
					{
						ImageView convertView;
						_convertViewReference.TryGetTarget(out convertView);
						if (convertView != null)
						{
							Drawable imageDrawable = convertView.Drawable;

							if (imageDrawable != null && imageDrawable is BitmapDrawable)
							{
								BitmapDrawable bnpDrawable = (BitmapDrawable)imageDrawable;

								RecycleBitmap(bnpDrawable.Bitmap);
							}

							if (result != null && result is Android.Graphics.Bitmap)
							{
								convertView.SetImageDrawable(new BitmapDrawable(_listener.ContextProp.Resources, result));
								convertView.Visibility = ViewStates.Visible;
							}
						}
					}
				}

				void RecycleBitmap(Android.Graphics.Bitmap bitmapImage)
				{
					if (bitmapImage != null && !bitmapImage.IsRecycled)
						bitmapImage.Recycle();
				}
			}

            public PdfPagesAdapter(Context context, string url, int resource) : base(context, resource)
            {
                ContextProp = context;
                _resource = resource;
                _url = url;
                _pdfPageGenerationTasks = new List<KeyValuePair<int, PdfPageGenerationTask>>();

                SetSource(_url);

                ComponentCallbacksNotifications callBacksNotifications = new ComponentCallbacksNotifications();
                ContextProp.RegisterComponentCallbacks(callBacksNotifications);
                callBacksNotifications.OnMemoryCritical += MemoryCriticalHandle;
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                if (convertView == null)
                {
                    convertView = new ImageView(ContextProp);
                    convertView.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
                    // set size initially to fill whole screen. otherwise because of no height PdfPageGenerationTask will attempt to load all pages
					IWindowManager windowManager = Context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
                    Display disp = windowManager?.DefaultDisplay;
                    if (disp != null)
                    {
                        Android.Graphics.Point screenSize = new Android.Graphics.Point();
                        disp.GetSize(screenSize);
                        Android.Graphics.Bitmap pdfPageStartBitmap = Android.Graphics.Bitmap.CreateBitmap((int)(screenSize.X - HorizontalMarginsForView), (int)(screenSize.Y - VerticalMarginsForView), Android.Graphics.Bitmap.Config.Argb4444);
						((ImageView)convertView).SetImageDrawable(new BitmapDrawable(ContextProp.Resources, pdfPageStartBitmap));
                    }
                }

                // on zoom do not cancel previous tasks so zooming is visible and performed on all visible views
                // on scrolling lets just focus on newest task
                bool isZooming = false;

                int tasksHistoryRelevant = 1;
                if (parent is PdfListView)
                {
                    isZooming = ((PdfListView)parent).ScalingStatus != PdfListView.ScalingStatusValues.Idle;
                    tasksHistoryRelevant = ((PdfListView)parent).VisibleItemCount;
                }

                // on scrolling hide reused views untill shown again so they do not show old content
                if (!isZooming)
                    ((ImageView)convertView).Visibility = ViewStates.Invisible;

                PdfPageGenerationTask newPdfViewTask = new PdfPageGenerationTask((ImageView)convertView, this);
                newPdfViewTask.TaskIndex = position;

                SetPageGenerationTaskMngr(newPdfViewTask, tasksHistoryRelevant);
                newPdfViewTask.Execute(_url, position.ToString(), _zoom.ToString());

                return convertView;
            }

            public override int Count
            {
                get
                {
                    return _count;
                }
            }

            public void ChangeZoom(double change)
            {
                _zoom = change;

                if (_zoom > _maxZoom)
                    _zoom = _maxZoom;
                else if (_zoom < _minZoom)
                    _zoom = _minZoom;
            }

            public int HorizontalMarginsForView { get; set; }
            public int VerticalMarginsForView { get; set; }

            void MemoryCriticalHandle(object sender, EventArgs e)
            {
                Console.WriteLine(string.Format("{0} {1} called", "PdfPagesAdapter", "MemoryCriticalHandle"));
                // try to save the day - clear all tasks in adapter queue
                for (int i = _pdfPageGenerationTasks.Count - 1; i >= 0; i--)
                {
                    Console.WriteLine(string.Format("{0} {1} sweep task - remaining {2}", "PdfPagesAdapter", "MemoryCriticalHandle", _pdfPageGenerationTasks.Count.ToString()));
                    _pdfPageGenerationTasks[i].Value.Cancel(false);
                    _pdfPageGenerationTasks.RemoveAt(i);
                }
            }

            void SetPageGenerationTaskMngr(PdfPageGenerationTask pdfGenerationTask, int tasksHistoryRelevant)
            {
                if (_pdfPageGenerationTasks.Count > 1)
                {
                    int historyRelevantCounter = 0;
                    for (int i = _pdfPageGenerationTasks.Count - 1; i >= 0; i--)
                    {
                        historyRelevantCounter++;
                        if (historyRelevantCounter > tasksHistoryRelevant)
                        {
                            _pdfPageGenerationTasks[i].Value.Cancel(false);
                            _pdfPageGenerationTasks.RemoveAt(i);
                        }
                    }
                }

                _pdfPageGenerationTasks.Add(new KeyValuePair<int, PdfPageGenerationTask>(pdfGenerationTask.TaskIndex, pdfGenerationTask));
            }

            void SetSource(string url)
            {
                if (File.Exists(_url))
                {
                    var file = new Java.IO.File(_url);

                    var mFileDescriptor = ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
                    // This is the PdfRenderer we use to render the PDF.
                    if (mFileDescriptor != null)
                    {
                        // create a new renderer
                        using (PdfRenderer renderer = new PdfRenderer(mFileDescriptor))
                        {
                            _count = renderer.PageCount;
                        }
                    }
                }
            }

			public Context ContextProp { get; set;}

            public void RenderingErrorCaught(string message)
            {
                OnPdfPagesAdapterError?.Invoke(this, message);
            }
        }

        private class PdfListView : ListView
        {
            Context _context;
            AKPdfViewer _container;
            GestureDetector _pdfGestureDetector;
            SimplePdfGestureListener _pdfGestureDetectorListener;
            ScaleGestureDetector _pdfScaleDetector;
            SimplePdfScaleGestureListener _pdfScaleGestureListener;

            public enum ScalingStatusValues
            {
                Idle,
                InProgress,
            };

            private class SimplePdfGestureListener : GestureDetector.SimpleOnGestureListener
            {
                PdfListView _listView;

                public SimplePdfGestureListener(PdfListView listView)
                {
                    _listView = listView;
                }

                public override bool OnDoubleTap(MotionEvent e)
                {
                    _listView.ChangeZoom(1.0f);
                    return base.OnDoubleTapEvent(e);
                }
            }

            private class SimplePdfScaleGestureListener : ScaleGestureDetector.SimpleOnScaleGestureListener
            {
                PdfListView _listView;

                public SimplePdfScaleGestureListener(PdfListView listView)
                {
                    _listView = listView;
                }
                public override bool OnScaleBegin(ScaleGestureDetector detector)
                {
                    _listView.ScalingStatus = ScalingStatusValues.InProgress;
                    return base.OnScaleBegin(detector);
                }

                public override void OnScaleEnd(ScaleGestureDetector detector)
                {
                    _listView.ScalingStatus = ScalingStatusValues.Idle;
                    base.OnScaleEnd(detector);
                }

                public override bool OnScale(ScaleGestureDetector detector)
                {
                    _listView.ChangeZoom(detector.ScaleFactor);

                    return base.OnScale(detector);
                }
            }

            public PdfListView(Context context, AKPdfViewer container) : base(context)
            {
                _context = context;
                _container = container;
                LayoutParameters = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);

                _pdfGestureDetectorListener = new SimplePdfGestureListener(this);
                _pdfGestureDetector = new GestureDetector(_pdfGestureDetectorListener);

                _pdfScaleGestureListener = new SimplePdfScaleGestureListener(this);
                _pdfScaleDetector = new ScaleGestureDetector(_context, _pdfScaleGestureListener);

                this.Scroll += ListScroll;
                this.ScrollStateChanged += ListScrollStateChanged;
            }

            public override bool OnTouchEvent(MotionEvent e)
            {
                _pdfScaleDetector.OnTouchEvent(e);
                _pdfGestureDetector.OnTouchEvent(e);

                return base.OnTouchEvent(e);
            }

            public ScrollState ScrollStatus
            {
                get;
                set;
            }

            public ScalingStatusValues ScalingStatus
            {
                get;
                set;
            }

            public int VisibleItemCount
            {
                get; set;
            }

            public int FirstVisibleItem
            {
                get; set;
            }

            void ListScroll(object sender, AbsListView.ScrollEventArgs e)
            {
                VisibleItemCount = e.VisibleItemCount;
                FirstVisibleItem = e.FirstVisibleItem;
                _container.PdfUpdateListCount();
            }

            void ListScrollStateChanged(object sender, AbsListView.ScrollStateChangedEventArgs e)
            {
                ScrollStatus = e.ScrollState;
            }

            void ChangeZoom(float scale)
            {
                ((PdfPagesAdapter)(Adapter))?.ChangeZoom(scale);
                InvalidateViews();
            }
        }

        public AKPdfViewer(Context context, string url) : base(context)
        {
            _context = context;
            _url = url;

            _pdfToolsContainer = new RelativeLayout(_context);
            RelativeLayout.LayoutParams _pdfToolsContainerLayoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            _pdfToolsContainer.LayoutParameters = _pdfToolsContainerLayoutParams;
            _pdfToolsContainer.SetPadding(10.Px(), 10.Px(), 10.Px(), 10.Px());
            _pdfToolsContainer.SetBackgroundColor(Android.Graphics.Color.Argb(100, 0, 0, 0));

            _pdfPagesCount = new TextView(Context);
            _pdfPagesCount.SetTextColor(Android.Graphics.Color.White);
            _pdfPagesCount.Text = "";
            _pdfToolsContainer.AddView(_pdfPagesCount);

            this.AddView(_pdfToolsContainer);
        }

        public bool IsModal { get; set; }

        public void SetModalMode(bool isModal, string closeButtonTranslation)
        {
            IsModal = isModal;

            if(_pdfCloseButtonContainer == null)
            {
                _pdfCloseButtonContainer = new RelativeLayout(_context);
                RelativeLayout.LayoutParams _pdfCloseButtonContainerLayoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                _pdfCloseButtonContainerLayoutParams.AddRule(LayoutRules.AlignParentRight);
                _pdfCloseButtonContainer.LayoutParameters = _pdfCloseButtonContainerLayoutParams;
                _pdfCloseButtonContainer.SetPadding(10.Px(), 10.Px(), 10.Px(), 10.Px());
                _pdfCloseButtonContainer.SetBackgroundColor(Android.Graphics.Color.Argb(100, 0, 0, 0));

                _closeButton = new TextView(Context);
                _closeButton.SetTextColor(Android.Graphics.Color.White);
                _closeButton.Click += ClickCloseButton;
                _closeButton.Text = closeButtonTranslation;
                _pdfCloseButtonContainer.AddView(_closeButton);

                this.AddView(_pdfCloseButtonContainer);
            }
        }

        public void SetLayout()
        {
            RelativeLayout.LayoutParams pdfContainerLayoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            pdfContainerLayoutParams.SetMargins(MarginLeft, MarginTop, MarginRight, MarginBottom);
            this.LayoutParameters = pdfContainerLayoutParams;
            this.SetBackgroundColor(Android.Graphics.Color.White);

            if(_pdfList == null)
            {
                _pdfList = new PdfListView(Context, this);
                _pdfAdapter = new PdfPagesAdapter(Context, _url, 0);
                _pdfAdapter.HorizontalMarginsForView = MarginLeft + MarginRight;
                _pdfAdapter.HorizontalMarginsForView = MarginTop + MarginBottom;
                _pdfList.Adapter = _pdfAdapter;
                this.AddView(_pdfList);

                _pdfAdapter.OnPdfPagesAdapterError -= OnPdfPagesAdapterError;
                _pdfAdapter.OnPdfPagesAdapterError += OnPdfPagesAdapterError;
            }
        }

        public int MarginTop { get; set; }
        public int MarginBottom { get; set; }
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }

        public void PdfUpdateListCount()
        {
            if (_pdfPagesCount != null && _pdfList != null)
                _pdfPagesCount.Text = string.Format("{0}/{1}", _pdfList.FirstVisiblePosition + 1, _pdfList.Count);
        }

        public void Show()
        {
            Visibility = ViewStates.Visible;
        }

        public void Hide()
        {
            Visibility = ViewStates.Gone;
        }

        void ClickCloseButton(object sender, EventArgs e)
        {
            DidDismiss?.Invoke(this, EventArgs.Empty);
        }

        void OnPdfPagesAdapterError(object sender, string message)
        {
            OnCustomerPdfViewerError?.Invoke(this, message);
        }
    }

    public class ComponentCallbacksNotifications : Java.Lang.Object, IComponentCallbacks2
    {
        public event EventHandler OnMemoryCritical;
        public event EventHandler OnWentIntoBackground;

        public ComponentCallbacksNotifications()
        {
        }

        #region ComponentCallbacks2
        public void OnTrimMemory([GeneratedEnum] TrimMemory level)
        {
            if (level == TrimMemory.RunningCritical)
                OnMemoryCritical?.Invoke(this, EventArgs.Empty);
            else if (level == TrimMemory.UiHidden || level == TrimMemory.Complete)
                OnWentIntoBackground?.Invoke(this, EventArgs.Empty);
                
            Console.WriteLine(string.Format("{0} {1} called - {2}", "ComponentCallbacksNotifications", "OnTrimMemory", level));
        }

        void IComponentCallbacks.OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            Console.WriteLine(string.Format("{0} {1} called", "ComponentCallbacksNotifications", "OnConfigurationChanged"));
        }

        public void OnLowMemory()
        {
            Console.WriteLine(string.Format("{0} {1} called", "ComponentCallbacksNotifications", "OnLowMemory"));
        }
        #endregion
    }

    static class Extensions
    {
        public static int Px(this int dp)
        {
            if (dp == 0)
                return 0;

            return ((float)dp).Px();
        }

        public static int Px(this float dp)
        {
            return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Android.App.Application.Context.Resources.DisplayMetrics);
        }
    }
}
