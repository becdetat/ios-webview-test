// This is a direct port of RNCachingUrlProtocol to MonoTouch / C#
// https://github.com/rnapier/RNCachingURLProtocol

//  Copyright (c) 2012 Rob Napier.
//
//  This code is licensed under the MIT License:
//
//  Permission is hereby granted, free of charge, to any person obtaining a
//  copy of this software and associated documentation files (the "Software"),
//  to deal in the Software without restriction, including without limitation
//  the rights to use, copy, modify, merge, publish, distribute, sublicense,
//  and/or sell copies of the Software, and to permit persons to whom the
//  Software is furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
//  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
//  DEALINGS IN THE SOFTWARE.
//

using System;
using MonoTouch.Foundation;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using MonoTouch.SystemConfiguration;

namespace ioswebhost
{
	public class MtRnCachingUrlProtocol : NSUrlProtocol
	{
		public MtRnCachingUrlProtocol ()
		{
		}

		static string MtRnCachingUrlHeader = @"X-MTRNCache";

		public NSUrlConnection Connection{get;set;}
		public NSMutableData Data{get;set;}
		public NSUrlResponse Response{get;set;}

		public override bool CanInitWithRequest(NSUrlRequest request)
		{
			// only handle http requests we haven't marked with our header.
			return 
				request.Url.Scheme == "http" && 
				!request.Headers.ContainsKey (MtRnCachingUrlHeader);
		}

		NSUrlRequest CanonicalRequestForRequest(NSUrlRequest request)
		{
			return NSUrlRequest;
		}

		string CachePathForRequest(NSUrlRequest aRequest)
		{
			// This stores in the Caches directory, which can be deleted when space is low, but we only use it for offline access
			var cachesPath = NSSearchPath.GetDirectories (NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User, true).Last ();
			return Path.Combine (cachesPath, aRequest.Url.AbsoluteString.GetHashCode ().ToString ());
		}

		public override void StartLoading()
		{
			if (!UseCache) 
			{
				var connectionRequest = (NSMutableUrlRequest)Request.MutableCopy;
				// we need to mark this request with our header so we know not to handle it in +[NSURLProtocol canInitWithRequest:].
				connectionRequest.SetValueForKey ("", MtRnCachingUrlHeader);
				var connection = NSUrlConnection.FromRequest (connectionRequest, this);
				Connection = connection;
			}
			else
			{
				var cache = NSKeyedUnarchiver.UnarchiveFile (CachePathForRequest (this.Request)) as MtRnCachedData;
				if (cache != null) {
					var data = cache.Data;
					var response = cache.Response;
					var redirectRequest = cache.RedirectRequest;
					if (redirectRequest != null) {
						this.Client.Redirected (this, redirectRequest, response);
					}
					else {
						Client.ReceivedResponse (this, response, NSUrlCacheStoragePolicy.NotAllowed);
						Client.DataLoaded (this, data);
						Client.FinishedLoading (this);
					}
				} else {
					this.Client.FailedWithError (NSError.FromDomain ("TODO", NSUrlError.CannotConnectToHost));
				}
			}
		}

		public override void StopLoading ()
		{
			Connection.Cancel ();
		}

		// NSURLConnection delegates (generally we pass these on to our client)

		public override NSUrlRequest WillSendRequest (NSUrlConnection connection, NSUrlRequest request, NSUrlResponse response)
		{
			// Thanks to Nick Dowell https://gist.github.com/1885821
			if (response != null) {
				var redirectableRequest = Request.MutableCopy as NSMutableUrlRequest;

				// We need to remove our header so we know to handle this request and cache it.
				// There are 3 requests in flight: the outside request, which we handled, the internal request,
				// which we marked with our header, and the redirectableRequest, which we're modifying here.
				// The redirectable request will cause a new outside request from the NSURLProtocolClient, which 
				// must not be marked with our header.
				redirectableRequest.SetValueForKey (null, MtRnCachingUrlHeader);

				var cachePath = CachePathForRequest (this.Request);
				var cache = new MtRnCachedData ();
				cache.Response = response;
				cache.Data = this.Data;
				cache.RedirectRequest = redirectableRequest;
				NSKeyedArchiver.ArchiveRootObjectToFile (cache, cachePath);
				this.Client.Redirected (this, redirectableRequest, response);
				return redirectableRequest;
			} else {
				return request;
			}		
		}

