# Umbraco 7 Custom URLs
An implementation which provides custom URL handling for Umbraco 7.


## Dependencies
- The Vorto Umbraco plugin


## Components
This implementation is made up of two features 
- Culture From URL 
- Vorto URL Segments

### Culture From URL
Default Umbraco 7 provides different cultures by domain. This code allows multiple URLs point to the same site but culture is handled by the first URL segment.

### Vorto URL Segments
Referencing a Vorto text string property (ideally used in a composition for all appropriate document types) this code allows for custom URL segments.





## How it works

The URL segment of a content item is based on the Vorto text string property with fallback to the content name.

e.g. https://example.com/en-GB/grand-parent-segment/parent-segment/content-segment/

- CultureFromUrlFilter.cs intercepts each request and set the current thread cultures.
- CultureFromUrlService.cs is used for checking the features enabled.
- VortoUrlInitialisation.cs registers event handlers for when content is published, moved, and deleted.
- VortoUrlProvider.cs defines valid URLs for each item of content. (This can be customised, see below).
- VortoUrlContentFinder.cs kicks off the process of matching a custom URL to a content item.
- VortoUrlRouteCache.cs finds and caches a content item and all valid URLs.
- VortoUrlService.cs is used for checking the features enabled.

The routes to access content items are stored in "/App_Data/VortoUrlSegments/routecache.xml" allowing for fast loading of previously accessed URLs.
Anything not in the cache is added when the content item is checked for a match.



## Customising

### web.config appsettings
- Set "CultureFromUrl:Enabled" to true if you want the culture to come from the first URL segment instead of the Umbraco "Cultures and Hostnames".
- Set "CultureFromUrl:Return404OnInvalidCulture" to false if you do not want to validate the culture supplied in the URL.
- Set "CultureFromUrl:AllowCustomCultures" to true if you want to allow any culture to be supplied in the URL.
- Set "VortoUrl:SegmentPropertyName" to the name of the Vorto string property to be used for URL segments.
- Set "VortoUrl:FallbackCultureName" to a valid culture name to use if no value is found in the supplied culture (defaults to en-US).
- Set "VortoUrl:CacheWriteTimeout" to change the write timeout on the cache XML file (defaults to 1000 milliseconds).

### Overrides
VortoUrlProvider and VortoUrlContentFinder are abstract classes as some of their methods can be overridden.

- Override VortoUrlProvider.IsIgnored() if there are particular IPublishedContent items which should not have a URL.
- Override VortoUrlProvider.GetVortoCulture() if there is a need to have a mapping between Umbraco languages and Vorto cultures.
- Override VortoUrlProvider.GetLanguageList() if there is a need to have URLs for more languages than are configured in Umbraco.
- Override VortoUrlProvider.GetRelativeUrl() if there is custom logic required for building URLs (there is default logic in place).
- Override VortoUrlProvider.GetAdditionalOtherUrls() if there is a need to add additional URLs to the list returned.
- Override VortoUrlContentFinder.IsIgnored() if a URL should not load any content.



## Try it out
This solution is configured so you can checkout the code and test it immediately. You will however need:
- IIS installed and configured to point to the "src" directory
- Visual Studio running as Administrator so the web project can use IIS

## Troubleshooting
If you are having issues, it may be because you have changed the configuration. Republish the entire site to start a new cache file.

If that does not solve the issue, feel free to drop me a message or log a bug.

## Contributions Are Welcome!
If you see something is wrong or there is a better way to do this then please do let me know.