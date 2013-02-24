#region

using Nancy.Helpers;

#endregion

namespace QuartzNet.WebConsole.Views
{
    public static class HtmlHelpers
    {
        public static string HtmlAttribute(this string str)
        {
            return HttpUtility.HtmlAttributeEncode(str);
        }

        public static string HtmlEncode(this string str)
        {
            return HttpUtility.HtmlEncode(str);
        }
    }
}