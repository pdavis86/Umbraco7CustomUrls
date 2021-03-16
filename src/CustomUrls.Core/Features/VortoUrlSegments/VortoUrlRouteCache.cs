using CustomUrls.Core.Features.CultureFromUrl;
using CustomUrls.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace CustomUrls.Core.Features.VortoUrlSegments
{
    public class VortoUrlRouteCache
    {
        private const string _elementNameRoot = "root";
        private const string _elementNameContent = "content";

        private const string _elementAttributeNameId = "id";
        private const string _elementAttributeNameContentId = "contentid";

        private static readonly object ctorlock = new object();
        private static VortoUrlRouteCache _instance;
        private static string _cacheFilePath;
        private static readonly ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();

        private XDocument _cacheFile;
        private int _cacheIsDirty;
        private int _rootsAreMissing;

        private VortoUrlRouteCache()
        {
            //Singleton
        }

        public static VortoUrlRouteCache Current
        {
            get
            {
                if (_instance == null)
                {
                    lock (ctorlock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VortoUrlRouteCache();
                            _cacheFilePath = HttpContext.Current.Server.MapPath("/App_Data/VortoUrlSegments/routecache.xml");
                            _instance.LoadOrCreateCacheFile();
                        }
                    }
                }
                if (_instance._rootsAreMissing > 0)
                {
                    _instance.AddRootsToCacheFile();
                    Interlocked.Exchange(ref _instance._rootsAreMissing, 0);
                    _instance.WriteCacheFileToDisk();
                }
                return _instance;
            }
        }

        public IPublishedContent Get(Uri uri, int rootContentId)
        {
            var elementId = GetElementIdFromUrl(rootContentId, uri.LocalPath);
            var element = GetElementById(elementId);

            if (element != null)
            {
                var contentFromCache = GetContentFromXmlElement(element);

                if (contentFromCache != null)
                {
                    return contentFromCache;
                }
            }

            var contentFromSearch = SearchForContent(elementId);

            WriteCacheFileToDisk();

            if (contentFromSearch == null)
            {
                return null;
            }

            return contentFromSearch;
        }

        public void Remove(IEnumerable<int> contentIds)
        {
            try
            {
                _locker.TryEnterWriteLock(VortoUrlService.Current.CacheWriteTimeout);

                foreach (var id in contentIds)
                {
                    var nodesForItem = GetElementsByAttributeValue(_elementAttributeNameContentId, id.ToString()).ToList();

                    if (nodesForItem.Any())
                    {
                        foreach (var node in nodesForItem)
                        {
                            if (node.Parent == _cacheFile.Root)
                            {
                                Interlocked.Increment(ref _rootsAreMissing);
                            }
                            node.Remove();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().DeclaringType, "Something went wrong trying to remove items from the in-memory cache", ex);
            }
            finally
            {
                _locker.ExitWriteLock();
            }

            WriteCacheFileToDisk();
        }

        public IEnumerable<string> ValidateAndClean()
        {
            var messages = new List<string>();

            var duplicates = _cacheFile.Descendants(_elementNameContent)
                   .Where(x => x.Attribute(_elementAttributeNameId) != null)
                   .GroupBy(x => x.Attribute(_elementAttributeNameId).Value)
                   .Where(x => x.Count() > 1)
                   .ToList();

            if (duplicates.Any())
            {
                try
                {
                    _locker.TryEnterWriteLock(VortoUrlService.Current.CacheWriteTimeout);

                    foreach (var group in duplicates)
                    {
                        var msg = $"{group.Count()} elements were found in the cache file for '{group.Key}'";
                        LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, msg);
                        messages.Add(msg);
                        group.Skip(1).Remove();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().DeclaringType, "Something went wrong trying to remove duplicates from the in-memory cache", ex);
                }
                finally
                {
                    _locker.ExitWriteLock();
                }

                WriteCacheFileToDisk();
            }

            return messages;
        }

        public void StartNewCacheFile()
        {
            try
            {
                _locker.TryEnterWriteLock(VortoUrlService.Current.CacheWriteTimeout);

                _cacheFile = new XDocument();
                _cacheFile.Add(new XElement(_elementNameRoot));
                _cacheFile.Changed += CacheChanged;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().DeclaringType, "Something went wrong trying to create a new in-memory cache", ex);
            }
            finally
            {
                _locker.ExitWriteLock();
            }

            AddRootsToCacheFile();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_cacheFilePath));
            WriteCacheFileToDisk();
        }

        private void AddRootsToCacheFile()
        {
            var rootItems = UmbracoContext.Current.ContentCache.GetAtRoot();
            if (rootItems.Any())
            {
                var urlProvider = UmbracoContext.Current.UrlProvider;

                var rootIds = ApplicationContext.Current.Services.DomainService.GetAll(true)
                    .Where(x => x.RootContentId.HasValue)
                    .Select(x => x.RootContentId.Value)
                    .Distinct()
                    .ToList();

                if (!rootIds.Any())
                {
                    rootIds.Add(rootItems.First().Id);
                }

                foreach (var id in rootIds)
                {
                    AddContentToCache(id, id, urlProvider);
                }

                //Backwards-compatbility for content not nested under its domain
                var nonDomainRoots = rootItems.Where(x => !rootIds.Contains(x.Id));
                if (nonDomainRoots.Any())
                {
                    var rootContentId = rootIds.First();
                    foreach (var item in nonDomainRoots)
                    {
                        //Not important - LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, $"Content item '{item.Name}' is on the content root but does not have a domain");
                        AddContentToCache(rootContentId, item.Id, urlProvider);
                    }
                }
            }
        }

        private void LoadOrCreateCacheFile()
        {
            if (_cacheFile != null && _cacheFile.Root.HasElements)
            {
                return;
            }

            if (System.IO.File.Exists(_cacheFilePath))
            {
                try
                {
                    _locker.TryEnterWriteLock(VortoUrlService.Current.CacheWriteTimeout);

                    _cacheFile = XDocument.Load(_cacheFilePath);
                    _cacheFile.Changed += CacheChanged;
                    return;
                }
                catch (Exception ex)
                {
                    //File is corrupt but let the code continue to create a new file
                    LogHelper.Error(MethodBase.GetCurrentMethod().DeclaringType, "The Vorto URL Route Cache file was corrupt", ex);
                }
                finally
                {
                    _locker.ExitWriteLock();
                }
            }

            StartNewCacheFile();
        }

        private void CacheChanged(object sender, XObjectChangeEventArgs e)
        {
            Interlocked.Increment(ref _cacheIsDirty);
        }

        private void WriteCacheFileToDisk()
        {
            if (_cacheIsDirty == 0)
            {
                return;
            }
            try
            {
                _locker.TryEnterWriteLock(VortoUrlService.Current.CacheWriteTimeout);
                _cacheFile.Save(_cacheFilePath);
                Interlocked.Exchange(ref _cacheIsDirty, 0);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().DeclaringType, "Something went wrong trying to write the cache to disk", ex);
            }
            finally
            {
                _locker.ExitWriteLock();
            }
        }

        private IEnumerable<XElement> GetElementsByAttributeValue(string attributeName, string value)
        {
            return _cacheFile.Root.Descendants().Where(x =>
                x.Attribute(attributeName) != null
                && x.Attribute(attributeName).Value.Equals(value)
            );
        }

        private XElement GetElementById(string id)
        {
            if (VortoUrlService.Current.IsContainsCompatibilityEnabled)
            {
                var pos = id.IndexOf("/");
                var rootContentId = pos > -1 ? id.Substring(0, pos) : id;
                var segment = pos > -1 ? id.Substring(pos) : string.Empty;
                return _cacheFile.Root.Descendants()
                    .Where(x =>
                        x.Attribute(_elementAttributeNameId) != null
                        && x.Attribute(_elementAttributeNameId).Value.StartsWith(rootContentId)
                        && x.Attribute(_elementAttributeNameId).Value.Contains(segment))
                    .FirstOrDefault();
            }

            //NOTE: might be able to improve performance by finding parent nodes first
            return GetElementsByAttributeValue(_elementAttributeNameId, id).FirstOrDefault();
        }

        private IPublishedContent GetContentFromXmlElement(XElement element)
        {
            if (
                element != null
                && int.TryParse(element.Attribute(_elementAttributeNameContentId)?.Value, out var contentId)
                )
            {
                var content = UmbracoContext.Current.ContentCache.GetById(contentId);

                if (content != null)
                {
                    return content;
                }
            }

            return null;
        }

        private bool AddContentToCache(int rootContentId, int contentId, UrlProvider urlProvider, bool deleteExisting = false, string matchingId = null)
        {
            var matchFound = false;

            try
            {
                _locker.TryEnterWriteLock(VortoUrlService.Current.CacheWriteTimeout);

                var contentIdStr = contentId.ToString();

                if (deleteExisting)
                {
                    var nodesForItem = GetElementsByAttributeValue(_elementAttributeNameContentId, contentIdStr).ToList();
                    foreach (var node in nodesForItem)
                    {
                        node.Remove();
                    }
                }

                var id = GetElementIdFromUrl(rootContentId, urlProvider.GetUrl(contentId, false));
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }
                if (id == matchingId)
                {
                    matchFound = true;
                }
                if (GetElementById(id) == null)
                {
                    GetParentElement(id).AddFirst(CreateElement(id, contentIdStr));
                }

                var otherUrlIds = urlProvider
                    .GetOtherUrls(contentId)
                    .Select(x => GetElementIdFromUrl(rootContentId, x))
                    .Distinct();
                foreach (var otherId in otherUrlIds)
                {
                    if (otherId == id)
                    {
                        continue;
                    }
                    if (otherId == matchingId)
                    {
                        matchFound = true;
                    }
                    if (GetElementById(otherId) == null)
                    {
                        GetParentElement(otherId).AddFirst(CreateElement(otherId, contentIdStr));
                    }
                }

            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().DeclaringType, "Something went wrong trying to write to the in-memory cache", ex);
            }
            finally
            {
                _locker.ExitWriteLock();
            }

            return matchFound;
        }

        private IPublishedContent SearchForContent(string elementId)
        {
            IPublishedContent content = null;

            var urlProvider = UmbracoContext.Current.UrlProvider;

            var segments = elementId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            XElement startingPoint = null;
            for (var i = segments.Length; i > 0; i--)
            {
                var id = string.Join("/", segments.Take(i));
                startingPoint = GetElementById(id);
                if (startingPoint != null)
                {
                    break;
                }
            }

            if (startingPoint == null)
            {
                //This should never happen. If it does, there's something wrong with the cache!
                LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, $"No starting point content item was found for element ID {elementId}");
                return null;
            }

            if (!int.TryParse(segments[0], out var rootContentId))
            {
                //This should never happen. If it does, there's something wrong with the cache!
                LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, $"Invalid root content ID for element ID {elementId}");
                return null;
            }

            var startingPointContent = GetContentFromXmlElement(startingPoint);

            var contentToScan = startingPointContent.Children.ToList();
            do
            {
                foreach (var item in contentToScan)
                {
                    var nodesForItem = GetElementsByAttributeValue(_elementAttributeNameContentId, item.Id.ToString());
                    if (!nodesForItem.Any())
                    {
                        if (AddContentToCache(rootContentId, item.Id, urlProvider, matchingId: elementId))
                        {
                            content = item;
                            break;
                        }
                    }
                }

                if (content != null)
                {
                    break;
                }

                contentToScan = contentToScan.SelectMany(x => x.Children).ToList();

            } while (contentToScan.Count > 0);

            return content;
        }

        private string GetElementIdFromUrl(int rootContentId, string url)
        {
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                var uri = new Uri(url);
                url = uri.LocalPath;
            }

            if (url == VortoUrlProvider.UmbracoNotInCacheUrl)
            {
                return null;
            }

            var id = url.TrimStart("/").TrimEnd("/").ToLower();

            var firstSegment = id.Contains("/") ? id.Substring(0, id.IndexOf("/")) : id;

            var stripFirstSegment = CultureFromUrlService.Current.IsCultureFromUrlEnabled;
            if (stripFirstSegment && firstSegment.Length > 2 && !Regex.IsMatch(firstSegment, CultureFromUrlService.LowercaseCultureRegexPattern))
            {
                stripFirstSegment = false;
            }

            if (stripFirstSegment)
            {
                if (!id.Contains("/"))
                {
                    return rootContentId.ToString();
                }
                id = id.Substring(id.IndexOf("/"));
            }

            return UrlHelper.CombinePaths(rootContentId.ToString(), id);
        }

        private XElement GetParentElement(string elementId)
        {
            var pos = elementId.LastIndexOf("/");
            if (pos == -1)
            {
                return _cacheFile.Root;
            }

            var parentId = elementId.Substring(0, pos);
            var parent = GetElementById(parentId);

            if (parent != null)
            {
                return parent;
            }

            //Most likely an overridden URL, just store it on the content root
            var rootId = elementId.Substring(0, elementId.IndexOf("/"));
            return GetElementById(rootId);
        }

        private XElement CreateElement(string id, string contentId)
        {
            var element = new XElement(_elementNameContent);
            element.SetAttributeValue(_elementAttributeNameId, id);
            element.SetAttributeValue(_elementAttributeNameContentId, contentId);
            return element;
        }

    }
}
