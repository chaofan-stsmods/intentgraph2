using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;
using System.Runtime.Loader;

namespace IntentGraph2.Initializer;

[ModInitializer(nameof(Initialize))]
public class Initializer
{
    public static void Initialize()
    {
        LogInfo("IntentGraph entry initialize");

        LoadDll("Antlr4.Runtime.Standard");
        var coreAssembly = LoadDll("intentgraph2core");

        coreAssembly?.GetType("IntentGraph2.IntentGraphMod")?
            .GetMethod("InitializeMod", BindingFlags.Public | BindingFlags.Static)?
            .Invoke(null, null);

        LogInfo("IntentGraph entry initialize done.");
    }

    public static Assembly? LoadDll(string dllName)
    {
        try
        {
            var currentAssembly = typeof(Initializer).Assembly;
            var loadContext = AssemblyLoadContext.GetLoadContext(currentAssembly);
            if (loadContext != null)
            {
                var helperAssemblyPath = Path.Join(Path.GetDirectoryName(currentAssembly.Location), dllName + ".dll");
                var assembly = loadContext.LoadFromAssemblyPath(helperAssemblyPath);
                return assembly;
            }
            else
            {
                LogInfo("Failed to get assembly load context for IntentGraphMod.");
            }
        }
        catch (Exception ex)
        {
            LogInfo($"Failed to load dll {dllName}: {ex}");
        }

        return null;
    }

    private static void LogInfo(string message)
    {
        Log.Info($"[IntentGraph] {message}");
    }
}
