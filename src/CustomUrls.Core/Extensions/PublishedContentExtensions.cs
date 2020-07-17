using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace CustomUrls.Core.Extensions
{
    public static class PublishedContentExtensions
    {
        public static IEnumerable<IPublishedContent> SortByParent(this IEnumerable<IPublishedContent> oldList)
        {
            //Multiple IPublishedContent.Level can have the same value

            if (!oldList.Any())
            {
                return oldList;
            }

            var parentIds = oldList.Where(x => x.Parent != null).Select(x => x.Parent.Id);
            var nextToAdd = oldList.FirstOrDefault(x => !parentIds.Contains(x.Id));
            var newList = new List<IPublishedContent>();
            do
            {
                newList.Insert(0, nextToAdd);
                nextToAdd = oldList.FirstOrDefault(x => x.Id.Equals(nextToAdd.Parent.Id));

            } while (nextToAdd != null);

            return newList;
        }
    }
}