		public override void ReceivedData (NSUrlConnection connection, NSData data)
		{
			this.Client.DataLoaded (this, data);
			AppendData (data);
		}

		public override void FailedWithError (NSUrlConnection connection, NSError error)
		{
			this.Client.FailedWithError (this, error);
			this.Connection = null;
			this.Data = null;
			this.Response = null;
		}

		public override void ReceivedResponse (NSUrlConnection connection, NSUrlResponse response)
		{
			Response = response;
			Client.ReceivedResponse (this, response, NSUrlCacheStoragePolicy.NotAllowed);	// We cache ourselves.
		}

		public override void FinishedLoading (NSUrlConnection connection)
		{
			Client.FinishedLoading (this);

			var cachePath = CachePathForRequest (this.Request);
			var cache = new MtRnCachedData ();
			cache.Response = this.Response;
			cache.Data = this.Data;
			NSKeyedArchiver.ArchiveRootObjectToFile (cache, cachePath);

			Connection = null;
			Data = null;
			Response = null;
		}

		bool UseCache
		{
			get {
				return !IsHostReachable (Request.Url.Host);
			}
		}

		bool IsHostReachable(string host)
		{
			// Note that I'm not using Apple's Reachability code. This is based on part of
			// https://github.com/xamarin/monotouch-samples/blob/master/ReachabilitySample/reachability.cs
			// which started as a port of reachability.m but I"m only using what I have to.

			if (string.IsNullOrEmpty (host))
				return false;
			using (var r = new NetworkReachability(host)) {
				NetworkReachabilityFlags flags;
				if (r.TryGetFlags(flags)) {
					return IsReachableWithoutRequiringConnection (flags);
				}
			}
			return false;
		}

		bool IsReachableWithoutRequiringConnection(NetworkReachabilityFlags flags)
		{
			bool isReachable = (flags & NetworkReachabilityFlags.Reachable) != 0;

			// Do we need a connection to reach it?
			bool noConnectionRequired = (flags & NetworkReachabilityFlags.ConnectionRequired) == 0;

			// Since the network stack will automatically try to get the WAN up,
			// probe that
			if ((flags & NetworkReachabilityFlags.IsWWAN) != 0)
				noConnectionRequired = true;

			return isReachable && noConnectionRequired;	
		}

		void AppendData(NSData newData) {
			if (this.Data == null) {
				this.Data = newData.MutableCopy;
			} else {
				this.Data.AppendData (newData);
			}
		}
				
		class MtRnCachedData : NSObject
		{
			const string DataKey = "data";
			const string ResponseKey = "repsonse";
			const string RedirectRequestKey = "redirectRequest";

			public NSData Data{get;set;}
			public NSUrlResponse Response{get;set;}
			public NSUrlRequest RedirectRequest{get;set;}

			[Export("encodeWithCoder:")]
			public void EncodeWithCoder(NSCoder aCoder)
			{
				aCoder.Encode (this.Data, DataKey);
				aCoder.Encode (this.Response, ResponseKey);
				aCoder.Encode (this.RedirectRequest, RedirectRequestKey);
			}

			[Export("initWithCoder:")]
			void InitWithCoder(NSCoder aDecoder)
			{
				this.Data = aDecoder.DecodeObject (DataKey) as NSData;
				this.Response = aDecoder.DecodeObject (ResponseKey) as NSUrlResponse;
				this.RedirectRequest = aDecoder.DecodeObject (RedirectRequestKey) as NSUrlResponse;
			}
		}

	}
}

