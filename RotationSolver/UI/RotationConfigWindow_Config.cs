﻿using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using ECommons.ImGuiMethods;
using RotationSolver.Basic.Configuration;
using RotationSolver.Basic.Configuration.Conditions;
using RotationSolver.Data;

using RotationSolver.UI.SearchableSettings;

namespace RotationSolver.UI;

public partial class RotationConfigWindow
{
    private string _searchText = string.Empty;
    private ISearchable[] _searchResults = [];

    internal static SearchableCollection _allSearchable = new();

    private void SearchingBox()
    {
        if (ImGui.InputTextWithHint("##Rotation Solver Reborn Search Box", UiString.ConfigWindow_Searching.GetDescription(), ref _searchText, 128, ImGuiInputTextFlags.AutoSelectAll))
        {
            _searchResults = _allSearchable.SearchItems(_searchText);
        }
    }

    #region Basic
    private static void DrawBasic()
    {
        _baseHeader?.Draw();
    }

    private static readonly CollapsingHeaderGroup _baseHeader = new(new()
    {
        { UiString.ConfigWindow_Basic_Timer.GetDescription, DrawBasicTimer },
        { UiString.ConfigWindow_Basic_AutoSwitch.GetDescription, DrawBasicAutoSwitch },
        { UiString.ConfigWindow_Basic_NamedConditions.GetDescription, DrawBasicNamedConditions },
        { UiString.ConfigWindow_Basic_Others.GetDescription, DrawBasicOthers },
    });

