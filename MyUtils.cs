using MegaCrit.Sts2.Core.Logging;

namespace CustomCardDeckMod;

public class MyUtils
{
    public static void LogInfo(string str)
    {
        Log.Info("*************************************");
        Log.Info("*************************************");
        Log.Info(str);
        Log.Info("*************************************");
        Log.Info("*************************************");
    }
}