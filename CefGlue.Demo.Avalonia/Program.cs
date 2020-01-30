using System.Runtime.InteropServices;
using Avalonia;
using Xilium.CefGlue.Common;

namespace Xilium.CefGlue.Demo.Avalonia
{
    class Program
    {

        static int Main(string[] args)
        {
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .AfterSetup(_ => CefRuntimeLoader.Initialize(new CefSettings() { WindowlessRenderingEnabled = false }))
                      .StartWithClassicDesktopLifetime(args);
                      
            return 0;
        }
    }
}
