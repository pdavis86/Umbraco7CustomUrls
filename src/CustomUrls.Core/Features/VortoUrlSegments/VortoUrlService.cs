using System;
using System.Configuration;

namespace CustomUrls.Core.Features.VortoUrlSegments
{
    public class VortoUrlService
    {
        private VortoUrlService()
        {
            //Singleton
        }

        [ThreadStatic]
        private static VortoUrlService _instance;
        public static VortoUrlService Current
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VortoUrlService();
                }
                return _instance;
            }
        }

        private string _segmentPropertyName;
        public string SegmentPropertyName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_segmentPropertyName))
                {
                    var setting = ConfigurationManager.AppSettings["VortoUrl:SegmentPropertyName"];
                    if (!string.IsNullOrWhiteSpace(setting))
                    {
                        _segmentPropertyName = setting;
                    }
                    else
                    {
                        _segmentPropertyName = "vortoUrlName";
                    }
                }
                return _segmentPropertyName;
            }
        }

        private string _fallbackCultureName;
        public string FallbackCultureName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_fallbackCultureName))
                {
                    var setting = ConfigurationManager.AppSettings["VortoUrl:FallbackCultureName"];
                    if (!string.IsNullOrWhiteSpace(setting))
                    {
                        _fallbackCultureName = setting;
                    }
                    else
                    {
                        _fallbackCultureName = "en-US";
                    }
                }
                return _fallbackCultureName;
            }
        }

        private int? _cacheWriteTimeout;
        public int CacheWriteTimeout
        {
            get
            {
                if (!_cacheWriteTimeout.HasValue)
                {
                    var setting = ConfigurationManager.AppSettings["VortoUrl:CacheWriteTimeout"];
                    if (int.TryParse(setting, out var timeout))
                    {
                        _cacheWriteTimeout = timeout;
                    }
                    else
                    {
                        _cacheWriteTimeout = 1000;
                    }
                }
                return (int)_cacheWriteTimeout;
            }
        }

        private bool? _isContainsCompatibilityEnabled;
        public bool IsContainsCompatibilityEnabled
        {
            get
            {
                if (!_isContainsCompatibilityEnabled.HasValue)
                {
                    //Default is false which means an exact match with a content URL must be found
                    //True will check for any content URL which contains the request URL local path

                    var setting = ConfigurationManager.AppSettings["VortoUrl:ContainsCompatibilityEnabled"];
                    if (bool.TryParse(setting, out var isEnabled))
                    {
                        _isContainsCompatibilityEnabled = isEnabled;
                    }
                    else
                    {
                        _isContainsCompatibilityEnabled = false;
                    }
                }
                return (bool)_isContainsCompatibilityEnabled;
            }
        }

        private bool? _isTrimUrlTrailingSlashEnabled;
        public bool IsTrimUrlTrailingSlashEnabled
        {
            get
            {
                if (!_isTrimUrlTrailingSlashEnabled.HasValue)
                {
                    //Default is false which will mean URLs end with a forward slash
                    //True will generate URLs without a trailing forward slash

                    var setting = ConfigurationManager.AppSettings["VortoUrl:TrimUrlTrailingSlash"];
                    if (bool.TryParse(setting, out var isEnabled))
                    {
                        _isTrimUrlTrailingSlashEnabled = isEnabled;
                    }
                    else
                    {
                        _isTrimUrlTrailingSlashEnabled = false;
                    }
                }
                return (bool)_isTrimUrlTrailingSlashEnabled;
            }
        }

    }
}
