namespace MvcTestApp
{
    internal static partial class MonorailCss
    {
        public static string[] CssClassValues() {
            var output = new List<string>();
            output.AddRange(BlazorMarkupValues());
            output.AddRange(BlazorAddClass());
            output.AddRange(CssClassCallValues());
            output.AddRange(CshtmlClasses());
            return output.ToArray();
        }
    

    }
}

