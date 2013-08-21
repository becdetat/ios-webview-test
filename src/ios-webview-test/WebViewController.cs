using System;
using MonoTouch.Dialog;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Foundation;

namespace ioswebhost
{
	public class WebViewController : UIViewController
	{
		public WebViewController (string path, string title)
		{
			Title = title;
			TabBarItem.Image = UIImage.FromBundle ("gear");

			var webView = new UIWebView (View.Bounds);
			var url = new NSUrl(path);
			var urlRequest = new NSUrlRequest (url);
			webView.LoadRequest (urlRequest);

			View = webView;
		}
	}
}

