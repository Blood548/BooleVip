using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace BooleVip;

public class BooleVip : BasePlugin
{
    public override string ModuleName => "BooleVip";
    public override string ModuleVersion => "0.1.0";

    private const string RecruitFlag = "@vip/recruit";
    private const string SergeantFlag = "@vip/sergeant";
    private const string CommanderFlag = "@vip/commander";

    public override void Load(bool hotReload)
    {
        AddCommand("css_vip", "Show VIP info", OnVipCommand);
        AddCommand("vip", "Show VIP info", OnVipCommand); // на случай, если чат-триггеры мапятся так

        RegisterEventHandler<EventPlayerConnectFull>(OnConnectFull);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
    }

    private HookResult OnVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return HookResult.Continue;

        var tier = GetTier(player);
        if (tier == VipTier.None)
        {
            player.PrintToChat($"{ChatColors.Red}[VIP]{ChatColors.Default} У тебя нет VIP статуса.");
            return HookResult.Continue;
        }

        var (name, hpPlus, armorPlus, moneyPlus, regen, syringe, smoke) = GetPerks(tier);

        player.PrintToChat($"{ChatColors.Green}[VIP]{ChatColors.Default} Твой статус: {ChatColors.Lime}{name}{ChatColors.Default}");
        player.PrintToChat($"{ChatColors.Gray}Бонусы:{ChatColors.Default} +HP/раунд {hpPlus}, +Armor/раунд {armorPlus}, +$/раунд {moneyPlus}");
        if (regen > 0) player.PrintToChat($"{ChatColors.Gray}Реген:{ChatColors.Default} +{regen} HP/сек");
        if (syringe) player.PrintToChat($"{ChatColors.Gray}Шприц:{ChatColors.Default} каждый раунд");
        if (smoke) player.PrintToChat($"{ChatColors.Gray}Smoke:{ChatColors.Default} разноцветный");
        return HookResult.Continue;
    }

    private HookResult OnConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        var tier = GetTier(player);
        if (tier == VipTier.None) return HookResult.Continue;

        var (name, _, _, _, _, _, _) = GetPerks(tier);

        // Уведомление в чат при заходе (всем)
        Server.PrintToChatAll($"{ChatColors.Green}[VIP]{ChatColors.Default} {ChatColors.Lime}{player.PlayerName}{ChatColors.Default} зашел на сервер ({name}).");

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
    {
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            var tier = GetTier(p);
            if (tier == VipTier.None) continue;

            ApplyRoundBonuses(p, tier);
        }
        return HookResult.Continue;
    }

    private void ApplyRoundBonuses(CCSPlayerController player, VipTier tier)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null) return;

        var (_, hpPlus, armorPlus, moneyPlus, regen, syringe, _) = GetPerks(tier);

        // HP/Armor
        try
        {
            var newHp = Math.Min(200, pawn.Health + hpPlus);
            pawn.Health = newHp;

            // Armor в CS2 обычно через ArmorValue
            pawn.ArmorValue = Math.Min(200, pawn.ArmorValue + armorPlus);
        }
        catch { /* разные сборки CSS могут отличаться полями */ }

        // Деньги
        try
        {
            player.InGameMoneyServices?.Account = Math.Min(16000, player.InGameMoneyServices.Account + moneyPlus);
        }
        catch { }

        // Реген (только для Commander)
        if (regen > 0)
        {
            AddTimer(1.0f, () =>
            {
                if (player == null || !player.IsValid) return;
                var pw = player.PlayerPawn.Value;
                if (pw == null) return;
                if (pw.Health <= 0) return;

                pw.Health = Math.Min(200, pw.Health + regen);
            }, TimerFlags.REPEAT);
        }

        // “Шприц каждый раунд” — зависит от того, как ты хочешь реализовать (граната/предмет/команда).
        // В v0.1 просто отметим, а реализацию добавим в следующем шаге.
        if (syringe)
        {
            player.PrintToChat($"{ChatColors.Green}[VIP]{ChatColors.Default} Шприц будет добавлен в следующей версии плагина.");
        }

        // Префикс в чате/табе — зависит от системы чата/таб-листа. В v0.1 делаем только чат-префикс сообщениями.
    }

    private VipTier GetTier(CCSPlayerController player)
    {
        // Проверяем флаги, которые выдаёт SimpleAdmin
        if (HasFlag(player, CommanderFlag)) return VipTier.Commander;
        if (HasFlag(player, SergeantFlag)) return VipTier.Sergeant;
        if (HasFlag(player, RecruitFlag)) return VipTier.Recruit;
        return VipTier.None;
    }

    private bool HasFlag(CCSPlayerController player, string flag)
    {
        // В разных системах флаги могут храниться по-разному.
        // Для SimpleAdmin они обычно в permissions/flags. Здесь сделан самый частый путь:
        return AdminManager.PlayerHasPermissions(player, flag);
    }

    private (string name, int hpPlus, int armorPlus, int moneyPlus, int regenPerSec, bool syringeEachRound, bool coloredSmoke) GetPerks(VipTier tier)
    {
        return tier switch
        {
            VipTier.Recruit   => ("Recruit",   2, 25,  600, 0, false, false),
            VipTier.Sergeant  => ("Sergeant",  3, 50, 1200, 0, false, true),
            VipTier.Commander => ("Commander", 5, 75, 2000, 1, true,  true),
            _ => ("None", 0, 0, 0, 0, false, false)
        };
    }

    private enum VipTier { None, Recruit, Sergeant, Commander }
}
