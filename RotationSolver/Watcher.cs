﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using RotationSolver.Basic.Configuration;
using System.Text.RegularExpressions;

namespace RotationSolver;

public static class Watcher
{
    private static ICallGateSubscriber<object, object>? IpcSubscriber;

    public static void Enable()
    {
        IpcSubscriber = Svc.PluginInterface.GetIpcSubscriber<object, object>("PingPlugin.Ipc");

        ActionEffect.ActionEffectEvent += ActionFromEnemy;
        ActionEffect.ActionEffectEvent += ActionFromSelf;
        MapEffect.Init((a1, position, param1, param2) =>
        {
            if (DataCenter.MapEffects.Count >= 64)
            {
                DataCenter.MapEffects.TryDequeue(out _);
            }

            var effect = new MapEffectData(position, param1, param2);
            DataCenter.MapEffects.Enqueue(effect);
#if DEBUG
            Svc.Log.Debug(effect.ToString());
#endif
        });
    }

    public static void Disable()
    {
        MapEffect.Dispose();
        ActionEffect.ActionEffectEvent -= ActionFromEnemy;
        ActionEffect.ActionEffectEvent -= ActionFromSelf;
    }

    public static string ShowStrSelf { get; private set; } = string.Empty;
    public static string ShowStrEnemy { get; private set; } = string.Empty;

    private static void ActionFromEnemy(ActionEffectSet set)
    {
        try
        {
            // Check Source.
            var source = set.Source;
            if (source == null) return;
            if (source is not IBattleChara battle) return;
            if (battle is IPlayerCharacter) return;
            const int FriendSubKind = 9;
            if (battle.SubKind == FriendSubKind) return; // Friend!
            if (Svc.Objects.SearchById(battle.GameObjectId) is IPlayerCharacter) return;

            var damageRatio = set.TargetEffects
                .Where(e => e.TargetID == Player.Object.GameObjectId)
                .SelectMany(e => new EffectEntry[]
            {
                e[0], e[1], e[2], e[3], e[4], e[5], e[6], e[7]
            })
                .Where(e => e.type == ActionEffectType.Damage)
                .Sum(e => (float)e.value / Player.Object.MaxHp);

            DataCenter.AddDamageRec(damageRatio);

            ShowStrEnemy = $"Damage Ratio: {damageRatio}\n{set}";

            foreach (var effect in set.TargetEffects)
            {
                if (effect.TargetID != Player.Object.GameObjectId) continue;
                if (effect.GetSpecificTypeEffect(ActionEffectType.Knockback, out var entry))
                {
                    var knock = Svc.Data.GetExcelSheet<Knockback>()?.GetRow(entry.value);
                    if (knock != null)
                    {
                        DataCenter.KnockbackStart = DateTime.UtcNow;
                        DataCenter.KnockbackFinished = DateTime.UtcNow + TimeSpan.FromSeconds(knock.Distance / (float)knock.Speed);
                        if (set.Action != null && !OtherConfiguration.HostileCastingKnockback.Contains(set.Action.RowId) && Service.Config.RecordKnockbackies)
                        {
                            OtherConfiguration.HostileCastingKnockback.Add(set.Action.RowId);
                            OtherConfiguration.Save();
                        }
                    }
                    break;
                }
            }

            if (set.Header.ActionType == ActionType.Action && DataCenter.PartyMembers.Length >= 4 && set.Action?.Cast100ms > 0)
            {
                var type = set.Action.GetActionCate();

                if (type is ActionCate.Spell or ActionCate.Weaponskill or ActionCate.Ability)
                {
                    if (set.TargetEffects.Count(e =>
                        DataCenter.PartyMembers.Any(p => p.GameObjectId == e.TargetID)
                        && e.GetSpecificTypeEffect(ActionEffectType.Damage, out var effect)
                        && (effect.value > 0 || (effect.param0 & 6) == 6))
                        == DataCenter.PartyMembers.Length)
                    {
                        if (Service.Config.RecordCastingArea)
                        {
                            OtherConfiguration.HostileCastingArea.Add(set.Action.RowId);
                            OtherConfiguration.SaveHostileCastingArea();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error in ActionFromEnemy: {ex}");
        }
    }

    private static void ActionFromSelf(ActionEffectSet set)
    {
        try
        {
            if (set.Source == null || Player.Object == null) return;
            if (set.Source.GameObjectId != Player.Object.GameObjectId) return;
            if (set.Header.ActionType != ActionType.Action && set.Header.ActionType != ActionType.Item) return;
            if (set.Action == null) return;
            if ((ActionCate)set.Action.ActionCategory.Value!.RowId == ActionCate.Autoattack) return;

            var id = set.Action.RowId;
            if (!set.Action.IsRealGCD() && (set.Action.ClassJob.Row > 0 || Enum.IsDefined((ActionID)id)))
            {
                OtherConfiguration.AnimationLockTime[id] = set.Header.AnimationLockTime;
            }

            if (!set.TargetEffects.Any()) return;

            var action = set.Action;
            var tar = set.Target;

            // Record
            DataCenter.AddActionRec(action);
            ShowStrSelf = set.ToString();

            DataCenter.HealHP = set.GetSpecificTypeEffect(ActionEffectType.Heal);
            DataCenter.ApplyStatus = set.GetSpecificTypeEffect(ActionEffectType.ApplyStatusEffectTarget);
            foreach (var effect in set.GetSpecificTypeEffect(ActionEffectType.ApplyStatusEffectSource))
            {
                DataCenter.ApplyStatus[effect.Key] = effect.Value;
            }
            DataCenter.MPGain = (uint)set.GetSpecificTypeEffect(ActionEffectType.MpGain).Where(i => i.Key == Player.Object.GameObjectId).Sum(i => i.Value);
            DataCenter.EffectTime = DateTime.UtcNow;
            DataCenter.EffectEndTime = DateTime.UtcNow.AddSeconds(set.Header.AnimationLockTime + 1);

            foreach (var effect in set.TargetEffects)
            {
                if (!effect.GetSpecificTypeEffect(ActionEffectType.Damage, out _)) continue;

                // Check if the target is already in the attacked targets list
                if (DataCenter.AttackedTargets.Any(i => i.id == effect.TargetID)) continue;

                // Ensure the current target is not dequeued
                while (DataCenter.AttackedTargets.Count >= DataCenter.ATTACKED_TARGETS_COUNT)
                {
                    var oldestTarget = DataCenter.AttackedTargets.Peek();
                    if (oldestTarget.id == effect.TargetID)
                    {
                        // If the oldest target is the current target, break the loop to avoid dequeuing it
                        break;
                    }
                    DataCenter.AttackedTargets.Dequeue();
                }

                // Enqueue the new target
                DataCenter.AttackedTargets.Enqueue((effect.TargetID, DateTime.UtcNow));
            }

            // Macro
            var regexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase;
            foreach (var item in Service.Config.Events)
            {
                if (!new Regex(item.Name, regexOptions).Match(action.Name).Success) continue;
                if (item.AddMacro(tar)) break;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error in ActionFromSelf: {ex}");
        }
    }
}