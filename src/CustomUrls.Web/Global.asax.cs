using CustomUrls.Core.Features.CultureFromUrl;
using System;
using System.Web.Mvc;
using Umbraco.Web;

namespace CustomUrls.Web
{
    public class Global : UmbracoApplication
    {
        protected override void OnApplicationStarting(object sender, EventArgs e)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            base.OnApplicationStarting(sender, e);

            if (CultureFromUrlService.Current.IsCultureFromUrlEnabled)
            {
                GlobalFilters.Filters.Add(new CultureFromUrlFilter());
            }
        }

        protected override void OnApplicationError(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();

            if (ex == null)
            {
                return;
            }

            try
            {
                if (Response.StatusCode != (int)System.Net.HttpStatusCode.InternalServerError)
                {
                    Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                }
                Logger.Error(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "Unhandled exception", ex);
            }
            catch
            {
                //Ignore as we can't log out the result of the catch
            }

            base.OnApplicationError(sender, e);
        }

    }
}
