using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Missions;
using Operator.Missions.Core;
using TMPro;
using UnityEngine;

namespace Operator.Missions.Unity
{
    /// <summary>Миссии: автовыдача, брифинг, маркер цели, HUD сектора.</summary>
    public class MissionManager : MonoBehaviour
    {
        [SerializeField] MissionCatalog missionCatalog;
        [SerializeField] Transform drone;
        [SerializeField] GameObject briefingPanel;
        [SerializeField] TextMeshProUGUI speakerText;
        [SerializeField] TextMeshProUGUI messageText;
        [SerializeField] NavigationHud navigationHud;

        bool _wasAtBase;

        void Awake()
        {
            if (drone == null)
            {
                var controller = FindFirstObjectByType<DroneController>();
                if (controller != null)
                {
                    drone = controller.transform;
                }
            }

            if (navigationHud == null)
            {
                navigationHud = GetComponent<NavigationHud>();
            }
        }

        void Start()
        {
            _wasAtBase = IsAtBase();
            RefreshMissionPresentation(forceBriefing: true);
        }

        void Update()
        {
            var atBase = IsAtBase();
            if (atBase != _wasAtBase)
            {
                _wasAtBase = atBase;
                RefreshMissionPresentation(forceBriefing: false);
            }
        }

        void RefreshMissionPresentation(bool forceBriefing)
        {
            var mission = GameBootstrap.Instance?.Session?.ActiveMission;

            if (mission == null)
            {
                SetBriefingVisible(false);
                navigationHud?.HideTarget();
                return;
            }

            navigationHud?.ShowTarget(mission.TargetCell);

            var showBriefing = forceBriefing || IsAtBase();
            if (showBriefing)
            {
                if (speakerText != null)
                {
                    speakerText.text = mission.SpeakerTitle ?? string.Empty;
                }

                if (messageText != null)
                {
                    messageText.text = mission.BriefingText ?? string.Empty;
                }
            }

            SetBriefingVisible(showBriefing);
        }

        void SetBriefingVisible(bool visible)
        {
            if (briefingPanel != null)
            {
                briefingPanel.SetActive(visible);
            }
        }

        bool IsAtBase()
        {
            if (drone == null)
            {
                return true;
            }

            var cell = WorldGrid.WorldToCell(drone.position);
            return cell.x == GarageBounds.ShaftX && cell.y <= GarageBounds.SecondCell.y;
        }
    }
}
