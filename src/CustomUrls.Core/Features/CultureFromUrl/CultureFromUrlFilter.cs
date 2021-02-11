using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Web;
using UrlHelper = CustomUrls.Core.Helpers.UrlHelper;

namespace CustomUrls.Core.Features.CultureFromUrl
{
    public class CultureFromUrlFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!UmbracoContext.Current.IsFrontEndUmbracoRequest || !CultureFromUrlService.Current.IsCultureFromUrlEnabled)
            {
                return;
            }

            if (!Uri.TryCreate(filterContext.HttpContext.Request.Url, filterContext.HttpContext.Request.RawUrl, out var url))
            {
                url = filterContext.HttpContext.Request.Url;
            }

            var culture = CultureFromUrlService.Current.GetCultureInfo(url);

            if (culture == null)
            {
                var domain = UrlHelper.GetUmbracoDomain(url);

                if (domain != null)
                {
                    culture = new CultureInfo(domain.LanguageIsoCode);
                }
                else
                {
                    culture = ApplicationContext.Current.Services.LocalizationService.GetAllLanguages().First().CultureInfo;
                }
            }

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        public void OnActionExecuted(ActionExecutedContext filterContext)
        {
            //Nothing to do
        }

    }
}
