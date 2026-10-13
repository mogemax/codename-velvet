using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace CodenameVelvet.Core.Debugging
{
    /// <summary>
    /// Stateless console logger for gameplay diagnostics. Messages read
    /// "[Velvet][{category}] {context name}: {message}" and pass the context to Unity,
    /// so clicking a log selects the object. Calls compile away (arguments included)
    /// outside the Editor and development builds; callers still gate them behind
    /// their own debug toggle.
    /// </summary>
    public static class VelvetLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string category, Object context, string message)
        {
            Debug.Log(Format(category, context, message), context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(string category, Object context, string message)
        {
            Debug.LogWarning(Format(category, context, message), context);
        }

        private static string Format(string category, Object context, string message)
        {
            string contextName = context != null ? context.name : "<none>";
            return $"[Velvet][{category}] {contextName}: {message}";
        }
    }
}
