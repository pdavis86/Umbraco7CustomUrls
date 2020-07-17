using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Cache;
using Umbraco.Web.Routing;

namespace CustomUrls.Core.Features.VortoUrlSegments
{
    public class VortoUrlInitialisation : ApplicationEventHandler
    {
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarting(umbracoApplication, applicationContext);

            //Note: The Umbraco cache is not updated until after these events
            ContentService.Published += ContentService_Published;
            ContentService.Moved += ContentService_Moved;
            ContentService.Trashing += ContentService_Trashing;

            CacheRefresherBase<PageCacheRefresher>.CacheUpdated += PageCacheRefresher_CacheUpdated;

            UrlProviderResolver.Current.InsertTypeBefore<DefaultUrlProvider, UrlHandling.UrlProvider>();
            UrlProviderResolver.Current.RemoveType<DefaultUrlProvider>();

            ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByNiceUrl, UrlHandling.ContentFinder>();
            ContentFinderResolver.Current.RemoveType<ContentFinderByNiceUrl>();
        }

        private void ContentService_Published(IPublishingStrategy sender, PublishEventArgs<IContent> e)
        {
            VortoUrlRouteCache.Current.Remove(e.PublishedEntities.Select(x => x.Id));
        }

        private void ContentService_Moved(IContentService sender, MoveEventArgs<IContent> e)
        {
            VortoUrlRouteCache.Current.Remove(e.MoveInfoCollection.Select(x => x.Entity.Id));
        }

        private void ContentService_Trashing(IContentService sender, MoveEventArgs<IContent> e)
        {
            VortoUrlRouteCache.Current.Remove(e.MoveInfoCollection.Select(x => x.Entity.Id));
        }

        private void PageCacheRefresher_CacheUpdated(PageCacheRefresher sender, CacheRefresherEventArgs e)
        {
            if (e.MessageType == Umbraco.Core.Sync.MessageType.RefreshAll)
            {
                VortoUrlRouteCache.Current.StartNewCacheFile();
            }
        }

    }
}
