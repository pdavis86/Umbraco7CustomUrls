using CustomUrls.Core.Features.CultureFromUrl;
using CustomUrls.Core.Helpers;
using System;
using System.Linq;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace CustomUrls.Core.Features.VortoUrlSegments
{
    public abstract class VortoUrlContentFinder : IContentFinder
    {
        public bool TryFindContent(PublishedContentRequest contentRequest)
        {
            if (CultureFromUrlService.Current.IsCultureFromUrlEnabled && CultureFromUrlService.Current.Is404OnInvalidCultureEnabled)
            {
                var cultureName = CultureFromUrlService.Current.GetCultureInfo(contentRequest.Uri)?.Name;
                if (cultureName != null)
                {
                    var umbracoLanguage = ApplicationContext.Current.Services.LocalizationService
                        .GetAllLanguages()
                        .FirstOrDefault(x => x.IsoCode == cultureName);

                    if (umbracoLanguage == null)
                    {
                        return false;
                    }
                }
            }

            if (IsIgnored(contentRequest.Uri))
            {
                return false;
            }

            int? rootContentId;
            if (contentRequest.HasDomain)
            {
                rootContentId = contentRequest.UmbracoDomain.RootContentId;
            }
            else
            {
                rootContentId = UrlHelper.GetUmbracoDomain(contentRequest.Uri)?.RootContentId;
            }

            if (!rootContentId.HasValue)
            {
                rootContentId = UmbracoContext.Current.ContentCache.GetAtRoot().FirstOrDefault()?.Id;
            }

            if (!rootContentId.HasValue)
            {
                LogHelper.Warn(MethodBase.GetCurrentMethod().DeclaringType, $"No RootContentId found for request {contentRequest.Uri}");
                return false;
            }

            contentRequest.PublishedContent = VortoUrlRouteCache.Current.Get(contentRequest.Uri, rootContentId.Value);

            return contentRequest.PublishedContent != null;
        }

        /// <summary>
        /// Should return true to indicate that no content was found for the supplied URL
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public virtual bool IsIgnored(Uri uri)
        {
            return false;
        }

    }
}
