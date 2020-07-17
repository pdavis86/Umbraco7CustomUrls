using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace CustomUrls.Core.Helpers
{
    public static class UrlHelper
    {
        public static IDomain GetUmbracoDomain(Uri uri)
        {
            var domains = ApplicationContext.Current.Services.DomainService.GetAll(true);
            var domain = domains.FirstOrDefault(x => x.DomainName.Contains(uri.Host));

            if (domain == null)
            {
                domain = domains.FirstOrDefault();
            }

            return domain;
        }

        public static string GetUri(Uri current, IDomain domain)
        {
            if (string.IsNullOrWhiteSpace(domain?.DomainName))
            {
                return current.GetLeftPart(UriPartial.Authority);
            }

            if (!Uri.TryCreate(domain.DomainName, UriKind.Absolute, out Uri uri))
            {
                if (!Uri.TryCreate(current.Scheme + "://" + domain.DomainName, UriKind.Absolute, out uri))
                {
                    return null;
                }
            }

            return uri.ToString();
        }

        public static string CombinePaths(string path1, string path2, bool trimEndSlash = false)
        {
            string path = path1.TrimEnd('/') + (!string.IsNullOrWhiteSpace(path2) ? path2.EnsureStartsWith('/') : null);
            return trimEndSlash ? path.TrimEnd('/') : path;
        }

    }
}
