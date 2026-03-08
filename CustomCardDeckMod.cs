using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CustomCardDeckMod
{
    public class CustomCardDeckMod
    {
        //获得初始遗物后添加自定义遗物
        [HarmonyPatch(typeof(Player), "PopulateStartingRelics")]
        public class ReceivePostCreateStartingRelics
        {
            [HarmonyPostfix]
            static void Postfix(Player __instance)
            {
                RelicCmd.Obtain(ModelDb.Relic<MyRelic>().ToMutable(), __instance);
            }
        }
    }
}