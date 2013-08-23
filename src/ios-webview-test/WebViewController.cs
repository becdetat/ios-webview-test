using System;
using MonoTouch.Dialog;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Foundation;

namespace ioswebhost
{
	public class WebViewController : UIViewController
	{
		const int TimeoutInterval = 60 * 60 * 24 * 7 * 4;	// 4 weeks

		public WebViewController (string path, string title)
		{
			Title = title;
			TabBarItem.Image = UIImage.FromBundle ("gear");

			var webView = new UIWebView (View.Bounds);
			var url = new NSUrl(path);
			var urlRequest = new NSUrlRequest (url, NSUrlRequestCachePolicy.ReloadRevalidatingCacheData, TimeoutInterval);
			webView.LoadRequest (urlRequest);

			View = webView;
		}
	}
}