    private static readonly uint PING_COLOR = ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedGreen);
    private static readonly uint LOCK_TIME_COLOR = ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedBlue);
    private static readonly uint WEAPON_DELAY_COLOR = ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedGold);
    private static readonly uint IDEAL_CLICK_TIME_COLOR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0f, 0f, 1f));
    private static readonly uint CLICK_TIME_COLOR = ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedPink);
    private static readonly uint ADVANCE_TIME_COLOR = ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudYellow);
    private static readonly uint ADVANCE_ABILITY_TIME_COLOR = ImGui.ColorConvertFloat4ToU32(ImGuiColors.ParsedOrange);
    const float gcdSize = 50, ogcdSize = 40, pingHeight = 12, spacingHeight = 8;

    private static void AddPingLockTime(ImDrawListPtr drawList, Vector2 lineStart, float sizePerTime, float ping, float animationLockTime, float advanceTime, uint color, float clickTime)
    {
        var size = new Vector2(ping * sizePerTime, pingHeight);
        drawList.AddRectFilled(lineStart, lineStart + size, ChangeAlpha(PING_COLOR));
        if (ImGuiHelper.IsInRect(lineStart, size))
        {
            ImguiTooltips.ShowTooltip(UiString.ConfigWindow_Basic_Ping.GetDescription());
        }

        var rectStart = lineStart + new Vector2(ping * sizePerTime, 0);
        size = new Vector2(animationLockTime * sizePerTime, pingHeight);
        drawList.AddRectFilled(rectStart, rectStart + size, ChangeAlpha(LOCK_TIME_COLOR));
        if (ImGuiHelper.IsInRect(rectStart, size))
        {
            ImguiTooltips.ShowTooltip(UiString.ConfigWindow_Basic_AnimationLockTime.GetDescription());
        }

        drawList.AddLine(lineStart - new Vector2(0, spacingHeight), lineStart + new Vector2(0, pingHeight * 2 + spacingHeight / 2), IDEAL_CLICK_TIME_COLOR, 1.5f);

        rectStart = lineStart + new Vector2(-advanceTime * sizePerTime, pingHeight);
        size = new Vector2(advanceTime * sizePerTime, pingHeight);
        drawList.AddRectFilled(rectStart, rectStart + size, ChangeAlpha(color));
        if (ImGuiHelper.IsInRect(rectStart, size))
        {
            ImguiTooltips.ShowTooltip(() =>
            {
                ImGui.TextWrapped(UiString.ConfigWindow_Basic_ClickingDuration.GetDescription());

                ImGui.Separator();

                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(IDEAL_CLICK_TIME_COLOR),
                    UiString.ConfigWindow_Basic_IdealClickingTime.GetDescription());

                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(CLICK_TIME_COLOR),
                    UiString.ConfigWindow_Basic_RealClickingTime.GetDescription());
            });
        }

        float time = 0;
        while (time < advanceTime)
        {
            var start = lineStart + new Vector2((time - advanceTime) * sizePerTime, 0);
            drawList.AddLine(start + new Vector2(0, pingHeight), start + new Vector2(0, pingHeight * 2 + spacingHeight), CLICK_TIME_COLOR, 2.5f);

            time += clickTime;
        }
    }
    private static void DrawBasicTimer()
    {
        _allSearchable.DrawItems(Configs.BasicTimer);
    }

    private static readonly CollapsingHeaderGroup _autoSwitch = new(new()
    {
        {
            UiString.ConfigWindow_Basic_SwitchCancelConditionSet.GetDescription,
            () => DataCenter.RightSet.SwitchCancelConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },
        {
            UiString.ConfigWindow_Basic_SwitchManualConditionSet.GetDescription,
            () => DataCenter.RightSet.SwitchManualConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },
        {
            UiString.ConfigWindow_Basic_SwitchAutoConditionSet.GetDescription,
            () => DataCenter.RightSet.SwitchAutoConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },
    })
    {
        HeaderSize = 18,
    };
    private static void DrawBasicAutoSwitch()
    {
        _allSearchable.DrawItems(Configs.BasicAutoSwitch);
        _autoSwitch?.Draw();
    }

    private static readonly Dictionary<int, bool> _isOpen = [];
    private static void DrawBasicNamedConditions()
    {
        if (!DataCenter.RightSet.NamedConditions.Any(c => string.IsNullOrEmpty(c.Name)))
        {
            DataCenter.RightSet.NamedConditions = [.. DataCenter.RightSet.NamedConditions, (string.Empty, new ConditionSet())];
        }

        ImGui.Spacing();

        int removeIndex = -1;
        for (int i = 0; i < DataCenter.RightSet.NamedConditions.Length; i++)
        {
            var value = _isOpen.TryGetValue(i, out var open) && open;

            var toggle = value ? FontAwesomeIcon.ArrowUp : FontAwesomeIcon.ArrowDown;
            var width = ImGui.GetWindowWidth() - ImGuiEx.CalcIconSize(FontAwesomeIcon.Ban).X
                - ImGuiEx.CalcIconSize(toggle).X - ImGui.GetStyle().ItemSpacing.X * 2 - 20 * Scale;

            ImGui.SetNextItemWidth(width);
            ImGui.InputTextWithHint($"##Rotation Solver Named Condition{i}", UiString.ConfigWindow_Condition_ConditionName.GetDescription(),
                ref DataCenter.RightSet.NamedConditions[i].Name, 1024);

            ImGui.SameLine();

            if (ImGuiEx.IconButton(toggle, $"##Rotation Solver Toggle Named Condition{i}"))
            {
                _isOpen[i] = value = !value;
            }

            ImGui.SameLine();

            if (ImGuiEx.IconButton(FontAwesomeIcon.Ban, $"##Rotation Solver Remove Named Condition{i}"))
            {
                removeIndex = i;
            }

            if (value && DataCenter.RightNowRotation != null)
            {
                DataCenter.RightSet.NamedConditions[i].Condition?.DrawMain(DataCenter.RightNowRotation);
            }
        }
        if (removeIndex > -1)
        {
            var list = DataCenter.RightSet.NamedConditions.ToList();
            list.RemoveAt(removeIndex);
            DataCenter.RightSet.NamedConditions = [.. list];
        }
    }

    private static void DrawBasicOthers()
    {
        _allSearchable.DrawItems(Configs.BasicParams);
    }
    #endregion

    #region UI
    private static void DrawUI()
    {
        _UIHeader?.Draw();
    }

    private static readonly CollapsingHeaderGroup _UIHeader = new(new()
    {
        {
            UiString.ConfigWindow_UI_Information.GetDescription,
            () => _allSearchable.DrawItems(Configs.UiInformation)
        },
        {
            UiString.ConfigWindow_UI_Windows.GetDescription,
            () =>_allSearchable.DrawItems(Configs.UiWindows)
        },
    });

    #endregion

    #region Auto
    private static void DrawAuto()
    {
        ImGui.TextWrapped(UiString.ConfigWindow_Auto_Description.GetDescription());
        _autoHeader?.Draw();
    }

    private static readonly CollapsingHeaderGroup _autoHeader = new(new()
    {
        { UiString.ConfigWindow_Auto_ActionUsage.GetDescription, () =>
            {
                ImGui.TextWrapped(UiString.ConfigWindow_Auto_ActionUsage_Description
                    .GetDescription());
                ImGui.Separator();

                _allSearchable.DrawItems(Configs.AutoActionUsage);
            }
        },
        { UiString.ConfigWindow_Auto_HealingCondition.GetDescription, DrawHealingActionCondition },
        { UiString.ConfigWindow_Auto_StateCondition.GetDescription, () => _autoState?.Draw() },
    });

    private static void DrawHealingActionCondition()
    {
        ImGui.TextWrapped(UiString.ConfigWindow_Auto_HealingCondition_Description.GetDescription());
        ImGui.Separator();

        _allSearchable.DrawItems(Configs.HealingActionCondition);
    }

    private static readonly CollapsingHeaderGroup _autoState = new(new()
    {
        {
            UiString.ConfigWindow_Auto_HealAreaConditionSet.GetDescription,
            () => DataCenter.RightSet.HealAreaConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_HealSingleConditionSet.GetDescription,
            () => DataCenter.RightSet.HealSingleConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_DefenseAreaConditionSet.GetDescription,
            () => DataCenter.RightSet.DefenseAreaConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_DefenseSingleConditionSet.GetDescription,
            () => DataCenter.RightSet.DefenseSingleConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
             UiString.ConfigWindow_Auto_DispelStancePositionalConditionSet.GetDescription,
            () => DataCenter.RightSet.DispelStancePositionalConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_RaiseShirkConditionSet.GetDescription,
            () => DataCenter.RightSet.RaiseShirkConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_MoveForwardConditionSet.GetDescription,
            () => DataCenter.RightSet.MoveForwardConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_MoveBackConditionSet.GetDescription,
            () => DataCenter.RightSet.MoveBackConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_AntiKnockbackConditionSet.GetDescription,
            () => DataCenter.RightSet.AntiKnockbackConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_SpeedConditionSet.GetDescription,
            () => DataCenter.RightSet.SpeedConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },

        {
            UiString.ConfigWindow_Auto_LimitBreakConditionSet.GetDescription,
            () => DataCenter.RightSet.LimitBreakConditionSet?.DrawMain(DataCenter.RightNowRotation)
        },
    })
    {
        HeaderSize = 18,
    };

    private static void DrawAutoActionCondition()
    {
        ImGui.TextWrapped(UiString.ConfigWindow_Auto_ActionCondition_Description.GetDescription());
        ImGui.Separator();

        _allSearchable.DrawItems(Configs.AutoActionCondition);
    }
    #endregion

    #region Target
    private static void DrawTarget()
    {
        _targetHeader?.Draw();
    }

    private static readonly CollapsingHeaderGroup _targetHeader = new(new()
    {
        { UiString.ConfigWindow_Target_Config.GetDescription, DrawTargetConfig },
        { UiString.ConfigWindow_List_Hostile.GetDescription, DrawTargetHostile },
    });

    private static void DrawTargetConfig()
    {
        _allSearchable.DrawItems(Configs.TargetConfig);
    }

    private static void DrawTargetHostile()
    {
        if (ImGuiEx.IconButton(FontAwesomeIcon.Plus, "Add Hostile"))
        {
            Service.Config.TargetingTypes.Add(TargetingType.Big);
        }
        ImGui.SameLine();
        ImGui.TextWrapped(UiString.ConfigWindow_Param_HostileDesc.GetDescription());

        for (int i = 0; i < Service.Config.TargetingTypes.Count; i++)
        {
            var targetType = Service.Config.TargetingTypes[i];

            void Delete()
            {
                Service.Config.TargetingTypes.RemoveAt(i);
            };

            void Up()
            {
                Service.Config.TargetingTypes.RemoveAt(i);
                Service.Config.TargetingTypes.Insert(Math.Max(0, i - 1), targetType);
            };
            void Down()
            {
                Service.Config.TargetingTypes.RemoveAt(i);
                Service.Config.TargetingTypes.Insert(Math.Min(Service.Config.TargetingTypes.Count - 1, i + 1), targetType);
            }

            var key = $"Targeting Type Pop Up: {i}";

            ImGuiHelper.DrawHotKeysPopup(key, string.Empty,
                (UiString.ConfigWindow_List_Remove.GetDescription(), Delete, ["Delete"]),
                (UiString.ConfigWindow_Actions_MoveUp.GetDescription(), Up, ["↑"]),
                (UiString.ConfigWindow_Actions_MoveDown.GetDescription(), Down, ["↓"]));

            var names = Enum.GetNames(typeof(TargetingType));
            var targingType = (int)Service.Config.TargetingTypes[i];
            var text = UiString.ConfigWindow_Param_HostileCondition.GetDescription();
            ImGui.SetNextItemWidth(ImGui.CalcTextSize(text).X + 30 * Scale);
            if (ImGui.Combo(text + "##HostileCondition" + i.ToString(), ref targingType, names, names.Length))
            {
                Service.Config.TargetingTypes[i] = (TargetingType)targingType;
            }

            ImGuiHelper.ExecuteHotKeysPopup(key, string.Empty, string.Empty, true,
                (Delete, [VirtualKey.DELETE]),
                (Up, [VirtualKey.UP]),
                (Down, [VirtualKey.DOWN]));
        }
    }
    #endregion

    #region Extra
    private static void DrawExtra()
    {
        ImGui.TextWrapped(UiString.ConfigWindow_Extra_Description.GetDescription());
        _extraHeader?.Draw();
    }
    private static readonly CollapsingHeaderGroup _extraHeader = new(new()
    {
        { UiString.ConfigWindow_EventItem.GetDescription, DrawEventTab },

        {
            UiString.ConfigWindow_Extra_Others.GetDescription,
            () => _allSearchable.DrawItems(Configs.Extra)
        },
    });

    private static void DrawEventTab()
    {
        if (ImGui.Button(UiString.ConfigWindow_Events_AddEvent.GetDescription()))
        {
            Service.Config.Events.Add(new ActionEventInfo());
        }
        ImGui.SameLine();

        ImGui.TextWrapped(UiString.ConfigWindow_Events_Description.GetDescription());

        ImGui.Text(UiString.ConfigWindow_Events_DutyStart.GetDescription());
        ImGui.SameLine();
        Service.Config.DutyStart.DisplayMacro();

        ImGui.Text(UiString.ConfigWindow_Events_DutyEnd.GetDescription());
        ImGui.SameLine();
        Service.Config.DutyEnd.DisplayMacro();

        ImGui.Separator();

        ActionEventInfo? remove = null;
        foreach (var eve in Service.Config.Events)
        {
            eve.DisplayEvent();

            ImGui.SameLine();

            if (ImGui.Button($"{UiString.ConfigWindow_Events_RemoveEvent.GetDescription()}##RemoveEvent{eve.GetHashCode()}"))
            {
                remove = eve;
            }
            ImGui.Separator();
        }
        if (remove != null)
        {
            Service.Config.Events.Remove(remove);
        }
    }
    #endregion
}
