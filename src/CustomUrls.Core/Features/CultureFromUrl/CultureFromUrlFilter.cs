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

            var culture = CultureFromUrlService.Current.GetCultureInfo(filterContext.RequestContext.HttpContext.Request.Url);

            if (culture == null)
            {
                var domain = UrlHelper.GetUmbracoDomain(filterContext.RequestContext.HttpContext.Request.Url);

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
