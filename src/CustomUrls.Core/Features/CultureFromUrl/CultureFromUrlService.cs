using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CustomUrls.Core.Features.CultureFromUrl
{
    public class CultureFromUrlService
    {
        public const string LowercaseCultureRegexPattern = "^[a-z]{2}-[a-z]{2}$";

        private CultureFromUrlService()
        {
            //Singleton
        }

        [ThreadStatic]
        private static CultureFromUrlService _instance;
        public static CultureFromUrlService Current
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CultureFromUrlService();
                }
                return _instance;
            }
        }

        private bool? _isCultureFromUrlEnabled;
        public bool IsCultureFromUrlEnabled
        {
            get
            {
                if (!_isCultureFromUrlEnabled.HasValue)
                {
                    //False will get the culture from the "Cultures and Hostnames" instead

                    var setting = ConfigurationManager.AppSettings["CultureFromUrl:Enabled"];
                    if (bool.TryParse(setting, out var isEnabled))
                    {
                        _isCultureFromUrlEnabled = isEnabled;
                    }
                    else
                    {
                        _isCultureFromUrlEnabled = false;
                    }
                }
                return (bool)_isCultureFromUrlEnabled;
            }
        }

        private bool? _is404OnInvalidCultureEnabled;
        public bool Is404OnInvalidCultureEnabled
        {
            get
            {
                if (!_is404OnInvalidCultureEnabled.HasValue)
                {
                    //False gives better performance but the culture can be set to any valid .Net culture

                    var setting = ConfigurationManager.AppSettings["CultureFromUrl:Return404OnInvalidCulture"];
                    if (bool.TryParse(setting, out var isEnabled))
                    {
                        _is404OnInvalidCultureEnabled = isEnabled;
                    }
                    else
                    {
                        _is404OnInvalidCultureEnabled = true;
                    }
                }
                return (bool)_is404OnInvalidCultureEnabled;
            }
        }

        private bool? _isAllowCustomCulturesEnabled;
        public bool IsAllowCustomCulturesEnabled
        {
            get
            {
                if (!_isAllowCustomCulturesEnabled.HasValue)
                {
                    //True allows the instantiation of a new CultureInfo with the culture URL segment

                    var setting = ConfigurationManager.AppSettings["CultureFromUrl:AllowCustomCultures"];
                    if (bool.TryParse(setting, out var isEnabled))
                    {
                        _isAllowCustomCulturesEnabled = isEnabled;
                    }
                    else
                    {
                        _isAllowCustomCulturesEnabled = false;
                    }
                }
                return (bool)_isAllowCustomCulturesEnabled;
            }
        }

        public string GetCultureName(Uri current, string fallbackCultureName = null)
        {
            string cultureName = null;
            if (current.Segments.Length > 1)
            {
                cultureName = current.Segments[1].TrimEnd('/');
            }

            if (string.IsNullOrWhiteSpace(cultureName) || !Regex.IsMatch(cultureName.ToLower(), LowercaseCultureRegexPattern))
            {
                if (!string.IsNullOrWhiteSpace(fallbackCultureName))
                {
                    return fallbackCultureName;
                }
                return null;
            }

            return cultureName;
        }

        public CultureInfo GetCultureInfo(Uri current, string fallbackCultureName = null)
        {
            var cultureName = GetCultureName(current, fallbackCultureName);



            if (IsAllowCustomCulturesEnabled)
            {
                return new CultureInfo(cultureName);
            }

            return CultureInfo.GetCultures(CultureTypes.AllCultures)
                .FirstOrDefault(x => x.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
        }

    }
}
