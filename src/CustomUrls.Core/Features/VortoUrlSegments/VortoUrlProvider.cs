using CustomUrls.Core.Extensions;
using CustomUrls.Core.Features.CultureFromUrl;
using CustomUrls.Core.Helpers;
using Our.Umbraco.Vorto.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace CustomUrls.Core.Features.VortoUrlSegments
{
    public abstract class VortoUrlProvider : IUrlProvider
    {
        public const string UmbracoNotInCacheUrl = "#";

        public string GetUrl(UmbracoContext umbracoContext, int id, Uri current, UrlProviderMode mode)
        {
            var content = umbracoContext.ContentCache.GetById(id);

            if (content == null)
            {
                LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, $"Couldn't find any page with nodeId={id}. This is most likely caused by the page not being published.");
                return null;
            }

            if (IsIgnored(content))
            {
                return null;
            }

            var domain = UrlHelper.GetUmbracoDomain(current);
            var domainUri = UrlHelper.GetUri(current, domain);

            string cultureName = null;
            if (CultureFromUrlService.Current.IsCultureFromUrlEnabled)
            {
                cultureName = CultureFromUrlService.Current.GetCultureInfo(current, VortoUrlService.Current.FallbackCultureName)?.Name;
            }
            else
            {
                cultureName = domain?.LanguageIsoCode ?? VortoUrlService.Current.FallbackCultureName;
            }

            var rootContentId = GetRootContentId(current, domain);

            var ancestorsAndContent = content.Ancestors()
                .Where(x => x.Parent != null)
                .SortByParent()
                .ToList();

            if (content.Id != rootContentId)
            {
                ancestorsAndContent.Add(content);
            }

            var url = GetRelativeUrl(ancestorsAndContent, cultureName);

            if (CultureFromUrlService.Current.IsCultureFromUrlEnabled)
            {
                url = $"/{cultureName}{url}";
            }

            var prefix = mode == UrlProviderMode.Relative ? string.Empty : domainUri;
            return UrlHelper.CombinePaths(prefix, url.ToLower(), VortoUrlService.Current.IsTrimUrlTrailingSlashEnabled);
        }

        public IEnumerable<string> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
        {
            var content = umbracoContext.ContentCache.GetById(id);

            if (content == null)
            {
                LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, $"Couldn't find any page with nodeId={id}. This is most likely caused by the page not being published.");
                return null;
            }

            var languages = GetLanguageList();

            var rootContentId = GetRootContentId(current, null);

            var ancestorsAndContent = content.Ancestors()
                .Where(x => x.Parent != null)
                .SortByParent()
                .ToList();

            if (content.Id != rootContentId)
            {
                ancestorsAndContent.Add(content);
            }

            var contentUrl = content.Url.ToLower();

            var urls = new List<string>();

            if (CultureFromUrlService.Current.IsCultureFromUrlEnabled)
            {
                var domainUri = UrlHelper.GetUri(current, UrlHelper.GetUmbracoDomain(current));
                foreach (var language in languages)
                {
                    var additionalUrls = GetOtherUrlsForContent(ancestorsAndContent, domainUri, language.IsoCode);
                    if (additionalUrls.Any())
                    {
                        urls.AddRange(additionalUrls.Where(x => x != contentUrl));
                    }
                }
            }
            else
            {
                var domains = ApplicationContext.Current.Services.DomainService
                    .GetAll(true)
                    .Where(x => x != null)
                    .OrderBy(x => x.Id)
                    .ToList();

                if (!domains.Any())
                {
                    //If domains are not set, add a dummy one
                    domains.Add(null);
                }

                foreach (var domain in domains)
                {
                    var domainUri = UrlHelper.GetUri(current, domain).ToString();
                    var cultureName = domain?.LanguageIsoCode ?? VortoUrlService.Current.FallbackCultureName;
                    var additionalUrls = GetOtherUrlsForContent(ancestorsAndContent, domainUri, cultureName);
                    if (additionalUrls.Any())
                    {
                        urls.AddRange(additionalUrls.Where(x => x != contentUrl));
                    }
                }
            }

            return urls.OrderBy(x => x);
        }

        public string GetUrlSegment(IPublishedContent content, string cultureName)
        {
            var vortoCulture = GetVortoCulture(cultureName);

            if (!content.HasVortoValue(
                VortoUrlService.Current.SegmentPropertyName,
                cultureName: vortoCulture,
                fallbackCultureName: VortoUrlService.Current.FallbackCultureName)
            )
            {
                return content.UrlName.EnsureEndsWith("/");
            }

            return content.GetVortoValue<string>(
                VortoUrlService.Current.SegmentPropertyName,
                cultureName: vortoCulture,
                fallbackCultureName: VortoUrlService.Current.FallbackCultureName)
                .ToUrlSegment()
                .EnsureEndsWith("/");
        }

        private int GetRootContentId(Uri current, IDomain domain)
        {
            int? rootContentId;
            if (domain != null)
            {
                rootContentId = domain.RootContentId;
            }
            else
            {
                rootContentId = UrlHelper.GetUmbracoDomain(current)?.RootContentId;
            }

            if (!rootContentId.HasValue)
            {
                rootContentId = UmbracoContext.Current.ContentCache.GetAtRoot().First().Id;
            }

            return rootContentId.Value;
        }

        private IEnumerable<string> GetOtherUrlsForContent(IEnumerable<IPublishedContent> ancestorsAndContent, string domainUri, string cultureName)
        {
            var otherUrls = new List<string>
            {
                GetRelativeUrl(ancestorsAndContent, cultureName)
            };

            var additionalUrls = GetAdditionalOtherUrls(ancestorsAndContent, domainUri, cultureName);
            if (additionalUrls != null && additionalUrls.Any())
            {
                otherUrls.AddRange(additionalUrls);
            }

            return otherUrls
                .Select(url =>
                {
                    if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        return url.ToLower();
                    }

                    if (CultureFromUrlService.Current.IsCultureFromUrlEnabled)
                    {
                        url = $"/{cultureName}{url}";
                    }

                    return UrlHelper.CombinePaths(domainUri, url.ToLower(), VortoUrlService.Current.IsTrimUrlTrailingSlashEnabled);
                })
                .ToList();
        }

        /// <summary>
        /// Should return true if the URL enumeration should be skipped for the supplied content
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public virtual bool IsIgnored(IPublishedContent content)
        {
            return false;
        }

        /// <summary>
        /// Should return the culture name to be used to find a value in the Vorto URL segment property
        /// </summary>
        /// <param name="cultureName"></param>
        /// <returns></returns>
        public virtual string GetVortoCulture(string cultureName)
        {
            return cultureName;
        }

        /// <summary>
        /// Should return a list of languages in use
        /// </summary>
        /// <returns></returns>
        public virtual List<ILanguage> GetLanguageList()
        {
            return ApplicationContext.Current.Services.LocalizationService.GetAllLanguages().ToList(); ;
        }

        /// <summary>
        /// Should return a relative URL without culture (domain and culture are handled separately)
        /// </summary>
        /// <param name="ancestorsAndContent"></param>
        /// <param name="cultureName"></param>
        /// <returns></returns>
        public virtual string GetRelativeUrl(IEnumerable<IPublishedContent> ancestorsAndContent, string cultureName)
        {
            return $"/{string.Join(string.Empty, ancestorsAndContent.Select(x => GetUrlSegment(x, cultureName)))}";
        }

        /// <summary>
        /// Should return an IEnumerable of URL strings, either relative without domain and culture or the full URL including scheme
        /// </summary>
        /// <param name="ancestorsAndContent"></param>
        /// <param name="domainUri"></param>
        /// <param name="cultureName"></param>
        /// <returns></returns>
        public virtual IEnumerable<string> GetAdditionalOtherUrls(IEnumerable<IPublishedContent> ancestorsAndContent, string domainUri, string cultureName)
        {
            return null;
        }

    }
}
