using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace ioswebhost
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		UIWindow _window;
		UITabBarController _tabBarController;

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			NSUrlProtocol.RegisterClass (new MonoTouch.ObjCRuntime.Class (typeof(MtRnCachingUrlProtocol)));

			_window = new UIWindow (UIScreen.MainScreen.Bounds);

			var webViewControllerSwxben = new WebViewController ("http://swxben.com", "SWXBEN");
			var webViewControllerReadify = new WebViewController ("http://readify.net", "Readify");
			var webViewControllerRgrrrrrba = new WebViewController ("http://rgrrrrrba.github.io", "RGRRRRRBA");
			var localWebViewController = new LocalWebViewController ("Content/Web/index.html", "Local");

			_tabBarController = new UITabBarController ();
			_tabBarController.ViewControllers = new UIViewController [] {
				webViewControllerSwxben,
				webViewControllerReadify,
				webViewControllerRgrrrrrba,
				localWebViewController,
			};
			
			_window.RootViewController = _tabBarController;
			_window.MakeKeyAndVisible ();
			
			return true;
		}
	}
}

