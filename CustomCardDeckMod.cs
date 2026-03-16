using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
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
        
        //无限删卡
        [HarmonyPatch(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.SetUsed))]
        public class MerchantCardRemovalEntrySetUsed
        {
            [HarmonyPostfix]
            static void Postfix(MerchantCardRemovalEntry __instance)
            {
                //__instance.Used = false;
                // 获取 Used 属性的 setter 方法
                System.Reflection.PropertyInfo property = typeof(MerchantCardRemovalEntry).GetProperty("Used");
                System.Reflection.MethodInfo setter = property?.GetSetMethod(true); // true 表示获取非公共成员

                // 调用私有 setter
                setter?.Invoke(__instance, new object[] { false });
            }
        }
    }
}