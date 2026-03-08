using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace CustomCardDeckMod
{
    [ModInitializer(nameof(Initialize))]
    public class MainFile
    {
        public static void Initialize()
        {
            new Harmony("CustomCardDeckMod").PatchAll();
            MyUtils.LogInfo("CustomCardDeckMod Start");
            
            ModHelper.AddModelToPool<RelicPoolModel, MyRelic>();
        }
    }
}