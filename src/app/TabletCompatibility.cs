using System;
using System.Reflection;

namespace BongoChat
{
    // Chat needs ordinary pointing and text input, not pressure, ink or raw pen data.
    // Configure before loading WPF: its tablet stack caches this choice at startup.
    public static class TabletCompatibility
    {
        private const string SwitchName = "Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport";
        public static bool Applied { get; private set; }
        public static string Status { get; private set; }

        public static bool Configure()
        {
            if (Applied) return true;
            try
            {
                // Keep the .NET 4.5 application baseline; the public AppContext
                // API exists on newer Windows Framework installations only.
                Type context = typeof(object).Assembly.GetType("System.AppContext");
                MethodInfo setter = context == null ? null : context.GetMethod("SetSwitch",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool) }, null);
                if (setter == null)
                {
                    Status = "AppContext unavailable; legacy runtime";
                    return false;
                }
                setter.Invoke(null, new object[] { SwitchName, true });
                Applied = true;
                Status = "WPF stylus/touch opt-out requested for this process; ordinary mouse input";
                return true;
            }
            catch (Exception ex)
            {
                // A compatibility mitigation must not prevent ordinary startup.
                Status = "Compatibility switch unavailable: " + ex.GetType().Name;
                return false;
            }
        }
    }
}