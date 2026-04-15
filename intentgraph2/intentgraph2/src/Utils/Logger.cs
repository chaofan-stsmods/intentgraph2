using MegaCrit.Sts2.Core.Logging;

namespace IntentGraph2.Utils;

internal class IgLogger
{
    public static void Info(string message)
    {
        Log.Info("[IntentGraph] " + message);
    }

    public static void Warn(string message)
    {
        Log.Warn("[IntentGraph] " + message);
    }

    public static void Error(string message)
    {
        Log.Error("[IntentGraph] " + message);
    }

    public static void Debug(string message)
    {
        Log.Debug("[IntentGraph] " + message);
    }

    public static void Verbose(string message)
    {
        Log.VeryDebug("[IntentGraph] " + message);
    }
}
