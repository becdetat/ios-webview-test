using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.IO;

namespace ioswebhost
{
	public class LocalWebViewController : UIViewController
	{
		public LocalWebViewController (string localPath, string title)
		{
			Title = title;
			TabBarItem.Image = UIImage.FromBundle ("gear");

			var webView = new UIWebView (View.Bounds);

			var localPathUrl = Path.Combine (NSBundle.MainBundle.BundlePath, localPath);
			var url = new NSUrl(localPathUrl, false);
			var urlRequest = new NSUrlRequest (url);

			webView.LoadRequest (urlRequest);

			View = webView;
		}
	}
}

