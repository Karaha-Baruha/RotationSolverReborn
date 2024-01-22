﻿using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using RotationSolver.Basic.Configuration;

namespace RotationSolver.Basic.Actions;
public struct ActionBasicInfo
{
    internal static readonly uint[] ActionsNoNeedCasting =
    [
        5,
        (uint)ActionID.PowerfulShotPvP,
        (uint)ActionID.BlastChargePvP,
    ];

    private readonly IBaseAction _action;
    public readonly string Name => _action.Action.Name;
    public readonly uint ID => _action.Action.RowId;
    public readonly uint IconID => ID == (uint)ActionID.SprintPvE ? 104u : _action.Action.Icon;

    public readonly uint AdjustedID => (uint)Service.GetAdjustedActionId((ActionID)ID);

    public readonly AttackType AttackType => (AttackType)(_action.Action.AttackType.Value?.RowId ?? byte.MaxValue);

    public readonly float AnimationLockTime => OtherConfiguration.AnimationLockTime?.TryGetValue(AdjustedID, out var time) ?? false ? time : 0.6f;

    public readonly byte Level => _action.Action.ClassJobLevel;
    public readonly bool EnoughLevel => Player.Level >= Level;

    public readonly bool IsPvP => _action.Action.IsPvP;
    /// <summary>
    /// Casting time.
    /// </summary>
    public readonly unsafe float CastTime => ActionManager.GetAdjustedCastTime(ActionType.Action, AdjustedID) / 1000f;

    public readonly bool IsOnSlot
    {
        get
        {
            if (IsDutyAction)
            {
                return DataCenter.DutyActions.Contains(AdjustedID);
            }

            return IsPvP == DataCenter.Territory?.IsPvpZone;
        }
    }
    public bool IsLimitBreak { get; }
    public bool IsGeneralGCD { get; }
    public bool IsRealGCD { get; }
    public bool IsDutyAction { get; }
    public Aspect Aspect { get; }

    public ActionBasicInfo(IBaseAction action, bool isDutyAction)
    {
        _action = action;
        IsGeneralGCD = _action.Action.IsGeneralGCD();
        IsRealGCD = _action.Action.IsRealGCD();
        IsLimitBreak = _action.Action.ActionCategory?.Value?.RowId == 9;
        IsDutyAction = isDutyAction;
        Aspect = (Aspect)_action.Action.Aspect;
    }

    internal readonly bool BasicCheck(bool skipStatusProvideCheck, bool skipCombo, bool ignoreCastingCheck)
    {
        if (!_action.Config.IsEnable || !IsOnSlot) return false;

        //Disabled.
        if (DataCenter.DisabledActionSequencer?.Contains(ID) ?? false) return false;

        if (!EnoughLevel) return false;

        var player = Player.Object;

        if (_action.Setting.StatusNeed != null)
        {
            if (!player.HasStatus(true, _action.Setting.StatusNeed)) return false;
        }

        if (_action.Setting.StatusProvide != null && !skipStatusProvideCheck)
        {
            if (player.HasStatus(true, _action.Setting.StatusProvide)) return false;
        }

        if(!skipCombo && IsGeneralGCD)
        {
            if (!CheckForCombo()) return false;
        }

        //Need casting.
        if (CastTime > 0 && !player.HasStatus(true, 
            [
                StatusID.Swiftcast,
                StatusID.Triplecast,
                StatusID.Dualcast,
            ])
            && !ActionsNoNeedCasting.Contains(ID))
        {
            //Is knocking back.
            if (DateTime.Now > DataCenter.KnockbackStart && DateTime.Now < DataCenter.KnockbackFinished) return false;

            if (DataCenter.NoPoslock && DataCenter.IsMoving && !ignoreCastingCheck) return false;
        }

        if (IsGeneralGCD && _action.Setting.StatusProvide?.Length > 0 && _action.Setting.IsFriendly
            && IActionHelper.IsLastGCD(true, _action)
            && DataCenter.TimeSinceLastAction.TotalSeconds < 3) return false;

        if (!(_action.Setting.ActionCheck?.Invoke() ?? true)) return false;

        return true;
    }

    private readonly bool CheckForCombo()
    {
        if (_action.Setting.ComboIdsNot != null)
        {
            if (_action.Setting.ComboIdsNot.Contains(DataCenter.LastComboAction)) return false;
        }

        var comboActions = (_action.Action.ActionCombo?.Row ?? 0) != 0
            ? new ActionID[] { (ActionID)_action.Action.ActionCombo!.Row }
            : [];


        if (_action.Setting.ComboIds != null) comboActions = [.. comboActions, .. _action.Setting.ComboIds];

        if (comboActions.Length > 0)
        {
            if (comboActions.Contains(DataCenter.LastComboAction))
            {
                if (DataCenter.ComboTime < DataCenter.WeaponRemain) return false;
            }
            else
            {
                return false;
            }
        }
        return true;
    }
}


