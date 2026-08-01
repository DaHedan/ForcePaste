using System.Windows;

namespace ForcePaste
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 先加载主题资源，再创建窗口（这样 DynamicResource 能正确解析）
            ThemeManager.Initialize();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ThemeManager.Cleanup();
            base.OnExit(e);
        }
    }
}
